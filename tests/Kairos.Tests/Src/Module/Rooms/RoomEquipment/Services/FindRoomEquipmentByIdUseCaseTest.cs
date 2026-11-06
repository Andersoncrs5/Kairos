using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomEquipment.Services;

public sealed class FindRoomEquipmentByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IRoomEquipmentRepository> _roomEquipmentRepoMock;
    private readonly FindRoomEquipmentByIdUseCase _useCase;

    public FindRoomEquipmentByIdUseCaseTest()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _roomEquipmentRepoMock = new Mock<IRoomEquipmentRepository>();

        
        _uowMock.Setup(u => u.RoomEquipmentRepository)
            .Returns(_roomEquipmentRepoMock.Object);

        _useCase = new FindRoomEquipmentByIdUseCase(_uowMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenRoomEquipmentExists()
    {
        
        var equipmentId = Guid.NewGuid().ToString();
        var model = new RoomEquipmentModel
        {
            Id = equipmentId,
            RoomId = Guid.NewGuid().ToString(),
            EquipmentId = Guid.NewGuid().ToString(),
            Quantity = 2,
            Notes = "Test note",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _roomEquipmentRepoMock
            .Setup(r => r.GetByIdAsync(equipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        
        var result = await _useCase.ExecuteAsync(equipmentId);

        
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(equipmentId);
        result.Value.Quantity.Should().Be(2);

        _roomEquipmentRepoMock.Verify(
            r => r.GetByIdAsync(equipmentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsNullEmptyOrWhitespace()
    {
        
        var resultNull = await _useCase.ExecuteAsync(null!);
        var resultEmpty = await _useCase.ExecuteAsync(string.Empty);
        var resultWhitespace = await _useCase.ExecuteAsync("   ");

        
        resultNull.IsFailure.Should().BeTrue();
        resultNull.StatusCode.Should().Be(400);
        resultNull.Errors.Should().Contain("Room equipment ID is required.");

        resultEmpty.IsFailure.Should().BeTrue();
        resultEmpty.StatusCode.Should().Be(400);
        resultEmpty.Errors.Should().Contain("Room equipment ID is required.");

        resultWhitespace.IsFailure.Should().BeTrue();
        resultWhitespace.StatusCode.Should().Be(400);
        resultWhitespace.Errors.Should().Contain("Room equipment ID is required.");

        _roomEquipmentRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsNotValidUuid()
    {
        
        var invalidId = "not-a-guid";

        
        var result = await _useCase.ExecuteAsync(invalidId);

        
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id must be a valid UUID.");

        _roomEquipmentRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenRoomEquipmentDoesNotExist()
    {
        
        var equipmentId = Guid.NewGuid().ToString();

        _roomEquipmentRepoMock
            .Setup(r => r.GetByIdAsync(equipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomEquipmentModel?)null);

        
        var result = await _useCase.ExecuteAsync(equipmentId);

        
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Room Equipment not found.");

        _roomEquipmentRepoMock.Verify(
            r => r.GetByIdAsync(equipmentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}