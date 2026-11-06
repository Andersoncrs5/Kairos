
using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomEquipment.Controller;

[Collection("Integration Tests")]
public sealed class RoomEquipmentUpdateControllerTest : BaseIntegrationTest
{
    private const string UpdateUrl = "/api/v1/room-equipments";

    private readonly ITestOutputHelper _output;

    public RoomEquipmentUpdateControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Successful updates
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsOk_AndPersistsChanges_AndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var roomEquipment = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = 5,
            Notes = "Updated room equipment association."
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            roomEquipment.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Message.Should().Be(
            "Room equipment association updated successfully");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(roomEquipment.Id);
        http.Data.RoomId.Should().Be(room.Id);
        http.Data.EquipmentId.Should().Be(equipment.Id);
        http.Data.Quantity.Should().Be(dto.Quantity);
        http.Data.Notes.Should().Be(dto.Notes);

        // Database persistence
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == roomEquipment.Id);

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(roomEquipment.Id);
        persisted.RoomId.Should().Be(room.Id);
        persisted.EquipmentId.Should().Be(equipment.Id);
        persisted.Quantity.Should().Be(dto.Quantity);
        persisted.Notes.Should().Be(dto.Notes);
        persisted.Version.Should().BeGreaterThan(1);
        persisted.UpdatedAt.Should().NotBeNull();

        await AssertRoomEquipmentInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenOnlyQuantityIsProvided()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var roomEquipment = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        const int updatedQuantity = 7;

        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = updatedQuantity
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            roomEquipment.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(roomEquipment.Id);
        http.Data.RoomId.Should().Be(room.Id);
        http.Data.EquipmentId.Should().Be(equipment.Id);
        http.Data.Quantity.Should().Be(updatedQuantity);

        // Notes should remain unchanged.
        http.Data.Notes.Should().Be(roomEquipment.Notes);

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == roomEquipment.Id);

        persisted.Quantity.Should().Be(updatedQuantity);
        persisted.Notes.Should().Be(roomEquipment.Notes);
        persisted.Version.Should().BeGreaterThan(1);
        persisted.UpdatedAt.Should().NotBeNull();

        await AssertRoomEquipmentInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenRoomEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string inboxId = Guid.NewGuid().ToString();
        string nonexistentId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = 5,
            Notes = "Updated notes."
        };

        // Act
        using var response = await SendUpdateAsync(
            nonexistentId,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeNull();
        http.Errors.Should().Contain(
            "Room equipment association doesn't exist.");
    }

    // ---------------------------------------------------------------
    // ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string inboxId = Guid.NewGuid().ToString();
        const string invalidId = "not-a-valid-uuid";

        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = 5
        };

        // Act
        using var response = await SendUpdateAsync(
            invalidId,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeNull();
        http.Errors.Should().Contain(
            "Room equipment association ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Inbox validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var roomEquipment = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = 5
        };

        // Act
        using var response = await SendUpdateAsync(
            roomEquipment.Id,
            dto,
            inboxId: null,
            accessToken: tokens.Token);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain(InboxHeader);

        // The original association must remain unchanged.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == roomEquipment.Id);

        persisted.Quantity.Should().Be(roomEquipment.Quantity);
        persisted.Notes.Should().Be(roomEquipment.Notes);
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenAccessTokenIsMissing()
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

        var dto = new UpdateRoomEquipmentDto
        {
            Quantity = 5
        };

        // Act
        using var response = await SendUpdateAsync(
            roomEquipment.Id,
            dto,
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
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
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

        var firstDto = new UpdateRoomEquipmentDto
        {
            Quantity = 5,
            Notes = "First association updated."
        };

        var secondDto = new UpdateRoomEquipmentDto
        {
            Quantity = 10,
            Notes = "Second association updated."
        };

        // First update
        using (var firstResponse = await SendUpdateAsync(
            firstAssociation.Id,
            firstDto,
            inboxId,
            tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Second update reuses the same Inbox ID.
        using var response = await SendUpdateAsync(
            secondAssociation.Id,
            secondDto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persistedFirst = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == firstAssociation.Id);

        var persistedSecond = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == secondAssociation.Id);

        persistedFirst.Quantity.Should().Be(firstDto.Quantity);
        persistedFirst.Notes.Should().Be(firstDto.Notes);

        // The second update must not be applied.
        persistedSecond.Quantity.Should().Be(secondAssociation.Quantity);
        persistedSecond.Notes.Should().Be(secondAssociation.Notes);

        await AssertRoomEquipmentInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendUpdateAsync(
        string roomEquipmentId,
        UpdateRoomEquipmentDto dto,
        string? inboxId,
        string? accessToken)
    {
        string url =
            $"{UpdateUrl}/{Uri.EscapeDataString(roomEquipmentId)}";

        return SendAsyncWithMethod(
            url,
            method: HttpMethod.Put,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
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

    private async Task AssertRoomEquipmentInboxProcessedAsync(string inboxId)
    {
        var inbox = await GetInboxByMessageIdAsync(inboxId);

        inbox.Should().NotBeNull();
        inbox.MessageId.Should().Be(inboxId);
        inbox.Status.Should().Be(InboxStatusEnum.Processed);
    }
}
