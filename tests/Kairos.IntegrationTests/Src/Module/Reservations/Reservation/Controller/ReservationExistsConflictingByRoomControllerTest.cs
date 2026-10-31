using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Reservations.Reservation.Controller;

[Collection("Integration Tests")]
public sealed class ReservationExistsConflictingByRoomControllerTest
    : BaseIntegrationTest
{
    private const string ExistsConflictingUrl =
        "/api/v1/reservations/rooms";

    private readonly ITestOutputHelper _output;

    public ReservationExistsConflictingByRoomControllerTest(
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
    public async Task ExistsConflictingByRoom_ReturnsTrue_WhenReservationConflicts()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var inbox = await NewInboxAsync();

        var startsAt = reservation.StartsAt.AddMinutes(30);
        var endsAt = reservation.EndsAt.AddMinutes(-30);

        // Act
        using var response = await SendExistsConflictingByRoomAsync(
            reservation.RoomId,
            startsAt,
            endsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("Room has conflicting reservations");
        http.TraceId.Should().Be(inbox.Id);

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task ExistsConflictingByRoom_ReturnsFalse_WhenNoReservationConflicts()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendExistsConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
        http.Message.Should().Be("Room has no conflicting reservations");
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
    public async Task ExistsConflictingByRoom_ReturnsBadRequest_WhenRoomIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendExistsConflictingByRoomAsync(
            " ",
            startsAt,
            endsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    }

    [Fact]
    public async Task ExistsConflictingByRoom_ReturnsBadRequest_WhenStartsAtIsAfterEndsAt()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(-1);

        // Act
        using var response = await SendExistsConflictingByRoomAsync(
            room.Id,
            startsAt,
            endsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExistsConflictingByRoom_ReturnsBadRequest_WhenDatesAreEqual()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);

        // Act
        using var response = await SendExistsConflictingByRoomAsync(
            room.Id,
            startsAt,
            startsAt,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsConflictingByRoom_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsConflictingByRoomAsync(
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
    public async Task ExistsConflictingByRoom_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendExistsConflictingByRoomAsync(
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
    public async Task ExistsConflictingByRoom_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Act
        using var response = await SendExistsConflictingByRoomAsync(
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
    public async Task ExistsConflictingByRoom_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsConflictingByRoomAsync(
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
    public async Task ExistsConflictingByRoom_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var inbox = await NewInboxAsync();

        // Primeira consulta: deve ser processada com sucesso.
        using (var firstResponse = await SendExistsConflictingByRoomAsync(
                   reservation.RoomId,
                   reservation.StartsAt,
                   reservation.EndsAt,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().BeTrue();
        }

        // Segunda consulta reutiliza o Inbox já processado.
        using var response = await SendExistsConflictingByRoomAsync(
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

    private async Task<HttpResponseMessage> SendExistsConflictingByRoomAsync(
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
            $"{ExistsConflictingUrl}/{Uri.EscapeDataString(roomId)}" +
            $"/conflicts/exists?startsAt={startsAtQuery}&endsAt={endsAtQuery}";

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