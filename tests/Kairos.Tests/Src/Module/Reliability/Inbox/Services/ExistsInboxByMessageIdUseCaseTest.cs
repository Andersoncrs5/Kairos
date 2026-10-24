using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.Api.Modules.Reliability.Inbox.Service.provider;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reliability.Inbox.Services;

public sealed class ExistsInboxByMessageIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInboxRepository _inboxRepository;
    private readonly ExistsInboxByMessageIdUseCase _sut;

    public ExistsInboxByMessageIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _inboxRepository = Substitute.For<IInboxRepository>();

        _unitOfWork.InboxRepository.Returns(_inboxRepository);

        _sut = new ExistsInboxByMessageIdUseCase(_unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenMessageIdIsNullOrWhitespace_ReturnsValidationFailure(
        string? messageId)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(
            messageId!,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should()
            .Contain("Message ID is required.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageIdExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var messageId = new string('a', 257);
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(
            messageId,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should()
            .Contain("Message ID cannot exceed 256 characters.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageExists_ReturnsTrue()
    {
        // Arrange
        const string messageId = "external-message-123";

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _inboxRepository
            .ExistsByMessageIdAsync(messageId, cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            messageId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();
        result.Errors.Should().BeEmpty();

        await _inboxRepository
            .Received(1)
            .ExistsByMessageIdAsync(messageId, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageDoesNotExist_ReturnsFalse()
    {
        // Arrange
        const string messageId = "unknown-message";
        var cancellationToken = CancellationToken.None;

        _inboxRepository
            .ExistsByMessageIdAsync(messageId, cancellationToken)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(
            messageId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeFalse();
        result.Errors.Should().BeEmpty();

        await _inboxRepository
            .Received(1)
            .ExistsByMessageIdAsync(messageId, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageIdHasSurroundingWhitespace_TrimsBeforeQuery()
    {
        // Arrange
        const string inputMessageId = "  external-message-123  ";
        const string normalizedMessageId = "external-message-123";

        var cancellationToken = CancellationToken.None;

        _inboxRepository
            .ExistsByMessageIdAsync(
                normalizedMessageId,
                cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            inputMessageId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _inboxRepository
            .Received(1)
            .ExistsByMessageIdAsync(
                normalizedMessageId,
                cancellationToken);

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                inputMessageId,
                cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        const string messageId = "external-message-123";
        var cancellationToken = CancellationToken.None;

        _inboxRepository
            .ExistsByMessageIdAsync(messageId, cancellationToken)
            .Returns(Task.FromException<bool>(
                new InvalidOperationException("Inbox lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(
            messageId,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Inbox lookup failed.");

        await _inboxRepository
            .Received(1)
            .ExistsByMessageIdAsync(messageId, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryIsCancelled_PropagatesCancellation()
    {
        // Arrange
        const string messageId = "external-message-123";

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cancellationToken = cts.Token;

        _inboxRepository
            .ExistsByMessageIdAsync(messageId, cancellationToken)
            .Returns(Task.FromCanceled<bool>(cancellationToken));

        // Act
        var act = () => _sut.ExecuteAsync(
            messageId,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _inboxRepository
            .Received(1)
            .ExistsByMessageIdAsync(messageId, cancellationToken);
    }
}