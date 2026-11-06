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
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomOperatingHour.Controller;

[Collection("Integration Tests")]
public sealed class RoomOperatingHourCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/room-operating-hours/rooms";

    private readonly ITestOutputHelper _output;

    public RoomOperatingHourCreateControllerTest(
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
    public async Task Create_ReturnsCreated_AndPersistsRoomOperatingHourAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomOperatingHourDto(
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(18, 0),
            notes: "Monday operating hours.");

        // Act
        using var response = await SendCreateAsync(
            room.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<RoomOperatingHourDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
        http.Data.RoomId.Should().Be(room.Id);
        http.Data.DayOfWeek.Should().Be(dto.DayOfWeek);
        http.Data.OpensAt.Should().Be(dto.OpensAt);
        http.Data.ClosesAt.Should().Be(dto.ClosesAt);
        http.Data.IsClosed.Should().BeFalse();
        http.Data.IsActive.Should().BeTrue();
        http.Data.Notes.Should().Be(dto.Notes);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == http.Data.Id);

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(http.Data.Id);
        persisted.RoomId.Should().Be(room.Id);
        persisted.DayOfWeek.Should().Be(dto.DayOfWeek);
        persisted.OpensAt.Should().Be(dto.OpensAt);
        persisted.ClosesAt.Should().Be(dto.ClosesAt);
        persisted.IsClosed.Should().BeFalse();
        persisted.IsActive.Should().BeTrue();
        persisted.Notes.Should().Be(dto.Notes);
        persisted.Version.Should().Be(1);
        persisted.CreatedAt.Should().NotBe(default);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenCreatingClosedDay()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomOperatingHourDto(
            dayOfWeek: DayOfWeek.Sunday,
            opensAt: TimeOnly.MinValue,
            closesAt: TimeOnly.MinValue,
            isClosed: true);

        // Act
        using var response = await SendCreateAsync(
            room.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<RoomOperatingHourDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.IsClosed.Should().BeTrue();
        http.Data.IsActive.Should().BeTrue();

        await AssertOperatingHourExistsAsync(http.Data.Id);
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Create_AllowsMultipleNonOverlappingPeriodsOnSameDay()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Tuesday,
            new TimeOnly(8, 0),
            new TimeOnly(12, 0));

        string inboxId = Guid.NewGuid().ToString();
        var dto = NewRoomOperatingHourDto(
            DayOfWeek.Tuesday,
            new TimeOnly(13, 0),
            new TimeOnly(18, 0));

        // Act
        using var response = await SendCreateAsync(
            room.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<RoomOperatingHourDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
        http.Data.OpensAt.Should().Be(new TimeOnly(13, 0));
        http.Data.ClosesAt.Should().Be(new TimeOnly(18, 0));

        await AssertOperatingHourCountAsync(room.Id, DayOfWeek.Tuesday, 2);
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // DTO and business validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDayOfWeekIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomOperatingHourDto(
            dayOfWeek: (DayOfWeek)7,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(18, 0));

        // Act
        using var response = await SendCreateAsync(
            room.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertNoOperatingHourExistsAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenOpeningTimeIsNotEarlierThanClosingTime()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomOperatingHourDto(
            dayOfWeek: DayOfWeek.Wednesday,
            opensAt: new TimeOnly(18, 0),
            closesAt: new TimeOnly(8, 0));

        // Act
        using var response = await SendCreateAsync(
            room.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomOperatingHourDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        await AssertNoOperatingHourExistsAsync(room.Id);
    }

    // ---------------------------------------------------------------
    // Room validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentRoomId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = NewRoomOperatingHourDto(
            DayOfWeek.Thursday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0));

        // Act
        using var response = await SendCreateAsync(
            nonexistentRoomId,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomOperatingHourDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room doesn't exist.");
    }

    // ---------------------------------------------------------------
    // Existing operating hours and conflicts
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConflict_WhenCreatingClosedDayWithExistingHours()
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

        string inboxId = Guid.NewGuid().ToString();
        var dto = NewRoomOperatingHourDto(
            DayOfWeek.Friday,
            TimeOnly.MinValue,
            TimeOnly.MinValue,
            isClosed: true);

        // Act
        using var response = await SendCreateAsync(
            room.Id,
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

        await AssertOperatingHourCountAsync(room.Id, DayOfWeek.Friday, 1);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenCreatingOpenPeriodOnClosedDay()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Saturday,
            TimeOnly.MinValue,
            TimeOnly.MinValue,
            isClosed: true);

        string inboxId = Guid.NewGuid().ToString();
        var dto = NewRoomOperatingHourDto(
            DayOfWeek.Saturday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0));

        // Act
        using var response = await SendCreateAsync(
            room.Id,
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
            "Room is marked as closed for this day.");

        await AssertOperatingHourCountAsync(room.Id, DayOfWeek.Saturday, 1);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenPeriodsOverlap()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        await CreateRoomOperatingHourHttp(
            tokens,
            room.Id,
            DayOfWeek.Sunday,
            new TimeOnly(8, 0),
            new TimeOnly(12, 0));

        string inboxId = Guid.NewGuid().ToString();
        var dto = NewRoomOperatingHourDto(
            DayOfWeek.Sunday,
            new TimeOnly(11, 0),
            new TimeOnly(14, 0));

        // Act
        using var response = await SendCreateAsync(
            room.Id,
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

        await AssertOperatingHourCountAsync(room.Id, DayOfWeek.Sunday, 1);
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
        var dto = NewRoomOperatingHourDto(
            DayOfWeek.Monday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0));

        // Act
        using var response = await SendCreateAsync(
            room.Id,
            dto,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var inboxes = await response.Content.ReadAsStringAsync();
        inboxes.Should().Contain("X-Inbox-Id");

        await AssertNoOperatingHourExistsAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();
        var dto = NewRoomOperatingHourDto(
            DayOfWeek.Monday,
            new TimeOnly(8, 0),
            new TimeOnly(18, 0));

        // Act
        using var response = await SendCreateAsync(
            room.Id,
            dto,
            inboxId,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertNoOperatingHourExistsAsync(room.Id);
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        string roomId,
        CreateRoomOperatingHourDto dto,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{CreateUrl}/{Uri.EscapeDataString(roomId)}";

        return SendAsync(
            url,
            HttpMethod.Post,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private static CreateRoomOperatingHourDto NewRoomOperatingHourDto(
        DayOfWeek dayOfWeek,
        TimeOnly opensAt,
        TimeOnly closesAt,
        bool isClosed = false,
        string? notes = "Created by integration tests.")
    {
        return new CreateRoomOperatingHourDto
        {
            DayOfWeek = dayOfWeek,
            OpensAt = opensAt,
            ClosesAt = closesAt,
            IsClosed = isClosed,
            Notes = notes
        };
    }

    private async Task AssertNoOperatingHourExistsAsync(string roomId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .AnyAsync(item => item.RoomId == roomId);

        exists.Should().BeFalse();
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

    private async Task AssertOperatingHourCountAsync(
        string roomId,
        DayOfWeek dayOfWeek,
        int expectedCount)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var count = await db.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .CountAsync(item =>
                item.RoomId == roomId && item.DayOfWeek == dayOfWeek);

        count.Should().Be(expectedCount);
    }
    
    protected async Task<HttpResponseMessage> SendAsync(
        string url,
        HttpMethod? method = null,
        object? body = null,
        string? accessToken = null,
        string? inboxId = null)
    {
        // Se o método não for informado, assume POST por retrocompatibilidade
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, url);

        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType());

        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (inboxId is not null)
            request.Headers.Add(InboxHeader, inboxId);

        return await Client.SendAsync(request);
    }
}