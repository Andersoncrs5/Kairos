using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.Api.Modules.Reliability.Inbox.Service.provider;
using NSubstitute;

namespace Kairos.Tests.Module.Reliability.Inbox.Services;

public sealed class FindInboxByMessageIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInboxRepository _inboxRepository;
    private readonly FindInboxByMessageIdUseCase _sut;

    public FindInboxByMessageIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _inboxRepository = Substitute.For<IInboxRepository>();

        _unitOfWork.InboxRepository.Returns(_inboxRepository);

        _sut = new FindInboxByMessageIdUseCase(_unitOfWork);
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
            .GetByMessageIdAsync(
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
            .GetByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageExists_ReturnsInbox()
    {
        // Arrange
        const string messageId = "external-message-123";

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var inbox = CreateInbox(messageId);

        _inboxRepository
            .GetByMessageIdAsync(messageId, cancellationToken)
            .Returns(Task.FromResult<InboxModel?>(inbox));

        // Act
        var result = await _sut.ExecuteAsync(
            messageId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(inbox.Id);
        result.Value.MessageId.Should().Be(messageId);
        result.Value.MessageType.Should().Be(inbox.MessageType);
        result.Value.Payload.Should().Be(inbox.Payload);

        await _inboxRepository
            .Received(1)
            .GetByMessageIdAsync(messageId, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string messageId = "unknown-message";
        var cancellationToken = CancellationToken.None;

        _inboxRepository
            .GetByMessageIdAsync(messageId, cancellationToken)
            .Returns(Task.FromResult<InboxModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(
            messageId,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should()
            .Contain("Inbox not found.");

        await _inboxRepository
            .Received(1)
            .GetByMessageIdAsync(messageId, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageIdHasSurroundingWhitespace_TrimsBeforeQuery()
    {
        // Arrange
        const string inputMessageId = "  external-message-123  ";
        const string normalizedMessageId = "external-message-123";

        var cancellationToken = CancellationToken.None;
        var inbox = CreateInbox(normalizedMessageId);

        _inboxRepository
            .GetByMessageIdAsync(
                normalizedMessageId,
                cancellationToken)
            .Returns(Task.FromResult<InboxModel?>(inbox));

        // Act
        var result = await _sut.ExecuteAsync(
            inputMessageId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MessageId.Should().Be(normalizedMessageId);

        await _inboxRepository
            .Received(1)
            .GetByMessageIdAsync(
                normalizedMessageId,
                cancellationToken);

        await _inboxRepository
            .DidNotReceive()
            .GetByMessageIdAsync(
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
            .GetByMessageIdAsync(messageId, cancellationToken)
            .Returns(Task.FromException<InboxModel?>(
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
            .GetByMessageIdAsync(messageId, cancellationToken);
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
            .GetByMessageIdAsync(messageId, cancellationToken)
            .Returns(Task.FromCanceled<InboxModel?>(
                cancellationToken));

        // Act
        var act = () => _sut.ExecuteAsync(
            messageId,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _inboxRepository
            .Received(1)
            .GetByMessageIdAsync(messageId, cancellationToken);
    }

    private static InboxModel CreateInbox(string messageId)
    {
        var now = DateTimeOffset.UtcNow;

        return new InboxModel
        {
            Id = Guid.NewGuid().ToString(),
            MessageId = messageId,
            MessageType = "test.event",
            Payload = """{"value":"integration-test"}""",
            Status = InboxStatusEnum.Pending,
            ReceivedAt = now,
            RetryCount = 0,
            CreatedAt = now
        };
    }
}