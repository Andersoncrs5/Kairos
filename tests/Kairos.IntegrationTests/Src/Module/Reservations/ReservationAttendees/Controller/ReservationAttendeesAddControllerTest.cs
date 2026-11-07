using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Response;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Reservations.ReservationAttendees.Controller;

[Collection("Integration Tests")]
public sealed class ReservationAttendeesAddControllerTest : BaseIntegrationTest
{
    private const string CreatePermission = "reservations.attendees.create";

    private readonly ITestOutputHelper _output;

    public ReservationAttendeesAddControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsCreated_AndPersistsAttendeeAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var dto = NewAttendeeDto(attendeeUser.Id, AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
        http.Data.ReservationId.Should().Be(reservation.Id);
        http.Data.UserId.Should().Be(attendeeUser.Id);
        http.Data.Role.Should().Be(AttendeeRoleEnum.Optional);
        http.Data.Status.Should().Be(AttendeeStatusEnum.Pending);
        http.Data.InvitedBy.Should().Be(reservation.UserId);

        http.Data.User.Should().NotBeNull();
        http.Data.User!.Id.Should().Be(attendeeUser.Id);

        http.Data.RespondedAt.Should().BeNull();
        http.Data.ResponseNote.Should().BeNull();
        http.Data.CheckedInAt.Should().BeNull();

        // Verifica a persistência no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<ReservationAttendeeModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == http.Data.Id);

        persisted.Should().NotBeNull();

        persisted!.ReservationId.Should().Be(reservation.Id);
        persisted.UserId.Should().Be(attendeeUser.Id);
        persisted.Role.Should().Be(AttendeeRoleEnum.Optional);
        persisted.Status.Should().Be(AttendeeStatusEnum.Pending);
        persisted.InvitedBy.Should().Be(reservation.UserId);

        persisted.Version.Should().Be(1);
        persisted.CreatedAt.Should().NotBe(default);
        persisted.UpdatedAt.Should().BeNull();

        persisted.RespondedAt.Should().BeNull();
        persisted.ResponseNote.Should().BeNull();
        persisted.CheckedInAt.Should().BeNull();

        // Verifica o processamento do Inbox.
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Add_DefaultsRoleToRequired_WhenRoleIsNotSent()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        string inboxId = Guid.NewGuid().ToString();

        // Act (o body não envia "Role")
        using var response = await SendAddAsync(
            reservation.Id,
            new { UserId = attendeeUser.Id },
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.Role.Should().Be(AttendeeRoleEnum.Required);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Add_ReturnsCreated_ForMultipleDifferentAttendees()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var first = await CreateAttendeeUserAsync();
        var second = await CreateAttendeeUserAsync();

        // Act
        var firstAttendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            first.Id);

        var secondAttendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            second.Id,
            AttendeeRoleEnum.Optional);

