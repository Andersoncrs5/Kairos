using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;
using Kairos.Api.Modules.Rooms.RoomFeedback.Summary;
using Moq;

namespace Kairos.Tests.Module.Rooms.RoomFeedback.Services;

public sealed class GetRoomFeedbackSummaryUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRoomRepository> _roomRepositoryMock;
    private readonly Mock<IRoomFeedbackRepository> _roomFeedbackRepositoryMock;

    private readonly GetRoomFeedbackSummaryUseCase _sut;

    public GetRoomFeedbackSummaryUseCaseTest()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _roomRepositoryMock = new Mock<IRoomRepository>();
        _roomFeedbackRepositoryMock = new Mock<IRoomFeedbackRepository>();

        _unitOfWorkMock
            .SetupGet(uow => uow.RoomRepository)
            .Returns(_roomRepositoryMock.Object);

        _unitOfWorkMock
            .SetupGet(uow => uow.RoomFeedbackRepository)
            .Returns(_roomFeedbackRepositoryMock.Object);

        _sut = new GetRoomFeedbackSummaryUseCase(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        // Arrange
        GetRoomFeedbackSummaryCommand command = null!;

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room feedback summary command is required.");

        _roomRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-id")]
    [InlineData("123")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsInvalid(
        string roomId)
    {
        // Arrange
        var command = new GetRoomFeedbackSummaryCommand(roomId);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _roomRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetRatingSummaryByRoomIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var command = new GetRoomFeedbackSummaryCommand(roomId);

        _roomRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room doesn't exist.");

        _roomRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetRatingSummaryByRoomIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WhenRoomExists()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var command = new GetRoomFeedbackSummaryCommand(roomId);

        var room = new RoomModel
        {
            Id = roomId,
            CreatedAt = DateTimeOffset.UtcNow,
            RoomTypeId = Guid.NewGuid().ToString(),
            Code = "ROOM-001",
            Name = "Test Room",
            Location = "Building A"
        };

        RoomFeedbackRatingSummary summary = new RoomFeedbackRatingSummary(
            TotalCount: 10,
            AverageRating: 4.5,
            RatingDistribution: new Dictionary<int, int>
            {
                [1] = 1,
                [2] = 0,
                [3] = 1,
                [4] = 3,
                [5] = 5
            });

        _roomRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetRatingSummaryByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().NotBeNull();

        _roomRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetRatingSummaryByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var roomId = Guid.NewGuid().ToString();
        var command = new GetRoomFeedbackSummaryCommand(roomId);

        RoomModel room = new RoomModel
        {
            Id = roomId,
            CreatedAt = DateTimeOffset.UtcNow,
            RoomTypeId = Guid.NewGuid().ToString(),
            Code = "ROOM-001",
            Name = "Test Room",
            Location = "Building A"
        };

        RoomFeedbackRatingSummary summary = new RoomFeedbackRatingSummary(
            TotalCount: 10,
            AverageRating: 4.5,
            RatingDistribution: new Dictionary<int, int>
            {
                [1] = 1,
                [2] = 0,
                [3] = 1,
                [4] = 3,
                [5] = 5
            });

        _roomRepositoryMock
            .Setup(repository => repository.GetByIdAsync(
                roomId,
                ct))
            .ReturnsAsync(room);

        _roomFeedbackRepositoryMock
            .Setup(repository => repository.GetRatingSummaryByRoomIdAsync(
                roomId,
                ct))
            .ReturnsAsync(summary);

        // Act
        await _sut.ExecuteAsync(command, ct);

        // Assert
        _roomRepositoryMock.Verify(
            repository => repository.GetByIdAsync(roomId, ct),
            Times.Once);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetRatingSummaryByRoomIdAsync(roomId, ct),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new GetRoomFeedbackSummaryCommand(
            Guid.NewGuid().ToString());

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _roomRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _roomFeedbackRepositoryMock.Verify(
            repository => repository.GetRatingSummaryByRoomIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}