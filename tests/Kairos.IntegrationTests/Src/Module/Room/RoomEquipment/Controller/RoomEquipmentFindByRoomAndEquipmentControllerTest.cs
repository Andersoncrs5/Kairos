
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomEquipment.Controller;

[Collection("Integration Tests")]
public sealed class RoomEquipmentFindByRoomAndEquipmentControllerTest
    : BaseIntegrationTest
{
    private const string FindUrl =
        "/api/v1/room-equipments/rooms";

    private readonly ITestOutputHelper _output;

    public RoomEquipmentFindByRoomAndEquipmentControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Find
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsOk_WhenRoomEquipmentAssociationExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        var association = await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindAsync(
            room.Id,
            equipment.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Message.Should().Be("Room equipment association found");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(association.Id);
        http.Data.RoomId.Should().Be(room.Id);
        http.Data.EquipmentId.Should().Be(equipment.Id);
        http.Data.Quantity.Should().Be(association.Quantity);
        http.Data.Notes.Should().Be(association.Notes);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsNotFound_WhenRoomEquipmentAssociationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // The room and equipment exist, but no association is created.

        // Act
        using var response = await SendFindAsync(
            room.Id,
            equipment.Id,
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

    [Fact]
    public async Task Find_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        string nonexistentRoomId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindAsync(
            nonexistentRoomId,
            equipment.Id,
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

    [Fact]
    public async Task Find_ReturnsNotFound_WhenEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string nonexistentEquipmentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindAsync(
            room.Id,
            nonexistentEquipmentId,
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
    // Room ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsBadRequest_WhenRoomIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        const string invalidRoomId = "invalid-uuid";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindAsync(
            invalidRoomId,
            equipment.Id,
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
            "Room ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Equipment ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsBadRequest_WhenEquipmentIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        const string invalidEquipmentId = "invalid-uuid";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindAsync(
            room.Id,
            invalidEquipmentId,
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
            "Equipment ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Inbox validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        // Act
        using var response = await SendFindAsync(
            room.Id,
            equipment.Id,
            inboxId: null,
            accessToken: tokens.Token);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain(InboxHeader);
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        await CreateRoomEquipmentHttp(
            tokens.Token,
            room.Id,
            equipment.Id);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindAsync(
            room.Id,
            equipment.Id,
            inboxId,
            accessToken: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendFindAsync(
        string roomId,
        string equipmentId,
        string? inboxId,
        string? accessToken)
    {
        string url =
            $"{FindUrl}/{Uri.EscapeDataString(roomId)}" +
            $"/equipments/{Uri.EscapeDataString(equipmentId)}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            url);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (inboxId is not null)
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }
}