        // Assert
        firstAttendee.Id.Should().NotBe(secondAttendee.Id);

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 2);
    }

    // ---------------------------------------------------------------
    // Validação do DTO (o Inbox não é criado: o 400 acontece antes da action)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenUserIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewAttendeeDto(userId: "");
        string inboxId = Guid.NewGuid().ToString();

        // Act & Assert
        await AssertModelValidationFailureAsync(
            Guid.NewGuid().ToString(),
            dto,
            inboxId,
            tokens.Token,
            "UserId");
    }

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenRoleIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        var body = new
        {
            UserId = Guid.NewGuid().ToString(),
            Role = 99
        };

        // Act & Assert
        await AssertModelValidationFailureAsync(
            Guid.NewGuid().ToString(),
            body,
            inboxId,
            tokens.Token,
            "Role");
    }

    // ---------------------------------------------------------------
    // Validação dos identificadores
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenReservationIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewAttendeeDto(Guid.NewGuid().ToString());
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            "invalid-reservation-id",
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Reservation ID must be a valid UUID.");
    }

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenAttendeeUserIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = NewAttendeeDto("invalid-user-id");
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Attendee user ID must be a valid UUID.");

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    // ---------------------------------------------------------------
    // Reserva
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var attendeeUser = await CreateAttendeeUserAsync();

        var dto = NewAttendeeDto(attendeeUser.Id);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            Guid.NewGuid().ToString(),
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Reservation not found.");
    }

    [Fact]
    public async Task Add_ReturnsConflict_WhenReservationIsCancelled()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        await CancelReservationHttp(tokens, reservation.Id);

        var attendeeUser = await CreateAttendeeUserAsync();

        var dto = NewAttendeeDto(attendeeUser.Id);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Reservation is cancelled.");

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    [Fact]
    public async Task Add_ReturnsConflict_WhenReservationHasAlreadyEnded()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MoveReservationToThePastAsync(reservation.Id);

        var attendeeUser = await CreateAttendeeUserAsync();

        var dto = NewAttendeeDto(attendeeUser.Id);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Reservation has already ended.");

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsForbidden_WhenRequesterIsNotTheOrganizer()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);

        // Tem a permissão do endpoint, mas não é o organizador da reserva.
        var (_, otherTokens) =
            await CreateUserWithPermissionsHttp(CreatePermission);

        var attendeeUser = await CreateAttendeeUserAsync();

        var dto = NewAttendeeDto(attendeeUser.Id);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            otherTokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Only the reservation organizer can add attendees.");

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    [Fact]
    public async Task Add_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);

        // Usuário recém-cadastrado: sem nenhuma permissão.
        var (_, tokens) = await RegisterUserAsync();

        var attendeeUser = await CreateAttendeeUserAsync();

        var dto = NewAttendeeDto(attendeeUser.Id);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    [Fact]
    public async Task Add_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var dto = NewAttendeeDto(attendeeUser.Id);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    // ---------------------------------------------------------------
    // Usuário convidado
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsNotFound_WhenAttendeeUserDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = NewAttendeeDto(Guid.NewGuid().ToString());
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Attendee user not found.");

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenAttendeeIsTheOrganizer()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        // O organizador já faz parte da reserva.
        var dto = NewAttendeeDto(reservation.UserId);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "The organizer is already part of the reservation.");

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenAttendeeUserIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var inactiveUser = await CreateAttendeeUserAsync(
            user => user.IsActive = false);

        var dto = NewAttendeeDto(inactiveUser.Id);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Attendee user is not active.");

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    // ---------------------------------------------------------------
    // Convite duplicado
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsConflict_WhenUserIsAlreadyAnAttendee()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id);

        var dto = NewAttendeeDto(attendeeUser.Id);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "User is already an attendee of this reservation.");

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 1);
    }

    // ---------------------------------------------------------------
    // Capacidade da sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsConflict_WhenRoomCapacityIsExceeded()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        // Capacidade 2 = organizador + 1 participante.
        await SetRoomCapacityAsync(reservation.RoomId, capacity: 2);

        var first = await CreateAttendeeUserAsync();
        var second = await CreateAttendeeUserAsync();

        await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            first.Id);

        var dto = NewAttendeeDto(second.Id);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Room capacity exceeded.");

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 1);
    }

    [Fact]
    public async Task Add_DoesNotCountDeclinedAttendees_WhenCheckingCapacity()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        // Capacidade 2 = organizador + 1 participante.
        await SetRoomCapacityAsync(reservation.RoomId, capacity: 2);

        var declinedUser = await CreateAttendeeUserAsync();
        var newUser = await CreateAttendeeUserAsync();
        var extraUser = await CreateAttendeeUserAsync();

        var declined = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            declinedUser.Id);

        // Quem recusou não ocupa vaga.
        await SetAttendeeStatusAsync(
            declined.Id,
            AttendeeStatusEnum.Declined);

        // Act: o novo convidado ocupa a vaga liberada.
        var added = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            newUser.Id);

        // Assert
        added.UserId.Should().Be(newUser.Id);

        // A sala voltou a ficar cheia: o próximo convite é recusado.
        using var response = await SendAddAsync(
            reservation.Id,
            NewAttendeeDto(extraUser.Id),
            Guid.NewGuid().ToString(),
            tokens.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 2);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Add_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var dto = NewAttendeeDto(attendeeUser.Id);

        // Act
        using var response = await SendAddAsync(
            reservation.Id,
            dto,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(error =>
            error.Contains(
                InboxHeader,
                StringComparison.OrdinalIgnoreCase));

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 0);
    }

    [Fact]
    public async Task Add_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var first = await CreateAttendeeUserAsync();
        var second = await CreateAttendeeUserAsync();

        string inboxId = Guid.NewGuid().ToString();

        // Primeiro convite.
        using (var firstResponse = await SendAddAsync(
                   reservation.Id,
                   NewAttendeeDto(first.Id),
                   inboxId,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(
                HttpStatusCode.Created);
        }

        // Segundo convite (outro usuário) reutilizando o mesmo Inbox.
        using var response = await SendAddAsync(
            reservation.Id,
            NewAttendeeDto(second.Id),
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        await AssertAttendeeCountAsync(reservation.Id, expectedCount: 1);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static string AddUrl(string reservationId)
        => $"/api/v1/reservations/{Uri.EscapeDataString(reservationId)}/attendees";

    private Task<HttpResponseMessage> SendAddAsync(
        string reservationId,
        object body,
        string? inboxId,
        string? accessToken)
    {
        return SendAsync(
            AddUrl(reservationId),
            body: body,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private static AddReservationAttendeeDto NewAttendeeDto(
        string userId,
        AttendeeRoleEnum role = AttendeeRoleEnum.Required)
    {
        return new AddReservationAttendeeDto
        {
            UserId = userId,
            Role = role
        };
    }

    private async Task<UserModel> CreateAttendeeUserAsync(
        Action<UserModel>? configure = null)
    {
        using var scope = CreateScope();

        return await CreateUserAsync(
            scope.ServiceProvider,
            configure);
    }

    /// <summary>
    /// O 400 de validação do model acontece antes da action,
    /// então nenhuma linha de Inbox deve existir.
    /// </summary>
    private async Task AssertModelValidationFailureAsync(
        string reservationId,
        object body,
        string inboxId,
        string accessToken,
        string propertyName)
    {
        using var response = await SendAddAsync(
            reservationId,
            body,
            inboxId,
            accessToken);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.ErrorValids.Should().NotBeNull();
        http.ErrorValids!.Keys.Should().Contain(key =>
            key.EndsWith(
                propertyName,
                StringComparison.OrdinalIgnoreCase));

        await AssertInboxNotCreatedAsync(inboxId);
    }

    private async Task AssertAttendeeCountAsync(
        string reservationId,
        int expectedCount)
    {
        using var scope = CreateScope();

        var count = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationAttendeeModel>()
            .AsNoTracking()
            .CountAsync(attendee =>
                attendee.ReservationId == reservationId);

        count.Should().Be(expectedCount);
    }

    private async Task SetRoomCapacityAsync(string roomId, int capacity)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var room = await db.Set<RoomModel>()
            .SingleAsync(item => item.Id == roomId);

        room.Capacity = capacity;

        await db.SaveChangesAsync();
    }

    private async Task SetAttendeeStatusAsync(
        string attendeeId,
        AttendeeStatusEnum status)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var attendee = await db.Set<ReservationAttendeeModel>()
            .SingleAsync(item => item.Id == attendeeId);

        attendee.Status = status;

        await db.SaveChangesAsync();
    }

    private async Task MoveReservationToThePastAsync(string reservationId)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var reservation = await db.Set<ReservationModel>()
            .SingleAsync(item => item.Id == reservationId);

        reservation.StartsAt = DateTimeOffset.UtcNow.AddHours(-3);
        reservation.EndsAt = DateTimeOffset.UtcNow.AddHours(-1);

        await db.SaveChangesAsync();
    }
}