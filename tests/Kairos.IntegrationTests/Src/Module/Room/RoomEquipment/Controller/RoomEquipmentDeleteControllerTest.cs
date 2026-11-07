using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomEquipment.Controller;

[Collection("Integration Tests")]
public sealed class RoomEquipmentDeleteControllerTest : BaseIntegrationTest
{
    private const string DeleteUrl = "/api/v1/room-equipments";

    private readonly ITestOutputHelper _output;

    public RoomEquipmentDeleteControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsOk_AndDeletesRoomEquipmentAndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var roomEquipment = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            roomEquipment.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeTrue();

        await AssertRoomEquipmentDoesNotExistAsync(roomEquipment.Id);
        await AssertRoomEquipmentInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenRoomEquipmentIdIsInvalidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        const string invalidId = "invalid-uuid";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            invalidId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenRoomEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            nonexistentId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AssertRoomEquipmentDoesNotExistAsync(nonexistentId);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenRoomEquipmentWasAlreadyDeleted()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var roomEquipment = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        string firstInboxId = Guid.NewGuid().ToString();

        // First deletion
        using (var firstResponse = await SendDeleteAsync(
            roomEquipment.Id,
            firstInboxId,
            tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Second deletion
        string secondInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            roomEquipment.Id,
            secondInboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        await AssertRoomEquipmentDoesNotExistAsync(roomEquipment.Id);
        await AssertRoomEquipmentInboxProcessedAsync(firstInboxId);
    }

    // ---------------------------------------------------------------
    // Inbox validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var roomEquipment = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        // Act
        using var response = await SendDeleteAsync(
            roomEquipment.Id,
            inboxId: null,
            accessToken: tokens.Token);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain(InboxHeader);

        await AssertRoomEquipmentExistsAsync(roomEquipment.Id);
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var roomEquipment = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            roomEquipment.Id,
            inboxId,
            accessToken: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertRoomEquipmentExistsAsync(roomEquipment.Id);
        await AssertInboxNotCreatedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Inbox idempotency
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var firstEquipment = await CreateEquipmentHttp(tokens);
        var secondEquipment = await CreateEquipmentHttp(tokens);

        var firstAssociation = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            firstEquipment.Id);

        var secondAssociation = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            secondEquipment.Id);

        string inboxId = Guid.NewGuid().ToString();

        // First deletion
        using (var firstResponse = await SendDeleteAsync(
            firstAssociation.Id,
            inboxId,
            tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Second request reuses the same Inbox ID
        using var response = await SendDeleteAsync(
            secondAssociation.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertRoomEquipmentDoesNotExistAsync(firstAssociation.Id);
        await AssertRoomEquipmentExistsAsync(secondAssociation.Id);

        await AssertRoomEquipmentInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendDeleteAsync(
        string roomEquipmentId,
        string? inboxId,
        string? accessToken)
    {
        string url =
            $"{DeleteUrl}/{Uri.EscapeDataString(roomEquipmentId)}";

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            url);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return Client.SendAsync(request);
    }

    
    private async Task AssertRoomEquipmentExistsAsync(string id)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        bool exists = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == id);

        exists.Should().BeTrue();
    }

    private async Task AssertRoomEquipmentDoesNotExistAsync(string id)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        bool exists = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == id);

        exists.Should().BeFalse();
    }

    private async Task AssertRoomEquipmentInboxProcessedAsync(string inboxId)
    {
        var inbox = await GetInboxByMessageIdAsync(inboxId);

        inbox.Should().NotBeNull();
        inbox.MessageId.Should().Be(inboxId);
        inbox.Status.Should().Be(InboxStatusEnum.Processed);
    }
}
