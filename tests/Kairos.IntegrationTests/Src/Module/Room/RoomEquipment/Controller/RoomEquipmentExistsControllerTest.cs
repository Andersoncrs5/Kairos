
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomEquipment.Controller;

[Collection("Integration Tests")]
public sealed class RoomEquipmentExistsControllerTest : BaseIntegrationTest
{
    private const string ExistsUrl =
        "/api/v1/room-equipments/exists/rooms";

    private readonly ITestOutputHelper _output;

    public RoomEquipmentExistsControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Exists
    // ---------------------------------------------------------------

    [Fact]
    public async Task Exists_ReturnsOkTrue_WhenEquipmentIsAssociatedWithRoom()
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
        using var response = await SendExistsAsync(
            room.Id,
            equipment.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().Be(true);
        http.Message.Should().Be(
            "Equipment is associated with this room");
    }

    [Fact]
    public async Task Exists_ReturnsOkFalse_WhenEquipmentIsNotAssociatedWithRoom()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var equipment = await CreateEquipmentHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // No association is created.

        // Act
        using var response = await SendExistsAsync(
            room.Id,
            equipment.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().Be(false);
        http.Message.Should().Be(
            "Equipment is not associated with this room");
    }

    [Fact]
    public async Task Exists_ReturnsOkFalse_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        string nonexistentRoomId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            nonexistentRoomId,
            equipment.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().Be(false);
        http.Message.Should().Be(
            "Equipment is not associated with this room");
    }

    [Fact]
    public async Task Exists_ReturnsOkFalse_WhenEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string nonexistentEquipmentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            room.Id,
            nonexistentEquipmentId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().Be(false);
        http.Message.Should().Be(
            "Equipment is not associated with this room");
    }

    // ---------------------------------------------------------------
    // Room ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Exists_ReturnsBadRequest_WhenRoomIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        const string invalidRoomId = "invalid-uuid";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            invalidRoomId,
            equipment.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<bool?>(response);

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
    public async Task Exists_ReturnsBadRequest_WhenEquipmentIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        const string invalidEquipmentId = "invalid-uuid";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            room.Id,
            invalidEquipmentId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<bool?>(response);

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
    public async Task Exists_ReturnsBadRequest_WhenInboxHeaderIsMissing()
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
        using var response = await SendExistsAsync(
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
    public async Task Exists_ReturnsUnauthorized_WhenAccessTokenIsMissing()
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
        using var response = await SendExistsAsync(
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

    private async Task<HttpResponseMessage> SendExistsAsync(
        string roomId,
        string equipmentId,
        string? inboxId,
        string? accessToken)
    {
        string url =
            $"{ExistsUrl}/{Uri.EscapeDataString(roomId)}" +
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
