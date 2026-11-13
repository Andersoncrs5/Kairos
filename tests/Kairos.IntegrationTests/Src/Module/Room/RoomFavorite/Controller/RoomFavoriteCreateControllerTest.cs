using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairos.IntegrationTests.Module.Room.RoomFavorite.Controller;

[Collection("Integration Tests")]
public sealed class RoomFavoriteCreateControllerTest : BaseIntegrationTest
{
    private const string BaseUrl = "/api/v1/rooms";
    private const string InboxHeader = "X-Inbox-Id";

    private readonly KairosApiFactory _factory;

    public RoomFavoriteCreateControllerTest(KairosApiFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsOk_AndPersistsFavoriteAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        RoomModel room;

        using (var scope = CreateScope())
        {
            room = await CreateRoomAsync(scope.ServiceProvider);
        }

        string userId = GetUserIdFromToken(tokens.Token);
        string inboxId = Guid.NewGuid().ToString();

        using var client = _factory.CreateClient();

        // Act
        using var response = await SendAddAsync(
            client,
            room.Id,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomFavoriteDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().NotBeNull();

        // Verify database persistence.
        using var verificationScope = CreateScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var favorite = await db.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.RoomId == room.Id &&
                x.UserId == userId);

        favorite.Should().NotBeNull();
        favorite!.Id.Should().NotBeNullOrWhiteSpace();
        favorite.RoomId.Should().Be(room.Id);
        favorite.UserId.Should().Be(userId);
        favorite.Version.Should().Be(1);
        favorite.CreatedAt.Should().NotBe(default);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Idempotency
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsOk_AndDoesNotDuplicate_WhenAlreadyFavorited()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        RoomModel room;

        using (var scope = CreateScope())
        {
            room = await CreateRoomAsync(scope.ServiceProvider);
        }

        string userId = GetUserIdFromToken(tokens.Token);

        string firstInboxId = Guid.NewGuid().ToString();
        string secondInboxId = Guid.NewGuid().ToString();

        using var client = _factory.CreateClient();

        // Act - First request
        using var firstResponse = await SendAddAsync(
            client,
            room.Id,
            firstInboxId,
            tokens.Token);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstHttp = await ReadAsync<RoomFavoriteDto>(firstResponse);

        firstHttp.Should().NotBeNull();
        firstHttp!.Success.Should().BeTrue();
        firstHttp.Data.Should().NotBeNull();

        // Act - Second request with a different Inbox ID
        using var secondResponse = await SendAddAsync(
            client,
            room.Id,
            secondInboxId,
            tokens.Token);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondHttp = await ReadAsync<RoomFavoriteDto>(secondResponse);

        secondHttp.Should().NotBeNull();
        secondHttp!.Success.Should().BeTrue();
        secondHttp.Data.Should().NotBeNull();

        // Assert - Only one favorite must exist.
        using var verificationScope = CreateScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var favorites = await db.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .Where(x =>
                x.RoomId == room.Id &&
                x.UserId == userId)
            .ToListAsync();

        favorites.Should().ContainSingle();

        await AssertInboxProcessedAsync(firstInboxId);
        await AssertInboxProcessedAsync(secondInboxId);
    }

    // ---------------------------------------------------------------
    // Room validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenRoomIdIsNotUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        using var client = _factory.CreateClient();

        // Act
        using var response = await SendAddAsync(
            client,
            "invalid-uuid",
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomFavoriteDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room ID must be a valid UUID.");
    }

    [Fact]
    public async Task Add_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string roomId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        using var client = _factory.CreateClient();

        // Act
        using var response = await SendAddAsync(
            client,
            roomId,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomFavoriteDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room doesn't exist.");

        using var verificationScope = CreateScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .AnyAsync(x => x.RoomId == roomId);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenRoomIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        RoomModel room;

        using (var scope = CreateScope())
        {
            room = await CreateRoomAsync(scope.ServiceProvider);

            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            await db.Set<RoomModel>()
                .Where(x => x.Id == room.Id)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(x => x.IsActive, false));
        }

        string inboxId = Guid.NewGuid().ToString();

        using var client = _factory.CreateClient();

        // Act
        using var response = await SendAddAsync(
            client,
            room.Id,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomFavoriteDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room is not active.");

        using var verificationScope = CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await verificationDb.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .AnyAsync(x => x.RoomId == room.Id);

        persisted.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Authentication and Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var client = _factory.CreateClient();

        // Act
        using var response = await SendAddAsync(
            client,
            Guid.NewGuid().ToString(),
            inboxId: null,
            accessToken: tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Add_ReturnsUnauthorized_WhenJwtIsMissing()
    {
        // Arrange
        string roomId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        using var client = _factory.CreateClient();

        // Act
        using var response = await SendAddAsync(
            client,
            roomId,
            inboxId,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static async Task<HttpResponseMessage> SendAddAsync(
        HttpClient client,
        string roomId,
        string? inboxId,
        string? accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{BaseUrl}/{Uri.EscapeDataString(roomId)}/favorite");

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.TryAddWithoutValidation(
                InboxHeader,
                inboxId);
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);
        }

        return await client.SendAsync(request);
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
