using FluentAssertions;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.IntegrationTests.Module.Room.RoomEquipment.Repository;

[Collection("Integration Tests")]
public sealed class RoomEquipmentRepositoryTest : BaseIntegrationTest
{
    public RoomEquipmentRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetByRoomAndEquipmentAsync_ReturnsNull_WhenAssociationDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var result = await repository.GetByRoomAndEquipmentAsync(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByRoomAndEquipmentAsync_ReturnsAssociation_WhenAssociationExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);
        var equipment = await CreateEquipmentAsync(scope.ServiceProvider);
        var roomEquipment = await CreateRoomEquipmentAsync(
            scope.ServiceProvider,
            room.Id,
            equipment.Id);

        var result = await repository.GetByRoomAndEquipmentAsync(
            room.Id,
            equipment.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(roomEquipment.Id);
        result.RoomId.Should().Be(room.Id);
        result.EquipmentId.Should().Be(equipment.Id);
    }

    [Fact]
    public async Task ExistsByRoomAndEquipmentAsync_ReturnsFalse_WhenAssociationDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var exists = await repository.ExistsByRoomAndEquipmentAsync(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByRoomAndEquipmentAsync_ReturnsTrue_WhenAssociationExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);
        var equipment = await CreateEquipmentAsync(scope.ServiceProvider);
        await CreateRoomEquipmentAsync(
            scope.ServiceProvider,
            room.Id,
            equipment.Id);

        var exists = await repository.ExistsByRoomAndEquipmentAsync(
            room.Id,
            equipment.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task GetByRoomIdAsync_ReturnsOnlyAssociationsForRequestedRoom()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var room1 = await CreateRoomAsync(scope.ServiceProvider);
        var room2 = await CreateRoomAsync(scope.ServiceProvider);
        var equipment1 = await CreateEquipmentAsync(scope.ServiceProvider);
        var equipment2 = await CreateEquipmentAsync(scope.ServiceProvider);

        var association1 = await CreateRoomEquipmentAsync(
            scope.ServiceProvider,
            room1.Id,
            equipment1.Id);

        var association2 = await CreateRoomEquipmentAsync(
            scope.ServiceProvider,
            room2.Id,
            equipment2.Id);

        var result = await repository.GetByRoomIdAsync(room1.Id);

        result.Should().ContainSingle(item => item.Id == association1.Id);
        result.Should().NotContain(item => item.Id == association2.Id);
    }

    [Fact]
    public async Task Find_ReturnsPagedResults_WhenFilteredByRoomId()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);
        var equipment = await CreateEquipmentAsync(scope.ServiceProvider);
        await CreateRoomEquipmentAsync(
            scope.ServiceProvider,
            room.Id,
            equipment.Id,
            quantity: 5,
            notes: "Test notes");

        var filter = new FilterRoomEquipmentDto
        {
            RoomId = room.Id,
            Page = 1,
            PageSize = 10
        };

        var page = await repository.Find(filter);

        page.Should().NotBeNull();
        page.TotalCount.Should().Be(1);
    }

    private static IRoomEquipmentRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IRoomEquipmentRepository>();
    }
}