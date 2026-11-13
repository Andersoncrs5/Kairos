
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomFavorite.Controller;

[Collection("Integration Tests")]
public sealed class RoomFavoriteRemoveControllerTest : BaseIntegrationTest
{
    private const string FavoriteUrl = "/api/v1/rooms";
    private const string InboxHeader = "X-Inbox-Id";

    private readonly ITestOutputHelper _output;

    public RoomFavoriteRemoveControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task Remove_ReturnsOk_AndDeletesFavoriteAndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string userId = GetUserIdFromToken(tokens.Token);

        await AddFavoriteAsync(room.Id, tokens.Token);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendRemoveAsync(
            room.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeTrue();
        http.Message.Should().Contain("no longer a favorite");

        // Verify physical deletion of the favorite.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .AnyAsync(favorite =>
                favorite.RoomId == room.Id &&
                favorite.UserId == userId);

        persisted.Should().BeFalse();

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Idempotency
    // ---------------------------------------------------------------

    [Fact]
    public async Task Remove_ReturnsFalse_WhenRoomIsNotFavorite()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string userId = GetUserIdFromToken(tokens.Token);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendRemoveAsync(
            room.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
        http.Message.Should().Contain("no longer a favorite");

        // Ensure that no favorite was created.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomFavoriteModel>()
            .AsNoTracking()
            .AnyAsync(favorite =>
                favorite.RoomId == room.Id &&
                favorite.UserId == userId);

        persisted.Should().BeFalse();

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Remove_ReturnsFalse_WhenFavoriteWasAlreadyRemoved()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        await AddFavoriteAsync(room.Id, tokens.Token);

        string firstInboxId = Guid.NewGuid().ToString();
        string secondInboxId = Guid.NewGuid().ToString();

        // Act - First removal
        using (var firstResponse = await SendRemoveAsync(
                   room.Id,
                   tokens.Token,
                   firstInboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Data.Should().BeTrue();
        }

        // Act - Second removal with a different Inbox ID
        using var response = await SendRemoveAsync(
            room.Id,
            tokens.Token,
            secondInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();

        await AssertInboxProcessedAsync(firstInboxId);
        await AssertInboxProcessedAsync(secondInboxId);
    }

    // ---------------------------------------------------------------
    // Business rules
    // ---------------------------------------------------------------

    [Fact]
    public async Task Remove_ReturnsOk_WhenRoomIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string userId = GetUserIdFromToken(tokens.Token);

        await AddFavoriteAsync(room.Id, tokens.Token);

        // Deactivate the room after creating the favorite.
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            int affected = await db.Set<RoomModel>()
                .Where(x => x.Id == room.Id)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(x => x.IsActive, false));

            affected.Should().Be(1);
        }

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendRemoveAsync(
            room.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();

        // The inactive room must remain in the database.
        // Only its favorite relationship should be removed.
        using var verificationScope = CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persistedRoom = await verificationDb.Set<RoomModel>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == room.Id);

        persistedRoom.IsActive.Should().BeFalse();

        var favoriteExists = await verificationDb
            .Set<RoomFavoriteModel>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.RoomId == room.Id &&
                x.UserId == userId);

        favoriteExists.Should().BeFalse();

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Identifier validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Remove_ReturnsBadRequest_WhenRoomIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendRemoveAsync(
            "invalid-uuid",
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task Remove_ReturnsForbidden_WhenUserLacksDeletePermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var room = await CreateRoomHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendRemoveAsync(
            room.Id,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Remove_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        string roomId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendRemoveAsync(
            roomId,
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Remove_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string userId = GetUserIdFromToken(tokens.Token);

        await AddFavoriteAsync(room.Id, tokens.Token);

        // Act
        using var response = await SendRemoveAsync(
            room.Id,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        // The favorite must remain because the request was rejected.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomFavoriteModel>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.RoomId == room.Id &&
                x.UserId == userId);

        persisted.Should().BeTrue();
    }

    [Fact]
    public async Task Remove_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string userId = GetUserIdFromToken(tokens.Token);

        await AddFavoriteAsync(room.Id, tokens.Token);

        string inboxId = Guid.NewGuid().ToString();

        // First removal must succeed.
        using (var firstResponse = await SendRemoveAsync(
                   room.Id,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().BeTrue();
        }

        // Reusing the same Inbox ID must be rejected.
        using var response = await SendRemoveAsync(
            room.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        // The favorite remains removed.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomFavoriteModel>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.RoomId == room.Id &&
                x.UserId == userId);

        persisted.Should().BeFalse();

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task AddFavoriteAsync(
        string roomId,
        string accessToken)
    {
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFavoriteAsync(
            HttpMethod.Put,
            roomId,
            accessToken,
            inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();

        await AssertInboxProcessedAsync(inboxId);
    }

    private Task<HttpResponseMessage> SendRemoveAsync(
        string roomId,
        string? accessToken,
        string? inboxId)
    {
        return SendFavoriteAsync(
            HttpMethod.Delete,
            roomId,
            accessToken,
            inboxId);
    }

    private async Task<HttpResponseMessage> SendFavoriteAsync(
        HttpMethod method,
        string roomId,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            method,
            $"{FavoriteUrl}/{Uri.EscapeDataString(roomId)}/favorite");

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);
        }

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.TryAddWithoutValidation(
                InboxHeader,
                inboxId);
        }

        return await Client.SendAsync(request);
    }

    private static string GetUserIdFromToken(string accessToken)
    {
        JwtSecurityToken jwt =
            new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        string? userId =
            jwt.Claims.FirstOrDefault(x =>
                x.Type == JwtRegisteredClaimNames.Sid)?.Value
            ?? jwt.Claims.FirstOrDefault(x =>
                x.Type == JwtRegisteredClaimNames.Sub)?.Value;

        return userId
            ?? throw new InvalidOperationException(
                "The JWT does not contain the authenticated user ID.");
    }
}
