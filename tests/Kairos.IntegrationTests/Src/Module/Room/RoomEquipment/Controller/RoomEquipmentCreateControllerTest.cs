
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
public sealed class RoomEquipmentCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/room-equipments/rooms";

    private readonly ITestOutputHelper _output;

    public RoomEquipmentCreateControllerTest(
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
    public async Task Create_ReturnsCreated_AndPersistsRoomEquipmentAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: room.Id,
            quantity: 3,
            notes: "Three monitors assigned to this room.");

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
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
            .SingleOrDefaultAsync(item => item.Id == http.Data.Id);

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(http.Data.Id);
        persisted.RoomId.Should().Be(room.Id);
        persisted.EquipmentId.Should().Be(equipment.Id);
        persisted.Quantity.Should().Be(dto.Quantity);
        persisted.Notes.Should().Be(dto.Notes);
        persisted.Version.Should().Be(1);
        persisted.CreatedAt.Should().NotBe(default);

        await AssertRoomEquipmentInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenNotesAreNull()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: room.Id,
            notes: null);

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.RoomId.Should().Be(room.Id);
        http.Data.EquipmentId.Should().Be(equipment.Id);
        http.Data.Quantity.Should().Be(dto.Quantity);
        http.Data.Notes.Should().BeNull();

        await AssertRoomEquipmentExistsAsync(http.Data.Id);
        await AssertRoomEquipmentInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Room validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRoomIdIsNotUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        const string invalidRoomId = "invalid-uuid";

        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: invalidRoomId);

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room ID must be a valid UUID.");

        await AssertNoRoomEquipmentExistsAsync(invalidRoomId);
    }

    [Fact]
    public async Task Create_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        string nonexistentRoomId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: nonexistentRoomId);

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room doesn't exist.");

        await AssertNoRoomEquipmentExistsAsync(nonexistentRoomId);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRoomIdIsNull()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: null!);

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Room ID is required.");

        await AssertInboxNotCreatedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Equipment validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsNotFound_WhenEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string nonexistentEquipmentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: nonexistentEquipmentId,
            roomId: room.Id);

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Equipment doesn't exist.");

        await AssertNoRoomEquipmentExistsAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenEquipmentIdIsNull()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: null!,
            roomId: room.Id);

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Equipment ID is required.");

        await AssertInboxNotCreatedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // DTO validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenQuantityIsLessThanOne()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: room.Id,
            quantity: 0);

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Quantity must be at least 1.");

        await AssertNoRoomEquipmentExistsAsync(room.Id);
        await AssertInboxNotCreatedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNotesExceed255Characters()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: room.Id,
            notes: new string('A', 256));

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Notes cannot exceed 255 characters.");

        await AssertNoRoomEquipmentExistsAsync(room.Id);
        await AssertInboxNotCreatedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Existing associations and conflicts
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConflict_WhenEquipmentIsAlreadyAssociatedWithRoom()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var firstDto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: room.Id,
            quantity: 2);

        string firstInboxId = Guid.NewGuid().ToString();

        // First association
        using (var firstResponse = await SendCreateAsync(
            firstDto,
            firstInboxId,
            tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Attempt to associate the same equipment again
        string secondInboxId = Guid.NewGuid().ToString();

        var secondDto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: room.Id,
            quantity: 5);

        // Act
        using var response = await SendCreateAsync(
            secondDto,
            secondInboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Equipment is already associated with this room.");

        await AssertRoomEquipmentCountAsync(
            room.Id,
            equipment.Id,
            expectedCount: 1);

        await AssertRoomEquipmentInboxProcessedAsync(firstInboxId);
    }

    // ---------------------------------------------------------------
    // Inbox and authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var dto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: room.Id);

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertNoRoomEquipmentExistsAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomEquipmentDto(
            equipmentId: equipment.Id,
            roomId: room.Id);

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertNoRoomEquipmentExistsAsync(room.Id);
        await AssertInboxNotCreatedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var firstEquipment = await CreateEquipmentHttp(tokens);
        var secondEquipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        var firstDto = NewRoomEquipmentDto(
            equipmentId: firstEquipment.Id,
            roomId: room.Id);

        var secondDto = NewRoomEquipmentDto(
            equipmentId: secondEquipment.Id,
            roomId: room.Id);

        // First association
        using (var firstResponse = await SendCreateAsync(
            firstDto,
            inboxId,
            tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Second request reusing the same Inbox ID
        using var response = await SendCreateAsync(
            secondDto,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var firstAssociation = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.RoomId == room.Id &&
                item.EquipmentId == firstEquipment.Id);

        var secondAssociation = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.RoomId == room.Id &&
                item.EquipmentId == secondEquipment.Id);

        firstAssociation.Should().NotBeNull();
        secondAssociation.Should().BeNull();

        await AssertRoomEquipmentCountAsync(
            room.Id,
            firstEquipment.Id,
            expectedCount: 1);

        await AssertRoomEquipmentCountAsync(
            room.Id,
            secondEquipment.Id,
            expectedCount: 0);

        await AssertRoomEquipmentInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        AddEquipmentToRoomDto dto,
        string? inboxId,
        string? accessToken)
    {
        return SendAsync(
            CreateUrl,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private static AddEquipmentToRoomDto NewRoomEquipmentDto(
        string equipmentId,
        string roomId,
        int quantity = 1,
        string? notes = "Created by integration tests.")
    {
        return new AddEquipmentToRoomDto
        {
            EquipmentId = equipmentId,
            RoomId = roomId,
            Quantity = quantity,
            Notes = notes
        };
    }

    private async Task AssertNoRoomEquipmentExistsAsync(string roomId)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        bool exists = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .AnyAsync(item => item.RoomId == roomId);

        exists.Should().BeFalse();
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

    private async Task AssertRoomEquipmentCountAsync(
        string roomId,
        string equipmentId,
        int expectedCount)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        int count = await db.Set<RoomEquipmentModel>()
            .AsNoTracking()
            .CountAsync(item =>
                item.RoomId == roomId &&
                item.EquipmentId == equipmentId);

        count.Should().Be(expectedCount);
    }

    private async Task AssertRoomEquipmentInboxProcessedAsync(string inboxId)
    {
        var inbox = await GetInboxByMessageIdAsync(inboxId);

        inbox.Should().NotBeNull();
        inbox.MessageId.Should().Be(inboxId);
        inbox.Status.Should().Be(InboxStatusEnum.Processed);
    }
}
