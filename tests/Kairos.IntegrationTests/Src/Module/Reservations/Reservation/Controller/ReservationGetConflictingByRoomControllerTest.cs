using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Reservations.Reservation.Controller;

[Collection("Integration Tests")]
public sealed class ReservationGetConflictingByRoomControllerTest
    : BaseIntegrationTest
{
    private const string GetConflictingUrl =
        "/api/v1/reservations/rooms";

    private readonly ITestOutputHelper _output;

    public ReservationGetConflictingByRoomControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Consulta
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetConflictingByRoom_ReturnsReservations_WhenConflictsExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var inbox = await NewInboxAsync();

        // Consulta um intervalo que se sobrepõe à reserva.
        var startsAt = reservation.StartsAt.AddMinutes(30);
        var endsAt = reservation.EndsAt.AddMinutes(-30);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            reservation.RoomId,
            startsAt,
            endsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<IReadOnlyList<ReservationDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Message.Should().Be(
            "Conflicting reservations retrieved successfully");

        http.TraceId.Should().Be(inbox.Id);

        http.Data.Should().ContainSingle();

        var conflictingReservation = http.Data!.Single();

        conflictingReservation.Id.Should().Be(reservation.Id);
        conflictingReservation.RoomId.Should().Be(reservation.RoomId);
        conflictingReservation.CategoryId.Should().Be(reservation.CategoryId);
        conflictingReservation.UserId.Should().Be(reservation.UserId);
        conflictingReservation.Title.Should().Be(reservation.Title);
        conflictingReservation.StartsAt.Should().Be(reservation.StartsAt);
        conflictingReservation.EndsAt.Should().Be(reservation.EndsAt);
        conflictingReservation.Status.Should().Be(reservation.Status);

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task GetConflictingByRoom_ReturnsEmptyList_WhenNoConflictsExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(20);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<IReadOnlyList<ReservationDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data.Should().BeEmpty();

        http.Message.Should().Be(
            "Conflicting reservations retrieved successfully");

        http.TraceId.Should().Be(inbox.Id);

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validação dos parâmetros
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetConflictingByRoom_ReturnsBadRequest_WhenRoomIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            " ",
            startsAt,
            endsAt,
            tokens.Token,
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetConflictingByRoom_ReturnsBadRequest_WhenStartsAtIsAfterEndsAt()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(-1);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<IReadOnlyList<ReservationDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Reservation start date must be earlier than end date.");
    }

    [Fact]
    public async Task GetConflictingByRoom_ReturnsBadRequest_WhenDatesAreEqual()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            startsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<IReadOnlyList<ReservationDto>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Reservation start date must be earlier than end date.");
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetConflictingByRoom_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            reservation.RoomId,
            reservation.StartsAt,
            reservation.EndsAt,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A autorização impede a execução da action.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Pending);
        updatedInbox.ProcessedAt.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Autenticação
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetConflictingByRoom_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            accessToken: null,
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Pending);
        updatedInbox.ProcessedAt.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetConflictingByRoom_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetConflictingByRoom_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetConflictingByRoom_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var inbox = await NewInboxAsync();

        // Primeira consulta: deve ser processada com sucesso.
        using (var firstResponse = await SendGetConflictingByRoomAsync(
                   reservation.RoomId,
                   reservation.StartsAt,
                   reservation.EndsAt,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp =
                await ReadAsync<IReadOnlyList<ReservationDto>>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
            firstHttp.Data.Should().ContainSingle();
        }

        // Segunda consulta reutiliza o Inbox já processado.
        using var response = await SendGetConflictingByRoomAsync(
            reservation.RoomId,
            reservation.StartsAt,
            reservation.EndsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendGetConflictingByRoomAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string? accessToken,
        string? inboxId)
    {
        string startsAtQuery = Uri.EscapeDataString(
            startsAt.ToString("O", CultureInfo.InvariantCulture));

        string endsAtQuery = Uri.EscapeDataString(
            endsAt.ToString("O", CultureInfo.InvariantCulture));

        string url =
            $"{GetConflictingUrl}/{Uri.EscapeDataString(roomId)}" +
            $"/conflicts?startsAt={startsAtQuery}&endsAt={endsAtQuery}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            url);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);
        }

        if (inboxId is not null)
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }
}