
using FluentAssertions;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairos.IntegrationTests.Module.Room.RoomFavorite.Repository;

[Collection("Integration Tests")]
public sealed class RoomFavoriteRepositoryTest : BaseIntegrationTest
{
    public RoomFavoriteRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    // ---------------------------------------------------------------
    // GetByUserAndRoomAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByUserAndRoomAsync_ReturnsNull_WhenFavoriteDoesNotExist()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);

        IRoomFavoriteRepository repository = GetRepository(scope);

        RoomFavoriteModel? result = await repository.GetByUserAndRoomAsync(
            user.Id,
            room.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserAndRoomAsync_ReturnsFavorite_WhenFavoriteExists()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);

        RoomFavoriteModel favorite = await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            room.Id);

        IRoomFavoriteRepository repository = GetRepository(scope);

        RoomFavoriteModel? result = await repository.GetByUserAndRoomAsync(
            user.Id,
            room.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(favorite.Id);
        result.UserId.Should().Be(user.Id);
        result.RoomId.Should().Be(room.Id);
    }

    // ---------------------------------------------------------------
    // ExistsByUserAndRoomAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByUserAndRoomAsync_ReturnsFalse_WhenFavoriteDoesNotExist()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);

        IRoomFavoriteRepository repository = GetRepository(scope);

        bool result = await repository.ExistsByUserAndRoomAsync(
            user.Id,
            room.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByUserAndRoomAsync_ReturnsTrue_WhenFavoriteExists()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            room.Id);

        IRoomFavoriteRepository repository = GetRepository(scope);

        bool result = await repository.ExistsByUserAndRoomAsync(
            user.Id,
            room.Id);

        result.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // DeleteByUserAndRoomAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteByUserAndRoomAsync_ReturnsOne_WhenFavoriteExists()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            room.Id);

        IRoomFavoriteRepository repository = GetRepository(scope);

        int result = await repository.DeleteByUserAndRoomAsync(
            user.Id,
            room.Id);

        result.Should().Be(1);

        RoomFavoriteModel? favorite = await repository.GetByUserAndRoomAsync(
            user.Id,
            room.Id);

        favorite.Should().BeNull();
    }

    [Fact]
    public async Task DeleteByUserAndRoomAsync_ReturnsZero_WhenFavoriteDoesNotExist()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);

        IRoomFavoriteRepository repository = GetRepository(scope);

        int result = await repository.DeleteByUserAndRoomAsync(
            user.Id,
            room.Id);

        result.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // CountByUserAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task CountByUserAsync_ReturnsZero_WhenUserHasNoFavorites()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);

        IRoomFavoriteRepository repository = GetRepository(scope);

        int result = await repository.CountByUserAsync(user.Id);

        result.Should().Be(0);
    }

    [Fact]
    public async Task CountByUserAsync_ReturnsOnlyFavoritesForRequestedUser()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        UserModel anotherUser = await CreateUserAsync(scope.ServiceProvider);

        RoomModel firstRoom = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel secondRoom = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel thirdRoom = await CreateRoomAsync(scope.ServiceProvider);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            firstRoom.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            secondRoom.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            anotherUser.Id,
            thirdRoom.Id);

        IRoomFavoriteRepository repository = GetRepository(scope);

        int result = await repository.CountByUserAsync(user.Id);

        result.Should().Be(2);
    }

    // ---------------------------------------------------------------
    // CountByRoomAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task CountByRoomAsync_ReturnsZero_WhenRoomHasNoFavorites()
    {
        using IServiceScope scope = CreateScope();

        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);

        IRoomFavoriteRepository repository = GetRepository(scope);

        int result = await repository.CountByRoomAsync(room.Id);

        result.Should().Be(0);
    }

    [Fact]
    public async Task CountByRoomAsync_ReturnsOnlyFavoritesForRequestedRoom()
    {
        using IServiceScope scope = CreateScope();

        UserModel firstUser = await CreateUserAsync(scope.ServiceProvider);
        UserModel secondUser = await CreateUserAsync(scope.ServiceProvider);
        UserModel thirdUser = await CreateUserAsync(scope.ServiceProvider);

        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel anotherRoom = await CreateRoomAsync(scope.ServiceProvider);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            firstUser.Id,
            room.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            secondUser.Id,
            room.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            thirdUser.Id,
            anotherRoom.Id);

        IRoomFavoriteRepository repository = GetRepository(scope);

        int result = await repository.CountByRoomAsync(room.Id);

        result.Should().Be(2);
    }

    // ---------------------------------------------------------------
    // GetFavoriteRoomIdsAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetFavoriteRoomIdsAsync_ReturnsEmptySet_WhenRoomIdsAreEmpty()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);

        IRoomFavoriteRepository repository = GetRepository(scope);

        IReadOnlySet<string> result = await repository.GetFavoriteRoomIdsAsync(
            user.Id,
            Array.Empty<string>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFavoriteRoomIdsAsync_ReturnsOnlyFavoriteIdsWithinRequestedRooms()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        UserModel anotherUser = await CreateUserAsync(scope.ServiceProvider);

        RoomModel firstRoom = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel secondRoom = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel thirdRoom = await CreateRoomAsync(scope.ServiceProvider);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            firstRoom.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            secondRoom.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            anotherUser.Id,
            thirdRoom.Id);

        IRoomFavoriteRepository repository = GetRepository(scope);

        IReadOnlySet<string> result = await repository.GetFavoriteRoomIdsAsync(
            user.Id,
            new[]
            {
                firstRoom.Id,
                secondRoom.Id,
                thirdRoom.Id
            });

        result.Should().HaveCount(2);
        result.Should().Contain(firstRoom.Id);
        result.Should().Contain(secondRoom.Id);
        result.Should().NotContain(thirdRoom.Id);
    }

    [Fact]
    public async Task GetFavoriteRoomIdsAsync_ReturnsEmptySet_WhenUserHasNoFavoritesInRequestedRooms()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);

        IRoomFavoriteRepository repository = GetRepository(scope);

        IReadOnlySet<string> result = await repository.GetFavoriteRoomIdsAsync(
            user.Id,
            new[] { room.Id });

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Find
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsPagedResults_WhenFilteredByRoomId()
    {
        using IServiceScope scope = CreateScope();

        UserModel firstUser = await CreateUserAsync(scope.ServiceProvider);
        UserModel secondUser = await CreateUserAsync(scope.ServiceProvider);

        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel anotherRoom = await CreateRoomAsync(scope.ServiceProvider);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            firstUser.Id,
            room.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            secondUser.Id,
            room.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            firstUser.Id,
            anotherRoom.Id);

        IRoomFavoriteRepository repository = GetRepository(scope);

        FilterRoomFavoriteDto filter = new()
        {
            RoomId = room.Id,
            Page = 1,
            PageSize = 10
        };

        var result = await repository.Find(filter);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Find_ReturnsPagedResults_WhenFilteredByUserId()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        UserModel anotherUser = await CreateUserAsync(scope.ServiceProvider);

        RoomModel firstRoom = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel secondRoom = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel thirdRoom = await CreateRoomAsync(scope.ServiceProvider);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            firstRoom.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            secondRoom.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            anotherUser.Id,
            thirdRoom.Id);

        IRoomFavoriteRepository repository = GetRepository(scope);

        FilterRoomFavoriteDto filter = new()
        {
            UserId = user.Id,
            Page = 1,
            PageSize = 10
        };

        var result = await repository.Find(filter);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Find_ReturnsEmptyPage_WhenNoFavoriteMatchesFilter()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);
        RoomModel room = await CreateRoomAsync(scope.ServiceProvider);

        IRoomFavoriteRepository repository = GetRepository(scope);

        FilterRoomFavoriteDto filter = new()
        {
            UserId = user.Id,
            RoomId = room.Id,
            Page = 1,
            PageSize = 10
        };

        var result = await repository.Find(filter);

        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Find_ReturnsPagedResults_WhenPageSizeIsApplied()
    {
        using IServiceScope scope = CreateScope();

        UserModel user = await CreateUserAsync(scope.ServiceProvider);

        RoomModel firstRoom = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel secondRoom = await CreateRoomAsync(scope.ServiceProvider);
        RoomModel thirdRoom = await CreateRoomAsync(scope.ServiceProvider);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            firstRoom.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            secondRoom.Id);

        await CreateFavoriteAsync(
            scope.ServiceProvider,
            user.Id,
            thirdRoom.Id);

        IRoomFavoriteRepository repository = GetRepository(scope);

        FilterRoomFavoriteDto filter = new()
        {
            UserId = user.Id,
            Page = 1,
            PageSize = 2
        };

        var result = await repository.Find(filter);

        result.TotalCount.Should().Be(3);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static IRoomFavoriteRepository GetRepository(IServiceScope scope)
    {
        return scope.ServiceProvider.GetRequiredService<IRoomFavoriteRepository>();
    }

    private static async Task<RoomFavoriteModel> CreateFavoriteAsync(
        IServiceProvider services,
        string userId,
        string roomId)
    {
        AppDbContext context = services.GetRequiredService<AppDbContext>();

        RoomFavoriteModel favorite = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            RoomId = roomId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await context.Set<RoomFavoriteModel>().AddAsync(favorite);
        await context.SaveChangesAsync();

        return favorite;
    }
}
