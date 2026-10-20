using FluentAssertions;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairos.IntegrationTests.Module.Room.RoomType.Repository;

[Collection("Integration Tests")]
public sealed class RoomTypeRepositoryTest : BaseIntegrationTest
{
    public RoomTypeRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenNameDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var name = $"Missing Room Type {Guid.NewGuid():N}";

        var result = await repository.GetByNameAsync(name);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsRoomType_WhenNameExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var roomType = await CreateRoomTypeAsync(scope.ServiceProvider);

        var result = await repository.GetByNameAsync(roomType.Name);

        result.Should().NotBeNull();
        result!.Id.Should().Be(roomType.Id);
        result.Name.Should().Be(roomType.Name);
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsFalse_WhenNameDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var exists = await repository.ExistsByNameAsync(
            $"Missing Room Type {Guid.NewGuid():N}");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsTrue_WhenNameExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var roomType = await CreateRoomTypeAsync(scope.ServiceProvider);

        var exists = await repository.ExistsByNameAsync(roomType.Name);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveRoomTypes()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var activeRoomType = await CreateRoomTypeAsync(
            scope.ServiceProvider);

        var inactiveRoomType = await CreateRoomTypeAsync(
            scope.ServiceProvider,
            roomType => roomType.IsActive = false);

        var result = await repository.GetActiveAsync();

        result.Should().Contain(roomType =>
            roomType.Id == activeRoomType.Id);

        result.Should().NotContain(roomType =>
            roomType.Id == inactiveRoomType.Id);
    }

    private static IRoomTypeRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IRoomTypeRepository>();
    }
}