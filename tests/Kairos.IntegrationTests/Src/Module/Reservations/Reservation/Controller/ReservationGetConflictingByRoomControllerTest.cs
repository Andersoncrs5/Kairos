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
        string inboxId = Guid.NewGuid().ToString();

        // Consulta um intervalo que se sobrepõe à reserva.
        var startsAt = reservation.StartsAt.AddMinutes(30);
        var endsAt = reservation.EndsAt.AddMinutes(-30);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            reservation.RoomId,
            startsAt,
            endsAt,
            tokens.Token,
            inboxId);

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

        http.TraceId.Should().Be(inboxId);

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
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task GetConflictingByRoom_ReturnsEmptyList_WhenNoConflictsExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var startsAt = DateTimeOffset.UtcNow.AddDays(20);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            tokens.Token,
            inboxId);

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

        http.TraceId.Should().Be(inboxId);

        // Verifica o processamento do Inbox.
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validação dos parâmetros
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetConflictingByRoom_ReturnsBadRequest_WhenRoomIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            " ",
            startsAt,
            endsAt,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetConflictingByRoom_ReturnsBadRequest_WhenStartsAtIsAfterEndsAt()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(-1);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            tokens.Token,
            inboxId);

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
        string inboxId = Guid.NewGuid().ToString();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            startsAt,
            tokens.Token,
            inboxId);

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
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            reservation.RoomId,
            reservation.StartsAt,
            reservation.EndsAt,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A autorização impede a execução da action.
        await AssertInboxProcessedAsync(inboxId);
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
        string inboxId = Guid.NewGuid().ToString();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendGetConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertInboxProcessedAsync(inboxId);
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
    public async Task GetConflictingByRoom_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Primeira consulta: deve ser processada com sucesso.
        using (var firstResponse = await SendGetConflictingByRoomAsync(
                   reservation.RoomId,
                   reservation.StartsAt,
                   reservation.EndsAt,
                   tokens.Token,
                   inboxId))
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
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
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