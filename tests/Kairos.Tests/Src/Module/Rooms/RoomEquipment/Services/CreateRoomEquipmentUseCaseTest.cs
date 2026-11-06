using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Rooms.RoomEquipment.Services;

public sealed class CreateRoomEquipmentUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IRoomRepository> _roomRepoMock;
    private readonly Mock<IEquipmentRepository> _equipmentRepoMock;
    private readonly Mock<IRoomEquipmentRepository> _roomEquipmentRepoMock;
    private readonly CreateRoomEquipmentUseCase _useCase;

    public CreateRoomEquipmentUseCaseTest()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _roomRepoMock = new Mock<IRoomRepository>();
        _equipmentRepoMock = new Mock<IEquipmentRepository>();
        _roomEquipmentRepoMock = new Mock<IRoomEquipmentRepository>();

        _uowMock.Setup(u => u.RoomRepository).Returns(_roomRepoMock.Object);
        _uowMock.Setup(u => u.EquipmentRepository).Returns(_equipmentRepoMock.Object);
        _uowMock.Setup(u => u.RoomEquipmentRepository).Returns(_roomEquipmentRepoMock.Object);

        _useCase = new CreateRoomEquipmentUseCase(_uowMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenValidCommandProvided()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var equipmentId = Guid.NewGuid().ToString();

        var dto = new AddEquipmentToRoomDto
        {
            EquipmentId = equipmentId,
            Quantity = 2,
            Notes = "Test notes",
            RoomId = roomId
        };

        var command = new AddEquipmentToRoomCommand(roomId, dto);

        _roomRepoMock.Setup(r => r.ExistsAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _equipmentRepoMock.Setup(e => e.ExistsAsync(equipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _roomEquipmentRepoMock.Setup(re => re.ExistsByRoomAndEquipmentAsync(roomId, equipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.RoomId.Should().Be(roomId);
        result.Value.EquipmentId.Should().Be(equipmentId);
        result.Value.Quantity.Should().Be(2);
        
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var equipmentId = Guid.NewGuid().ToString();

        var dto = new AddEquipmentToRoomDto
        {
            EquipmentId = equipmentId,
            Quantity = 1,
            RoomId = roomId
        };

        var command = new AddEquipmentToRoomCommand(roomId, dto);

        _roomRepoMock.Setup(r => r.ExistsAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room doesn't exist.");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenEquipmentDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var equipmentId = Guid.NewGuid().ToString();

        var dto = new AddEquipmentToRoomDto
        {
            EquipmentId = equipmentId,
            Quantity = 1,
            RoomId = roomId
        };

        var command = new AddEquipmentToRoomCommand(roomId, dto);

        _roomRepoMock.Setup(r => r.ExistsAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _equipmentRepoMock.Setup(e => e.ExistsAsync(equipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Equipment doesn't exist.");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenAssociationAlreadyExists()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();
        var equipmentId = Guid.NewGuid().ToString();

        var dto = new AddEquipmentToRoomDto
        {
            EquipmentId = equipmentId,
            Quantity = 1,
            RoomId = roomId
        };

        var command = new AddEquipmentToRoomCommand(roomId, dto);

        _roomRepoMock.Setup(r => r.ExistsAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _equipmentRepoMock.Setup(e => e.ExistsAsync(equipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _roomEquipmentRepoMock.Setup(re => re.ExistsByRoomAndEquipmentAsync(roomId, equipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain("Equipment is already associated with this room.");
    }
}