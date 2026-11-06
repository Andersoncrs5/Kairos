
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomEquipment.Controller;

[Collection("Integration Tests")]
public sealed class RoomEquipmentFindByRoomIdControllerTest
    : BaseIntegrationTest
{
    private const string FindUrl =
        "/api/v1/room-equipments/rooms";

    private readonly ITestOutputHelper _output;

    public RoomEquipmentFindByRoomIdControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Find by Room ID
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindByRoomId_ReturnsOk_WhenRoomHasEquipmentAssociations()
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

        // Act
        using var response = await SendFindByRoomIdAsync(
            room.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http =
            await ReadAsync<IReadOnlyList<RoomEquipmentDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Message.Should().Be(
            "Room equipments retrieved successfully");

        http.Data.Should().NotBeNull();
        http.Data.Should().HaveCount(2);

        var firstResult = http.Data!
            .SingleOrDefault(item => item.Id == firstAssociation.Id);

        var secondResult = http.Data!
            .SingleOrDefault(item => item.Id == secondAssociation.Id);

        firstResult.Should().NotBeNull();
        firstResult!.RoomId.Should().Be(room.Id);
        firstResult.EquipmentId.Should().Be(firstEquipment.Id);
        firstResult.Quantity.Should().Be(firstAssociation.Quantity);
        firstResult.Notes.Should().Be(firstAssociation.Notes);

        secondResult.Should().NotBeNull();
        secondResult!.RoomId.Should().Be(room.Id);
        secondResult.EquipmentId.Should().Be(secondEquipment.Id);
        secondResult.Quantity.Should().Be(secondAssociation.Quantity);
        secondResult.Notes.Should().Be(secondAssociation.Notes);
    }

    [Fact]
    public async Task FindByRoomId_ReturnsOkWithEmptyList_WhenRoomHasNoEquipmentAssociations()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // No equipment associations are created.

        // Act
        using var response = await SendFindByRoomIdAsync(
            room.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http =
            await ReadAsync<IReadOnlyList<RoomEquipmentDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Message.Should().Be(
            "Room equipments retrieved successfully");

        http.Data.Should().NotBeNull();
        http.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task FindByRoomId_ReturnsOnlyAssociationsBelongingToRequestedRoom()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var firstRoom = await CreateRoomHttp(tokens);
        var secondRoom = await CreateRoomHttp(tokens);

        var firstEquipment = await CreateEquipmentHttp(tokens);
        var secondEquipment = await CreateEquipmentHttp(tokens);

        var firstAssociation = await CreateRoomEquipmentHttp(
            tokens.Token,
            firstRoom.Id,
            firstEquipment.Id);

        var secondAssociation = await CreateRoomEquipmentHttp(
            tokens.Token,
            secondRoom.Id,
            secondEquipment.Id);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByRoomIdAsync(
            firstRoom.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http =
            await ReadAsync<IReadOnlyList<RoomEquipmentDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data.Should().ContainSingle();

        http.Data.Should().ContainSingle(item =>
            item.Id == firstAssociation.Id);

        http.Data.Should().NotContain(item =>
            item.Id == secondAssociation.Id);

        http.Data.Should().OnlyContain(item =>
            item.RoomId == firstRoom.Id);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindByRoomId_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string nonexistentRoomId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByRoomIdAsync(
            nonexistentRoomId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http =
            await ReadAsync<IReadOnlyList<RoomEquipmentDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeNull();
        http.Errors.Should().Contain("Room doesn't exist.");
    }

    // ---------------------------------------------------------------
    // Room ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindByRoomId_ReturnsBadRequest_WhenRoomIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        const string invalidRoomId = "invalid-uuid";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByRoomIdAsync(
            invalidRoomId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http =
            await ReadAsync<IReadOnlyList<RoomEquipmentDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeNull();
        http.Errors.Should().Contain(
            "Room ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Inbox validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindByRoomId_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        // Act
        using var response = await SendFindByRoomIdAsync(
            room.Id,
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
    public async Task FindByRoomId_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByRoomIdAsync(
            room.Id,
            inboxId,
            accessToken: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendFindByRoomIdAsync(
        string roomId,
        string? inboxId,
        string? accessToken)
    {
        string url =
            $"{FindUrl}/{Uri.EscapeDataString(roomId)}";

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
