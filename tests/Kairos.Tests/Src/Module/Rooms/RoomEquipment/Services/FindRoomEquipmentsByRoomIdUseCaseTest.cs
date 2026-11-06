using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomEquipment.Services;

public sealed class FindRoomEquipmentsByRoomIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRoomRepository> _roomRepositoryMock = new();
    private readonly Mock<IRoomEquipmentRepository> _roomEquipmentRepositoryMock = new();

    private readonly FindRoomEquipmentsByRoomIdUseCase _sut;

    public FindRoomEquipmentsByRoomIdUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.RoomRepository)
            .Returns(_roomRepositoryMock.Object);

        _unitOfWorkMock
            .SetupGet(x => x.RoomEquipmentRepository)
            .Returns(_roomEquipmentRepositoryMock.Object);

        _sut = new FindRoomEquipmentsByRoomIdUseCase(
            _unitOfWorkMock.Object);
    }

    // ---------------------------------------------------------------
    // Room ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room ID is required.");

        VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-uuid")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsInvalid(
        string roomId)
    {
        // Act
        var result = await _sut.ExecuteAsync(roomId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        if (string.IsNullOrWhiteSpace(roomId))
        {
            result.Errors.Should().Contain("Room ID is required.");
        }
        else
        {
            result.Errors.Should().Contain("Room ID must be a valid UUID.");
        }

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Room existence
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        _roomRepositoryMock
            .Setup(x => x.ExistsAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(roomId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room doesn't exist.");

        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _roomEquipmentRepositoryMock.Verify(
            x => x.GetByRoomIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Find room equipments
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEquipments_WhenRoomExists()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        var expected = new List<RoomEquipmentModel>
        {
            new()
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = roomId,
                EquipmentId = Guid.NewGuid().ToString(),
                Quantity = 2,
                Notes = "Projector",
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = roomId,
                EquipmentId = Guid.NewGuid().ToString(),
                Quantity = 5,
                Notes = "Microphones",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        SetupRoomExists(roomId);

        _roomEquipmentRepositoryMock
            .Setup(x => x.GetByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(roomId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(expected);
        result.Value.Should().HaveCount(2);

        _roomEquipmentRepositoryMock.Verify(
            x => x.GetByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenRoomHasNoEquipments()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId);

        IReadOnlyList<RoomEquipmentModel> expected = Array.Empty<RoomEquipmentModel>();

        _roomEquipmentRepositoryMock
            .Setup(x => x.GetByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(roomId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepositories()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _roomRepositoryMock
            .Setup(x => x.ExistsAsync(roomId, cancellationToken))
            .ReturnsAsync(true);

        _roomEquipmentRepositoryMock
            .Setup(x => x.GetByRoomIdAsync(roomId, cancellationToken))
            .ReturnsAsync(Array.Empty<RoomEquipmentModel>());

        // Act
        var result = await _sut.ExecuteAsync(roomId, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(roomId, cancellationToken),
            Times.Once);

        _roomEquipmentRepositoryMock.Verify(
            x => x.GetByRoomIdAsync(roomId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCancelled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var act = () => _sut.ExecuteAsync(
            Guid.NewGuid().ToString(),
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void SetupRoomExists(string roomId)
    {
        _roomRepositoryMock
            .Setup(x => x.ExistsAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void VerifyNoRepositoryCalls()
    {
        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _roomEquipmentRepositoryMock.Verify(
            x => x.GetByRoomIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}