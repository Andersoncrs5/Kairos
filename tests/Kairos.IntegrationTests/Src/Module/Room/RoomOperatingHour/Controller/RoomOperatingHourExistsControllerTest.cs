using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomOperatingHour.Controller;

[Collection("Integration Tests")]
public sealed class RoomOperatingHourExistsControllerTest : BaseIntegrationTest
{
    private const string ExistsUrl = "/api/v1/room-operating-hours/exists/id";

    private readonly ITestOutputHelper _output;

    public RoomOperatingHourExistsControllerTest(
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
    public async Task Exists_ReturnsOkTrue_WhenRoomOperatingHourExists_AndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var operatingHour = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Monday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0));

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsAsync(
            operatingHour.Id,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inbox.Id);
        http.Data.Should().BeTrue();
        http.Message.Should().Be("Room operating hour exists");

        var updatedInbox = await GetInboxAsync(inbox.Id);
        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task Exists_ReturnsOkFalse_WhenRoomOperatingHourDoesNotExist_AndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsAsync(
            nonexistentId,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inbox.Id);
        http.Data.Should().BeFalse();
        http.Message.Should().Be("Room operating hour does not exist");

        var updatedInbox = await GetInboxAsync(inbox.Id);
        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Inbox validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Exists_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            nonexistentId,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(InboxHeader);
    }

    [Fact]
    public async Task Exists_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            nonexistentId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Inbox is not available for processing.");
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
        var operatingHour = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Wednesday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0));

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsAsync(
            operatingHour.Id,
            inbox.Id,
            accessToken: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updatedInbox = await GetInboxAsync(inbox.Id);
        updatedInbox.Status.Should().Be(InboxStatusEnum.Pending);
        updatedInbox.ProcessedAt.Should().BeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendExistsAsync(
        string roomOperatingHourId,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{ExistsUrl}/{Uri.EscapeDataString(roomOperatingHourId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

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
