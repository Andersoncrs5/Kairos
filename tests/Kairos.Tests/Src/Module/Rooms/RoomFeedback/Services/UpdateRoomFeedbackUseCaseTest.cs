using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Rooms.RoomFeedback.Services;

public sealed class UpdateRoomFeedbackUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRoomFeedbackRepository> _roomFeedbackRepositoryMock;

    private readonly UpdateRoomFeedbackUseCase _sut;

    public UpdateRoomFeedbackUseCaseTest()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _roomFeedbackRepositoryMock = new Mock<IRoomFeedbackRepository>();

        _unitOfWorkMock
            .SetupGet(uow => uow.RoomFeedbackRepository)
            .Returns(_roomFeedbackRepositoryMock.Object);

        _sut = new UpdateRoomFeedbackUseCase(_unitOfWorkMock.Object);
    }

    private static UpdateRoomFeedbackCommand CreateCommand(
        string? feedbackId = null,
        string? requesterUserId = null,
        UpdateRoomFeedbackDto? dto = null)
    {
        return new UpdateRoomFeedbackCommand(
            feedbackId ?? Guid.NewGuid().ToString(),
            requesterUserId ?? Guid.NewGuid().ToString(),
            dto ?? CreateDto());
    }

    private static UpdateRoomFeedbackDto CreateDto(
        int rating = 4,
        string? comment = "Updated feedback",
        RoomFeedbackIssueTypeEnum? issueType = null)
    {
        return new UpdateRoomFeedbackDto
        {
            Rating = rating,
            Comment = comment,
            IssueType = issueType
        };
    }

    private static RoomFeedbackModel CreateFeedback(
        string? userId = null,
        int rating = 3,
        string? comment = "Original feedback",
        RoomFeedbackIssueTypeEnum? issueType = null)
    {
        return new RoomFeedbackModel
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = Guid.NewGuid().ToString(),
            RoomId = Guid.NewGuid().ToString(),
            UserId = userId ?? Guid.NewGuid().ToString(),
            Rating = rating,
            Comment = comment,
            IssueType = issueType,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        // Arrange
        UpdateRoomFeedbackCommand command = null!;

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room feedback update command is required.");

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenDtoIsNull()
    {
        // Arrange
        var command = new UpdateRoomFeedbackCommand(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-id")]
    [InlineData("123")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenFeedbackIdIsInvalid(
        string feedbackId)
    {
        // Arrange
        var command = CreateCommand(feedbackId: feedbackId);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-id")]
    [InlineData("123")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRequesterUserIdIsInvalid(
        string requesterUserId)
    {
        // Arrange
        var command = CreateCommand(requesterUserId: requesterUserId);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRatingIsInvalid()
    {
        // Arrange
        var command = CreateCommand(
            dto: CreateDto(rating: 6));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenFeedbackDoesNotExist()
    {
        // Arrange
        var feedbackId = Guid.NewGuid().ToString();
        var command = CreateCommand(feedbackId: feedbackId);

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                feedbackId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomFeedbackModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room feedback not found.");

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                feedbackId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnForbidden_WhenRequesterIsNotFeedbackAuthor()
    {
        // Arrange
        var requesterUserId = Guid.NewGuid().ToString();
        var feedback = CreateFeedback();

        var command = CreateCommand(
            feedbackId: feedback.Id,
            requesterUserId: requesterUserId);

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                feedback.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);
        result.Errors.Should().Contain(
            "Only the author can update this room feedback.");

        feedback.Rating.Should().Be(3);
        feedback.Comment.Should().Be("Original feedback");

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                feedback.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccessWithoutUpdating_WhenDataHasNotChanged()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var feedback = CreateFeedback(
            userId: userId,
            rating: 4,
            comment: "Existing comment");

        var originalUpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        feedback.UpdatedAt = originalUpdatedAt;

        var command = CreateCommand(
            feedbackId: feedback.Id,
            requesterUserId: userId,
            dto: CreateDto(
                rating: 4,
                comment: " Existing comment "));

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                feedback.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(feedback);

        feedback.Rating.Should().Be(4);
        feedback.Comment.Should().Be("Existing comment");
        feedback.UpdatedAt.Should().Be(originalUpdatedAt);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                feedback.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateFeedback_WhenRequesterIsAuthor()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        var feedback = CreateFeedback(
            userId: userId,
            rating: 3,
            comment: "Old comment");

        var originalUpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        feedback.UpdatedAt = originalUpdatedAt;

        var command = CreateCommand(
            feedbackId: feedback.Id,
            requesterUserId: userId,
            dto: CreateDto(
                rating: 5,
                comment: "  Updated comment  "));

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                feedback.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(feedback);

        feedback.Rating.Should().Be(5);
        feedback.Comment.Should().Be("Updated comment");
        feedback.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClearComment_WhenCommentIsNull()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var feedback = CreateFeedback(
            userId: userId,
            comment: "Old comment");

        var command = CreateCommand(
            feedbackId: feedback.Id,
            requesterUserId: userId,
            dto: CreateDto(
                rating: feedback.Rating,
                comment: null));

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                feedback.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        feedback.Comment.Should().BeNull();
        feedback.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var userId = Guid.NewGuid().ToString();
        var feedback = CreateFeedback(userId: userId);

        var command = CreateCommand(
            feedbackId: feedback.Id,
            requesterUserId: userId);

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                feedback.Id,
                ct))
            .ReturnsAsync(feedback);

        // Act
        await _sut.ExecuteAsync(command, ct);

        // Assert
        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                feedback.Id,
                ct),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = CreateCommand();

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
