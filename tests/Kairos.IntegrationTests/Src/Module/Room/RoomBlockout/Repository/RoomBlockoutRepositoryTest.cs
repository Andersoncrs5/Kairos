
using FluentAssertions;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.IntegrationTests.Module.RoomBlockout.Repository;

[Collection("Integration Tests")]
public sealed class RoomBlockoutRepositoryTest : BaseIntegrationTest
{
    public RoomBlockoutRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetByRoomIdAsync_ReturnsOnlyBlockoutsForRequestedRoom()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);
        var room1 = await CreateRoomAsync(scope.ServiceProvider);
        var room2 = await CreateRoomAsync(scope.ServiceProvider);

        var blockout1 = await CreateRoomBlockoutAsync(
            scope.ServiceProvider,
            room1.Id,
            user.Id);

        var blockout2 = await CreateRoomBlockoutAsync(
            scope.ServiceProvider,
            room2.Id,
            user.Id);

        var result = await repository.GetByRoomIdAsync(room1.Id);

        result.Should().ContainSingle(item => item.Id == blockout1.Id);
        result.Should().NotContain(item => item.Id == blockout2.Id);
    }

    [Fact]
    public async Task GetOverlappingAsync_ReturnsOnlyOverlappingBlockouts()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);
        var room = await CreateRoomAsync(scope.ServiceProvider);

        var startsAt = DateTimeOffset.UtcNow.AddDays(5);

        var overlappingBlockout = await CreateRoomBlockoutAsync(
            scope.ServiceProvider,
            room.Id,
            user.Id,
            startsAt,
            startsAt.AddHours(1));

        var nonOverlappingBlockout = await CreateRoomBlockoutAsync(
            scope.ServiceProvider,
            room.Id,
            user.Id,
            startsAt.AddHours(2),
            startsAt.AddHours(3));

        var result = await repository.GetOverlappingAsync(
            room.Id,
            startsAt.AddMinutes(30),
            startsAt.AddHours(1).AddMinutes(30));

        result.Should().ContainSingle(
            item => item.Id == overlappingBlockout.Id);

        result.Should().NotContain(
            item => item.Id == nonOverlappingBlockout.Id);
    }

    [Fact]
    public async Task ExistsOverlappingAsync_ReturnsTrue_WhenBlockoutOverlaps()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);
        var room = await CreateRoomAsync(scope.ServiceProvider);

        var startsAt = DateTimeOffset.UtcNow.AddDays(5);

        await CreateRoomBlockoutAsync(
            scope.ServiceProvider,
            room.Id,
            user.Id,
            startsAt,
            startsAt.AddHours(1));

        var exists = await repository.ExistsOverlappingAsync(
            room.Id,
            startsAt.AddMinutes(15),
            startsAt.AddMinutes(45));

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsOverlappingAsync_ReturnsFalse_WhenNoBlockoutOverlaps()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);
        var room = await CreateRoomAsync(scope.ServiceProvider);

        var startsAt = DateTimeOffset.UtcNow.AddDays(5);

        await CreateRoomBlockoutAsync(
            scope.ServiceProvider,
            room.Id,
            user.Id,
            startsAt,
            startsAt.AddHours(1));

        var exists = await repository.ExistsOverlappingAsync(
            room.Id,
            startsAt.AddHours(1),
            startsAt.AddHours(2));

        exists.Should().BeFalse();
    }

    private static IRoomBlockoutRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IRoomBlockoutRepository>();
    }
}
