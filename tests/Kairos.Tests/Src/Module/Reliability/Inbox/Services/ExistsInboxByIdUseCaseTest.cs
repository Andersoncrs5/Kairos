using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.Api.Modules.Reliability.Inbox.Service.provider;
using NSubstitute;

namespace Kairos.Tests.Module.Reliability.Inbox.Services;

public sealed class ExistsInboxByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInboxRepository _inboxRepository;
    private readonly ExistsInboxByIdUseCase _sut;

    public ExistsInboxByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _inboxRepository = Substitute.For<IInboxRepository>();

        _unitOfWork.InboxRepository.Returns(_inboxRepository);

        _sut = new ExistsInboxByIdUseCase(_unitOfWork);
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
            .Contain("Inbox ID is required.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdExists_ReturnsTrue()
    {
        // Arrange
        const string id = "inbox-123";
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _inboxRepository
            .ExistsAsync(id, cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();
        result.Errors.Should().BeEmpty();

        await _inboxRepository
            .Received(1)
            .ExistsAsync(id, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdDoesNotExist_ReturnsFalse()
    {
        // Arrange
        const string id = "inbox-unknown";
        var cancellationToken = CancellationToken.None;

        _inboxRepository
            .ExistsAsync(id, cancellationToken)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeFalse();
        result.Errors.Should().BeEmpty();

        await _inboxRepository
            .Received(1)
            .ExistsAsync(id, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdHasSurroundingWhitespace_TrimsBeforeQuery()
    {
        // Arrange
        const string inputId = "  inbox-123  ";
        const string normalizedId = "inbox-123";

        var cancellationToken = CancellationToken.None;

        _inboxRepository
            .ExistsAsync(normalizedId, cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            inputId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _inboxRepository
            .Received(1)
            .ExistsAsync(normalizedId, cancellationToken);

        await _inboxRepository
            .DidNotReceive()
            .ExistsAsync(inputId, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        const string id = "inbox-123";
        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Inbox lookup failed.");

        _inboxRepository
            .ExistsAsync(id, cancellationToken)
            .Returns(Task.FromException<bool>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Inbox lookup failed.");

        await _inboxRepository
            .Received(1)
            .ExistsAsync(id, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryIsCancelled_PropagatesCancellation()
    {
        // Arrange
        const string id = "inbox-123";

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cancellationToken = cts.Token;

        _inboxRepository
            .ExistsAsync(id, cancellationToken)
            .Returns(Task.FromCanceled<bool>(cancellationToken));

        // Act
        var act = () => _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _inboxRepository
            .Received(1)
            .ExistsAsync(id, cancellationToken);
    }
}