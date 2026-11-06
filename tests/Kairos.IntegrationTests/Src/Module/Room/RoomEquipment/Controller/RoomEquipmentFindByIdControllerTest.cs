
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomEquipment.Controller;

[Collection("Integration Tests")]
public sealed class RoomEquipmentFindByIdControllerTest : BaseIntegrationTest
{
    private const string FindUrl = "/api/v1/room-equipments";

    private readonly ITestOutputHelper _output;

    public RoomEquipmentFindByIdControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsOk_WhenRoomEquipmentExists()
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
        using var response = await SendFindByIdAsync(
            roomEquipment.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomEquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Message.Should().Be("Room equipment found");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(roomEquipment.Id);
        http.Data.RoomId.Should().Be(room.Id);
        http.Data.EquipmentId.Should().Be(equipment.Id);
        http.Data.Quantity.Should().Be(roomEquipment.Quantity);
        http.Data.Notes.Should().Be(roomEquipment.Notes);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenRoomEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            nonexistentId,
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
        http.Errors.Should().Contain("Room Equipment not found.");
    }

    // ---------------------------------------------------------------
    // ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        const string invalidId = "invalid-uuid";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            invalidId,
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
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Inbox validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
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
        using var response = await SendFindByIdAsync(
            roomEquipment.Id,
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
    public async Task FindById_ReturnsUnauthorized_WhenAccessTokenIsMissing()
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
        using var response = await SendFindByIdAsync(
            roomEquipment.Id,
            inboxId,
            accessToken: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendFindByIdAsync(
        string roomEquipmentId,
        string? inboxId,
        string? accessToken)
    {
        string url =
            $"{FindUrl}/{Uri.EscapeDataString(roomEquipmentId)}";

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
