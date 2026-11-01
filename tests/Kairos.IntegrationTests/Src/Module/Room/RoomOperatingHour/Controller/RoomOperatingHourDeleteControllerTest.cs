using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomOperatingHour.Controller;

[Collection("Integration Tests")]
public sealed class RoomOperatingHourDeleteControllerTest : BaseIntegrationTest
{
    private const string DeleteUrl = "/api/v1/room-operating-hours";

    private readonly ITestOutputHelper _output;

    public RoomOperatingHourDeleteControllerTest(
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
    public async Task Delete_ReturnsOk_AndDeletesRoomOperatingHourAndProcessesInbox()
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
        using var response = await SendDeleteAsync(
            operatingHour.Id,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inbox.Id);
        http.Data.Should().BeTrue();

        await AssertOperatingHourDoesNotExistAsync(operatingHour.Id);

        var updatedInbox = await GetInboxAsync(inbox.Id);
        updatedInbox.Status.Should().Be(Kairos.Api.Common.Enums.InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenRoomOperatingHourDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();
        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            nonexistentId,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

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
        var operatingHour = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Wednesday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0));

        // Act
        using var response = await SendDeleteAsync(
            operatingHour.Id,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("X-Inbox-Id");

        await AssertOperatingHourExistsAsync(operatingHour.Id);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var operatingHour = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Thursday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0));

        // Act
        using var response = await SendDeleteAsync(
            operatingHour.Id,
            inboxId: Guid.NewGuid().ToString(),
            accessToken: tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertOperatingHourExistsAsync(operatingHour.Id);
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
        var operatingHour = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Friday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0));

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            operatingHour.Id,
            inbox.Id,
            accessToken: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertOperatingHourExistsAsync(operatingHour.Id);

        var updatedInbox = await GetInboxAsync(inbox.Id);
        updatedInbox.Status.Should().Be(Kairos.Api.Common.Enums.InboxStatusEnum.Pending);
        updatedInbox.ProcessedAt.Should().BeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendDeleteAsync(
        string roomOperatingHourId,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{DeleteUrl}/{Uri.EscapeDataString(roomOperatingHourId)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);

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

    private async Task AssertOperatingHourExistsAsync(string id)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == id);

        exists.Should().BeTrue();
    }

    private async Task AssertOperatingHourDoesNotExistAsync(string id)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == id);

        exists.Should().BeFalse();
    }
}
