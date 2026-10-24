using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.Api.Modules.Reliability.Inbox.Service.provider;
using NSubstitute;

namespace Kairos.Tests.Module.Reliability.Inbox.Services;

public sealed class FindInboxByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInboxRepository _inboxRepository;
    private readonly FindInboxByIdUseCase _sut;

    public FindInboxByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _inboxRepository = Substitute.For<IInboxRepository>();

        _unitOfWork.InboxRepository.Returns(_inboxRepository);

        _sut = new FindInboxByIdUseCase(_unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsNullOrWhitespace_ReturnsValidationFailure(
        string? id)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(
            id!,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should()
            .Contain("Id is required.");

        await _inboxRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid-id")]
    [InlineData("123")]
    [InlineData("not-a-uuid")]
    public async Task ExecuteAsync_WhenIdIsNotValidUuid_ReturnsValidationFailure(
        string id)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should()
            .Contain("Id must be a valid UUID.");

        await _inboxRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenInboxDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        _inboxRepository
            .GetByIdAsync(id.ToString(), cancellationToken)
            .Returns(Task.FromResult<InboxModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(
            id.ToString(),
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should()
            .Contain("Inbox not found.");

        await _inboxRepository
            .Received(1)
            .GetByIdAsync(id.ToString(), cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenInboxExists_ReturnsInbox()
    {
        // Arrange
        var id = Guid.NewGuid();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var inbox = CreateInbox(id);

        _inboxRepository
            .GetByIdAsync(id.ToString(), cancellationToken)
            .Returns(Task.FromResult<InboxModel?>(inbox));

        // Act
        var result = await _sut.ExecuteAsync(
            id.ToString(),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().NotBeNull();

        result.Value.Id.Should().Be(id.ToString());
        result.Value.MessageId.Should().Be(inbox.MessageId);
        result.Value.MessageType.Should().Be(inbox.MessageType);
        result.Value.Payload.Should().Be(inbox.Payload);

        await _inboxRepository
            .Received(1)
            .GetByIdAsync(id.ToString(), cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Inbox lookup failed.");

        _inboxRepository
            .GetByIdAsync(id.ToString(), cancellationToken)
            .Returns(Task.FromException<InboxModel?>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(
            id.ToString(),
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Inbox lookup failed.");

        await _inboxRepository
            .Received(1)
            .GetByIdAsync(id.ToString(), cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    private static InboxModel CreateInbox(Guid id)
    {
        var now = DateTimeOffset.UtcNow;

        return new InboxModel
        {
            Id = id.ToString(),
            MessageId = $"message-{Guid.NewGuid():N}",
            MessageType = "test.event",
            Payload = """{"value":"integration-test"}""",
            Status = InboxStatusEnum.Pending,
            ReceivedAt = now,
            RetryCount = 0,
            CreatedAt = now
        };
    }
}