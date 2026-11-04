using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomOperatingHour.Controller;

[Collection("Integration Tests")]
public sealed class RoomOperatingHourFindByIdControllerTest : BaseIntegrationTest
{
    private const string FindByIdUrl = "/api/v1/room-operating-hours";

    private readonly ITestOutputHelper _output;

    public RoomOperatingHourFindByIdControllerTest(
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
    public async Task FindById_ReturnsOk_AndReturnsPersistedRoomOperatingHour_AndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var operatingHour = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Monday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0),
            notes: "Monday operating hours.");

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            operatingHour.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomOperatingHourDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Message.Should().Be("Room operating hour found");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(operatingHour.Id);
        http.Data.RoomId.Should().Be(room.Id);
        http.Data.DayOfWeek.Should().Be(DayOfWeek.Monday);
        http.Data.OpensAt.Should().Be(new TimeOnly(8, 0));
        http.Data.ClosesAt.Should().Be(new TimeOnly(18, 0));
        http.Data.IsClosed.Should().BeFalse();
        http.Data.IsActive.Should().BeTrue();
        http.Data.Notes.Should().Be("Monday operating hours.");

        // Verify that the returned record is still persisted.
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operatingHour.Id);

        persisted.Should().NotBeNull();
        persisted!.RoomId.Should().Be(room.Id);
        persisted.DayOfWeek.Should().Be(DayOfWeek.Monday);
        persisted.OpensAt.Should().Be(new TimeOnly(8, 0));
        persisted.ClosesAt.Should().Be(new TimeOnly(18, 0));
        persisted.Notes.Should().Be("Monday operating hours.");

        
    }

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenRoomOperatingHourDoesNotExist_AndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            nonexistentId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomOperatingHourDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeNull();
        http.Errors.Should().Contain("Room operating hour not found.");

        
    }

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenIdIsNotAValidUuid_AndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        const string invalidId = "not-a-valid-uuid";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            invalidId,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomOperatingHourDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.TraceId.Should().Be(inboxId);
        http.Errors.Should().Contain("Room operating hour not found.");


    }

    // ---------------------------------------------------------------
    // Inbox validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
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
    public async Task FindById_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
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
    public async Task FindById_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        string inboxId = Guid.NewGuid().ToString();
        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            nonexistentId,
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
        string roomOperatingHourId,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{FindByIdUrl}/{Uri.EscapeDataString(roomOperatingHourId)}";
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
