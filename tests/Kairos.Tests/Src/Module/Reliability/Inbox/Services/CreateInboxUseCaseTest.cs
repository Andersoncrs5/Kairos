using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.DTOs.Request;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.Api.Modules.Reliability.Inbox.Service.provider;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reliability.Inbox.Services;

public sealed class CreateInboxUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInboxRepository _inboxRepository;
    private readonly CreateInboxUseCase _sut;

    public CreateInboxUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _inboxRepository = Substitute.For<IInboxRepository>();

        _unitOfWork.InboxRepository.Returns(_inboxRepository);

        _sut = new CreateInboxUseCase(_unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Inbox data is required.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _inboxRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<InboxModel>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenMessageIdIsEmpty_ReturnsValidationFailure(
        string? messageId)
    {
        // Arrange
        var dto = CreateDto(messageId: messageId);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Message ID is required.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageIdExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(messageId: new string('a', 257));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Message ID cannot exceed 256 characters.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_WhenMessageTypeIsEmpty_ReturnsValidationFailure(
        string? messageType)
    {
        // Arrange
        var dto = CreateDto(messageType: messageType);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Message type is required.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageTypeExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(messageType: new string('a', 257));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Message type cannot exceed 256 characters.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_WhenPayloadIsEmpty_ReturnsValidationFailure(
        string? payload)
    {
        // Arrange
        var dto = CreateDto(payload: payload);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Payload is required.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPayloadIsInvalidJson_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(payload: """{"value":""");

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Payload must be valid JSON.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSourceExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(source: new string('a', 257));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Source cannot exceed 256 characters.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCorrelationIdExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(correlationId: new string('a', 257));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Correlation ID cannot exceed 256 characters.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenHeadersIsInvalidJson_ReturnsValidationFailure()
    {
        // Arrange
        var dto = CreateDto(headers: """{"content-type":""");

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Headers must be valid JSON.");

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var dto = CreateDto();

        _inboxRepository
            .ExistsByMessageIdAsync(
                dto.MessageId,
                Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "An inbox message with this MessageId already exists.");

        await _inboxRepository
            .Received(1)
            .ExistsByMessageIdAsync(
                dto.MessageId,
                Arg.Any<CancellationToken>());

        await _inboxRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<InboxModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_CreatesInbox()
    {
        // Arrange
        var dto = CreateDto(
            messageId: "  external-message-123  ",
            messageType: "  user.created  ",
            source: "  identity-service  ",
            headers: """{"content-type":"application/json"}""",
            correlationId: "  correlation-123  ");

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _inboxRepository
            .ExistsByMessageIdAsync(
                "external-message-123",
                cancellationToken)
            .Returns(false);

        // Act
        var result = await _sut.ExecuteAsync(dto, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);

        result.Value.Should().NotBeNull();

        var inbox = result.Value;

        inbox.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(inbox.Id, out _).Should().BeTrue();

        inbox.MessageId.Should().Be("external-message-123");
        inbox.MessageType.Should().Be("user.created");
        inbox.Payload.Should().Be(dto.Payload);
        inbox.Source.Should().Be("identity-service");
        inbox.Headers.Should().Be(dto.Headers);
        inbox.CorrelationId.Should().Be("correlation-123");

        inbox.Status.Should().Be(InboxStatusEnum.Pending);
        inbox.RetryCount.Should().Be(0);
        inbox.ReceivedAt.Should().Be(inbox.CreatedAt);

        inbox.ProcessedAt.Should().BeNull();
        inbox.NextAttemptAt.Should().BeNull();
        inbox.LastError.Should().BeNull();
        inbox.LockedBy.Should().BeNull();

        await _inboxRepository
            .Received(1)
            .ExistsByMessageIdAsync(
                "external-message-123",
                cancellationToken);

        await _inboxRepository
            .Received(1)
            .AddAsync(
                Arg.Is<InboxModel>(model =>
                    model.Id == inbox.Id &&
                    model.MessageId == "external-message-123" &&
                    model.MessageType == "user.created" &&
                    model.Status == InboxStatusEnum.Pending),
                cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageIdHasSurroundingWhitespace_TrimsBeforeCheckingExistence()
    {
        // Arrange
        var dto = CreateDto(messageId: "  external-message-123  ");

        _inboxRepository
            .ExistsByMessageIdAsync(
                "external-message-123",
                Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _inboxRepository
            .Received(1)
            .ExistsByMessageIdAsync(
                "external-message-123",
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var dto = CreateDto();

        _inboxRepository
            .ExistsByMessageIdAsync(
                dto.MessageId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(
                new InvalidOperationException("Inbox lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Inbox lookup failed.");

        await _inboxRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<InboxModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var dto = CreateDto();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.ExecuteAsync(dto, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _inboxRepository
            .DidNotReceive()
            .ExistsByMessageIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _inboxRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<InboxModel>(),
                Arg.Any<CancellationToken>());
    }

    private static CreateInboxDto CreateDto(
        string? messageId = "external-message-123",
        string? messageType = "user.created",
        string? payload = """{"userId":"user-123"}""",
        string? source = null,
        string? headers = null,
        string? correlationId = null)
    {
        return new CreateInboxDto
        {
            MessageId = messageId!,
            MessageType = messageType!,
            Payload = payload!,
            Source = source,
            Headers = headers,
            CorrelationId = correlationId
        };
    }
}