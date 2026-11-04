using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomOperatingHour.Controller;

[Collection("Integration Tests")]
public sealed class RoomOperatingHourUpdateControllerTest : BaseIntegrationTest
{
    private const string UpdateUrl = "/api/v1/room-operating-hours";

    private readonly ITestOutputHelper _output;

    public RoomOperatingHourUpdateControllerTest(
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

        var operatingHour = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Monday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0),
            notes: "Original operating hours.");

        var dto = new UpdateRoomOperatingHourDto
        {
            DayOfWeek = DayOfWeek.Tuesday,
            OpensAt = new TimeOnly(9, 0),
            ClosesAt = new TimeOnly(17, 0),
            IsClosed = false,
            IsActive = true,
            Notes = "Updated operating hours."
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            operatingHour.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomOperatingHourDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Message.Should().Be("Room operating hour updated successfully");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(operatingHour.Id);
        http.Data.RoomId.Should().Be(room.Id);
        http.Data.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
        http.Data.OpensAt.Should().Be(new TimeOnly(9, 0));
        http.Data.ClosesAt.Should().Be(new TimeOnly(17, 0));
        http.Data.IsClosed.Should().BeFalse();
        http.Data.IsActive.Should().BeTrue();
        http.Data.Notes.Should().Be("Updated operating hours.");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operatingHour.Id);

        persisted.Should().NotBeNull();
        persisted!.RoomId.Should().Be(room.Id);
        persisted.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
        persisted.OpensAt.Should().Be(new TimeOnly(9, 0));
        persisted.ClosesAt.Should().Be(new TimeOnly(17, 0));
        persisted.IsClosed.Should().BeFalse();
        persisted.IsActive.Should().BeTrue();
        persisted.Notes.Should().Be("Updated operating hours.");
        persisted.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenOnlySomePropertiesAreProvided()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var operatingHour = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Wednesday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0),
            notes: "Keep these notes.");

        var dto = new UpdateRoomOperatingHourDto
        {
            OpensAt = new TimeOnly(9, 30)
        };

        // Act: shared helper sends the PUT request and verifies database + Inbox.
        var updated = await UpdateRoomOperatingHourHttp(
            tokens,
            operatingHour.Id,
            dto);

        // Assert
        updated.Should().NotBeNull();
        updated.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        updated.OpensAt.Should().Be(new TimeOnly(9, 30));
        updated.ClosesAt.Should().Be(new TimeOnly(18, 0));
        updated.IsClosed.Should().BeFalse();
        updated.IsActive.Should().BeTrue();
        updated.Notes.Should().Be("Keep these notes.");
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenRoomOperatingHourDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();
        var nonexistentId = Guid.NewGuid().ToString();
        var dto = new UpdateRoomOperatingHourDto
        {
            OpensAt = new TimeOnly(9, 0)
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

        var http = await ReadAsync<RoomOperatingHourDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeNull();
        http.Errors.Should().Contain("Room operating hour not found.");
        
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();
        const string invalidId = "not-a-valid-uuid";
        var dto = new UpdateRoomOperatingHourDto
        {
            OpensAt = new TimeOnly(9, 0)
        };

        // Act
        using var response = await SendUpdateAsync(
            invalidId,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomOperatingHourDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room operating hour not found.");

    }

    // ---------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenOpeningTimeIsNotEarlierThanClosingTime()
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

        var dto = new UpdateRoomOperatingHourDto
        {
            OpensAt = new TimeOnly(18, 0),
            ClosesAt = new TimeOnly(8, 0)
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            operatingHour.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomOperatingHourDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Opening time must be earlier than closing time.");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == operatingHour.Id);

        persisted.OpensAt.Should().Be(new TimeOnly(8, 0));
        persisted.ClosesAt.Should().Be(new TimeOnly(18, 0));
        persisted.Version.Should().Be(1);
        
    }

    // ---------------------------------------------------------------
    // Conflicts
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsConflict_WhenUpdatedPeriodOverlapsAnotherPeriod()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Friday,
            new TimeOnly(8, 0),
            new TimeOnly(12, 0));

        var target = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Friday,
            new TimeOnly(13, 0),
            new TimeOnly(18, 0));

        var dto = new UpdateRoomOperatingHourDto
        {
            OpensAt = new TimeOnly(11, 0),
            ClosesAt = new TimeOnly(15, 0)
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            target.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomOperatingHourDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room operating hours overlap with an existing period.");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == target.Id);

        persisted.OpensAt.Should().Be(new TimeOnly(13, 0));
        persisted.ClosesAt.Should().Be(new TimeOnly(18, 0));
        persisted.Version.Should().Be(1);
        
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenMarkingDayClosedWhileOtherActiveHoursExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var target = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Saturday,
            new TimeOnly(8, 0),
            new TimeOnly(12, 0));

        await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Saturday,
            new TimeOnly(13, 0),
            new TimeOnly(18, 0));

        var dto = new UpdateRoomOperatingHourDto
        {
            IsClosed = true
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            target.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomOperatingHourDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room already has operating hours configured for this day.");
        
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenMovingPeriodToDayMarkedAsClosed()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var target = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Sunday,
            new TimeOnly(8, 0),
            new TimeOnly(12, 0));

        await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Monday,
            TimeOnly.MinValue,
            TimeOnly.MinValue,
            isClosed: true);

        var dto = new UpdateRoomOperatingHourDto
        {
            DayOfWeek = DayOfWeek.Monday
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            target.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomOperatingHourDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room is marked as closed for this day.");
        
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
        var operatingHour = await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Monday);

        var dto = new UpdateRoomOperatingHourDto
        {
            OpensAt = new TimeOnly(9, 0)
        };

        // Act
        using var response = await SendUpdateAsync(
            operatingHour.Id,
            dto,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(InboxHeader);
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        string inboxId = Guid.NewGuid().ToString();
        var dto = new UpdateRoomOperatingHourDto
        {
            OpensAt = new TimeOnly(9, 0)
        };

        // Act
        using var response = await SendUpdateAsync(
            Guid.NewGuid().ToString(),
            dto,
            inboxId,
            accessToken: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendUpdateAsync(
        string roomOperatingHourId,
        UpdateRoomOperatingHourDto dto,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{UpdateUrl}/{Uri.EscapeDataString(roomOperatingHourId)}";
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(dto)
        };

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
