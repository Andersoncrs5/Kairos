using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomEquipment.Services;

public sealed class FindRoomEquipmentByRoomAndEquipmentUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRoomEquipmentRepository> _roomEquipmentRepositoryMock = new();

    private readonly FindRoomEquipmentByRoomAndEquipmentUseCase _sut;

    public FindRoomEquipmentByRoomAndEquipmentUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.RoomEquipmentRepository)
            .Returns(_roomEquipmentRepositoryMock.Object);

        _sut = new FindRoomEquipmentByRoomAndEquipmentUseCase(
            _unitOfWorkMock.Object);
    }

    // ---------------------------------------------------------------
    // Room ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!, Guid.NewGuid().ToString());

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
        var result = await _sut.ExecuteAsync(roomId, Guid.NewGuid().ToString());

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
    // Equipment ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenEquipmentIdIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(Guid.NewGuid().ToString(), null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Equipment ID is required.");

        VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-uuid")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenEquipmentIdIsInvalid(
        string equipmentId)
    {
        // Act
        var result = await _sut.ExecuteAsync(Guid.NewGuid().ToString(), equipmentId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        if (string.IsNullOrWhiteSpace(equipmentId))
        {
            result.Errors.Should().Contain("Equipment ID is required.");
        }
        else
        {
            result.Errors.Should().Contain("Equipment ID must be a valid UUID.");
        }

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Find room equipment association
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenAssociationDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var equipmentId = Guid.NewGuid().ToString();

        _roomEquipmentRepositoryMock
            .Setup(x => x.GetByRoomAndEquipmentAsync(
                roomId,
                equipmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomEquipmentModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(roomId, equipmentId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room equipment association doesn't exist.");

        _roomEquipmentRepositoryMock.Verify(
            x => x.GetByRoomAndEquipmentAsync(
                roomId,
                equipmentId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAssociation_WhenAssociationExists()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var equipmentId = Guid.NewGuid().ToString();

        var expected = new RoomEquipmentModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            EquipmentId = equipmentId,
            Quantity = 3,
            Notes = "Projector unit",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _roomEquipmentRepositoryMock
            .Setup(x => x.GetByRoomAndEquipmentAsync(
                roomId,
                equipmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(roomId, equipmentId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(expected);
        result.Value!.Quantity.Should().Be(3);

        _roomEquipmentRepositoryMock.Verify(
            x => x.GetByRoomAndEquipmentAsync(
                roomId,
                equipmentId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepository()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var equipmentId = Guid.NewGuid().ToString();

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _roomEquipmentRepositoryMock
            .Setup(x => x.GetByRoomAndEquipmentAsync(roomId, equipmentId, cancellationToken))
            .ReturnsAsync(new RoomEquipmentModel
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = roomId,
                EquipmentId = equipmentId,
                CreatedAt = DateTimeOffset.UtcNow,
            });

        // Act
        var result = await _sut.ExecuteAsync(roomId, equipmentId, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _roomEquipmentRepositoryMock.Verify(
            x => x.GetByRoomAndEquipmentAsync(roomId, equipmentId, cancellationToken),
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
            Guid.NewGuid().ToString(),
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void VerifyNoRepositoryCalls()
    {
        _roomEquipmentRepositoryMock.Verify(
            x => x.GetByRoomAndEquipmentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}