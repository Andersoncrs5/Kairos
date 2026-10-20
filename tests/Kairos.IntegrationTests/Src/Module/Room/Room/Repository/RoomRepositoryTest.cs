using FluentAssertions;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairos.IntegrationTests.Module.Room.Room.Repository;

[Collection("Integration Tests")]
public sealed class RoomRepositoryTest : BaseIntegrationTest
{
    public RoomRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetByRoomTypeIdAsync_ReturnsRoomsOfRequestedType()
    {
        using var scope = CreateScope();
        var repository = GetRoomRepository(scope);

        var expectedRoom = await CreateRoomAsync(scope.ServiceProvider);
        var otherRoom = await CreateRoomAsync(scope.ServiceProvider);

        var result = await repository.GetByRoomTypeIdAsync(
            expectedRoom.RoomTypeId);

        result.Should().Contain(room => room.Id == expectedRoom.Id);
        result.Should().NotContain(room => room.Id == otherRoom.Id);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsActiveRooms()
    {
        using var scope = CreateScope();
        var repository = GetRoomRepository(scope);

        var activeRoom = await CreateRoomAsync(scope.ServiceProvider);

        var inactiveRoom = await CreateRoomAsync(
            scope.ServiceProvider,
            room => room.IsActive = false);

        var result = await repository.GetActiveAsync();

        result.Should().Contain(room => room.Id == activeRoom.Id);
        result.Should().NotContain(room => room.Id == inactiveRoom.Id);
        result.Should().OnlyContain(room => room.IsActive);
    }

    [Fact]
    public async Task GetActiveByRoomTypeIdAsync_ReturnsOnlyActiveRoomsOfRequestedType()
    {
        using var scope = CreateScope();
        var repository = GetRoomRepository(scope);

        var activeRoom = await CreateRoomAsync(scope.ServiceProvider);

        var inactiveRoom = await CreateRoomAsync(
            scope.ServiceProvider,
            room =>
            {
                room.RoomTypeId = activeRoom.RoomTypeId;
                room.IsActive = false;
            });

        var otherRoom = await CreateRoomAsync(scope.ServiceProvider);

        var result = await repository.GetActiveByRoomTypeIdAsync(
            activeRoom.RoomTypeId);

        result.Should().Contain(room => room.Id == activeRoom.Id);
        result.Should().NotContain(room => room.Id == inactiveRoom.Id);
        result.Should().NotContain(room => room.Id == otherRoom.Id);
        result.Should().OnlyContain(room =>
            room.IsActive &&
            room.RoomTypeId == activeRoom.RoomTypeId);
    }

    private static IRoomRepository GetRoomRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IRoomRepository>();
    }
}