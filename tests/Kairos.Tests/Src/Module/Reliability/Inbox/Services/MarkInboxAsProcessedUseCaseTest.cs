using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.Api.Modules.Reliability.Inbox.Service.provider;
using NSubstitute;

namespace Kairos.Tests.Module.Reliability.Inbox.Services;

public sealed class MarkInboxAsProcessedUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInboxRepository _inboxRepository;
    private readonly MarkInboxAsProcessedUseCase _sut;

    public MarkInboxAsProcessedUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _inboxRepository = Substitute.For<IInboxRepository>();

        _unitOfWork.InboxRepository.Returns(_inboxRepository);

        _sut = new MarkInboxAsProcessedUseCase(_unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string? id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id!, "worker-01");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id is required.");

        await _inboxRepository
            .DidNotReceive()
            .TryMarkAsProcessedAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid-id")]
    [InlineData("123")]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id, "worker-01");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should()
            .Contain("Id must be a valid UUID.");

        await _inboxRepository
            .DidNotReceive()
            .TryMarkAsProcessedAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_WhenLockedByIsEmpty_ReturnsValidationFailure(
        string? lockedBy)
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        // Act
        var result = await _sut.ExecuteAsync(id, lockedBy!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("LockedBy is required.");

        await _inboxRepository
            .DidNotReceive()
            .TryMarkAsProcessedAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLockedByExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var lockedBy = new string('a', 257);

        // Act
        var result = await _sut.ExecuteAsync(id, lockedBy);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should()
            .Contain("LockedBy cannot exceed 256 characters.");

        await _inboxRepository
            .DidNotReceive()
            .TryMarkAsProcessedAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenInboxIsProcessed_ReturnsSuccess()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string workerId = "worker-01";

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _inboxRepository
            .TryMarkAsProcessedAsync(
                id.ToString(),
                workerId,
                cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            id.ToString(),
            workerId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();

        await _inboxRepository
            .Received(1)
            .TryMarkAsProcessedAsync(
                id.ToString(),
                workerId,
                cancellationToken);

        await _inboxRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenInboxDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string workerId = "worker-01";
        var cancellationToken = CancellationToken.None;

        _inboxRepository
            .TryMarkAsProcessedAsync(
                id.ToString(),
                workerId,
                cancellationToken)
            .Returns(Task.FromResult(false));

        _inboxRepository
            .GetByIdAsync(id.ToString(), cancellationToken)
            .Returns(Task.FromResult<InboxModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(
            id.ToString(),
            workerId,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Inbox not found.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenInboxIsNotOwnedByWorker_ReturnsConflict()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string workerId = "worker-01";
        var cancellationToken = CancellationToken.None;

        var inbox = CreateInbox(
            id,
            InboxStatusEnum.Processing,
            lockedBy: "worker-02");

        _inboxRepository
            .TryMarkAsProcessedAsync(
                id.ToString(),
                workerId,
                cancellationToken)
            .Returns(Task.FromResult(false));

        _inboxRepository
            .GetByIdAsync(id.ToString(), cancellationToken)
            .Returns(Task.FromResult<InboxModel?>(inbox));

        // Act
        var result = await _sut.ExecuteAsync(
            id.ToString(),
            workerId,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Inbox is not processing or is not owned by this worker.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string workerId = "worker-01";

        _inboxRepository
            .TryMarkAsProcessedAsync(
                id.ToString(),
                workerId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(
                new InvalidOperationException("Inbox completion failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(id.ToString(), workerId);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Inbox completion failed.");
    }

    private static InboxModel CreateInbox(
        Guid id,
        InboxStatusEnum status,
        string? lockedBy = null)
    {
        var now = DateTimeOffset.UtcNow;

        return new InboxModel
        {
            Id = id.ToString(),
            MessageId = $"message-{Guid.NewGuid():N}",
            MessageType = "test.event",
            Payload = """{"value":"integration-test"}""",
            Status = status,
            ReceivedAt = now,
            RetryCount = 0,
            LockedBy = lockedBy,
            CreatedAt = now
        };
    }
}