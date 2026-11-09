using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Rooms.RoomFeedback.Services;

public sealed class FindRoomFeedbackByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRoomFeedbackRepository> _roomFeedbackRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;

    private readonly FindRoomFeedbackByIdUseCase _sut;

    private readonly string _feedbackId = Guid.NewGuid().ToString();
    private readonly string _requesterUserId = Guid.NewGuid().ToString();

    public FindRoomFeedbackByIdUseCaseTest()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _roomFeedbackRepositoryMock = new Mock<IRoomFeedbackRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();

        _unitOfWorkMock
            .SetupGet(uow => uow.RoomFeedbackRepository)
            .Returns(_roomFeedbackRepositoryMock.Object);

        _unitOfWorkMock
            .SetupGet(uow => uow.UserRepository)
            .Returns(_userRepositoryMock.Object);

        _sut = new FindRoomFeedbackByIdUseCase(_unitOfWorkMock.Object);
    }

    private static FindRoomFeedbackByIdCommand CreateCommand(
        string? feedbackId = null,
        string? requesterUserId = null)
    {
        return new FindRoomFeedbackByIdCommand(
            feedbackId ?? Guid.NewGuid().ToString(),
            requesterUserId ?? Guid.NewGuid().ToString()
            );
    }

    private static RoomFeedbackModel CreateFeedback(
        string? userId = null)
    {
        return new RoomFeedbackModel
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = Guid.NewGuid().ToString(),
            RoomId = Guid.NewGuid().ToString(),
            UserId = userId ?? Guid.NewGuid().ToString(),
            Rating = 5,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    // ---------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        // Arrange
        FindRoomFeedbackByIdCommand? command = null;

        // Act
        var result = await _sut.ExecuteAsync(command!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room feedback search command is required.");

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdWithDetailsAsync(
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
            repository => repository.GetByIdWithDetailsAsync(
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
            repository => repository.GetByIdWithDetailsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Find feedback
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenFeedbackDoesNotExist()
    {
        // Arrange
        var command = CreateCommand(
            _feedbackId,
            _requesterUserId);

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdWithDetailsAsync(
                _feedbackId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomFeedbackModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room feedback not found.");

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdWithDetailsAsync(
                _feedbackId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.ExistsPermission(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFeedback_WhenRequesterIsAuthor()
    {
        // Arrange
        var command = CreateCommand(
            _feedbackId,
            _requesterUserId);

        var feedback = CreateFeedback(_requesterUserId);

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdWithDetailsAsync(
                _feedbackId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(feedback);

        _userRepositoryMock.Verify(
            repository => repository.ExistsPermission(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFeedback_WhenRequesterHasManagePermission()
    {
        // Arrange
        var command = CreateCommand(
            _feedbackId,
            _requesterUserId);

        var feedback = CreateFeedback();

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdWithDetailsAsync(
                _feedbackId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        _userRepositoryMock
            .Setup(repository => repository.ExistsPermission(
                "rooms.feedbacks.manage",
                _requesterUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(feedback);

        _userRepositoryMock.Verify(
            repository => repository.ExistsPermission(
                "rooms.feedbacks.manage",
                _requesterUserId),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRequesterIsNotAuthorOrModerator()
    {
        // Arrange
        var command = CreateCommand(
            _feedbackId,
            _requesterUserId);

        var feedback = CreateFeedback();

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdWithDetailsAsync(
                _feedbackId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        _userRepositoryMock
            .Setup(repository => repository.ExistsPermission(
                "rooms.feedbacks.manage",
                _requesterUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room feedback not found.");

        _userRepositoryMock.Verify(
            repository => repository.ExistsPermission(
                "rooms.feedbacks.manage",
                _requesterUserId),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsAlreadyCanceled()
    {
        // Arrange
        var command = CreateCommand(
            _feedbackId,
            _requesterUserId);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = async () => await _sut.ExecuteAsync(
            command,
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdWithDetailsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateCancellationTokenToRepository()
    {
        // Arrange
        var command = CreateCommand(
            _feedbackId,
            _requesterUserId);

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var feedback = CreateFeedback(_requesterUserId);

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetByIdWithDetailsAsync(
                _feedbackId,
                cancellationToken))
            .ReturnsAsync(feedback);

        // Act
        var result = await _sut.ExecuteAsync(
            command,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetByIdWithDetailsAsync(
                _feedbackId,
                cancellationToken),
            Times.Once);
    }
}