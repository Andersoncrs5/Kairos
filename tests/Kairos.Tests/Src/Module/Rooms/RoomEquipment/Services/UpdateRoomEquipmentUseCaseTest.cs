using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Rooms.RoomEquipment.Services;

public sealed class UpdateRoomEquipmentUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IRoomEquipmentRepository> _roomEquipmentRepoMock;
    private readonly UpdateRoomEquipmentUseCase _useCase;

    public UpdateRoomEquipmentUseCaseTest()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _roomEquipmentRepoMock = new Mock<IRoomEquipmentRepository>();

        _uowMock.Setup(u => u.RoomEquipmentRepository)
            .Returns(_roomEquipmentRepoMock.Object);

        _useCase = new UpdateRoomEquipmentUseCase(_uowMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenValidCommandProvided()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var existingModel = new RoomEquipmentModel
        {
            Id = id,
            RoomId = Guid.NewGuid().ToString(),
            EquipmentId = Guid.NewGuid().ToString(),
            Quantity = 1,
            Notes = "Old notes",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = 5,
            Notes = "Updated notes"
        };

        var command = new UpdateRoomEquipmentCommand(id, dto);

        _roomEquipmentRepoMock
            .Setup(r => r.FindByIdWithForUpdateAsync(id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingModel);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Quantity.Should().Be(5);
        result.Value.Notes.Should().Be("Updated notes");

        _roomEquipmentRepoMock.Verify(
            r => r.FindByIdWithForUpdateAsync(id, null, It.IsAny<CancellationToken>()),
            Times.Once);

        _roomEquipmentRepoMock.Verify(
            r => r.Update(existingModel),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenAssociationDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = 2
        };

        var command = new UpdateRoomEquipmentCommand(id, dto);

        _roomEquipmentRepoMock
            .Setup(r => r.FindByIdWithForUpdateAsync(id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomEquipmentModel?)null);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room equipment association doesn't exist.");

        _roomEquipmentRepoMock.Verify(
            r => r.Update(It.IsAny<RoomEquipmentModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsNotValidUuid()
    {
        // Arrange
        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = 2
        };

        var command = new UpdateRoomEquipmentCommand("invalid-uuid", dto);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Room equipment association ID must be a valid UUID.");
        
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenQuantityIsInvalid()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = 0 // Inválido pelo DataAnnotations [Range(1, int.MaxValue)]
        };

        var command = new UpdateRoomEquipmentCommand(id, dto);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Quantity must be at least 1.");
    }
}