using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Reservations.ReservationAttendees.Controller;

[Collection("Integration Tests")]
public sealed class ReservationAttendeesUpdateControllerTest : BaseIntegrationTest
{
    private const string UpdatePermission = "reservations.attendees.update";

    private readonly ITestOutputHelper _output;

    public ReservationAttendeesUpdateControllerTest(
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
    public async Task Update_ReturnsOk_AndPersistsRoleAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id,
            AttendeeRoleEnum.Required);

        var before = await GetAttendeeByIdAsync(attendee.Id);

        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            attendee.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(attendee.Id);
        http.Data.ReservationId.Should().Be(reservation.Id);
        http.Data.UserId.Should().Be(attendeeUser.Id);
        http.Data.Role.Should().Be(AttendeeRoleEnum.Optional);

        // Os demais campos não são alterados.
        http.Data.Status.Should().Be(AttendeeStatusEnum.Pending);
        http.Data.InvitedBy.Should().Be(reservation.UserId);
        http.Data.RespondedAt.Should().BeNull();
        http.Data.ResponseNote.Should().BeNull();
        http.Data.CheckedInAt.Should().BeNull();

        // Verifica a persistência no banco.
        var persisted = await GetAttendeeByIdAsync(attendee.Id);

        persisted.Role.Should().Be(AttendeeRoleEnum.Optional);
        persisted.Status.Should().Be(AttendeeStatusEnum.Pending);
        persisted.InvitedBy.Should().Be(reservation.UserId);
        persisted.ReservationId.Should().Be(reservation.Id);
        persisted.UserId.Should().Be(attendeeUser.Id);

        persisted.Version.Should().Be(before.Version + 1);
        persisted.CreatedAt.Should().Be(before.CreatedAt);
        persisted.UpdatedAt.Should().NotBeNull();

        persisted.RespondedAt.Should().BeNull();
        persisted.ResponseNote.Should().BeNull();
        persisted.CheckedInAt.Should().BeNull();

        // Verifica o processamento do Inbox.
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsOk_WithoutChanges_WhenRoleIsTheSame()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id,
            AttendeeRoleEnum.Required);

        var dto = NewUpdateDto(AttendeeRoleEnum.Required);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            attendee.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.Role.Should().Be(AttendeeRoleEnum.Required);

        // Nada foi alterado no banco (idempotente).
        await AssertAttendeeUntouchedAsync(
            attendee.Id,
            AttendeeRoleEnum.Required);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_OnlyChangesTheTargetedAttendee()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var firstUser = await CreateAttendeeUserAsync();
        var secondUser = await CreateAttendeeUserAsync();

        var first = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            firstUser.Id);

        var second = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            secondUser.Id);

        // Act
        await UpdateReservationAttendeeHttp(
            tokens,
            reservation.Id,
            first.Id,
            AttendeeRoleEnum.Optional);

        // Assert
        var persistedFirst = await GetAttendeeByIdAsync(first.Id);
        persistedFirst.Role.Should().Be(AttendeeRoleEnum.Optional);

        await AssertAttendeeUntouchedAsync(
            second.Id,
            AttendeeRoleEnum.Required);
    }

    [Fact]
    public async Task Update_IncrementsVersion_OnEachRealChange()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id,
            AttendeeRoleEnum.Required);

        // Act
        await UpdateReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendee.Id,
            AttendeeRoleEnum.Optional);

        await UpdateReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendee.Id,
            AttendeeRoleEnum.Required);

        // Assert
        var persisted = await GetAttendeeByIdAsync(attendee.Id);

        persisted.Role.Should().Be(AttendeeRoleEnum.Required);
        persisted.Version.Should().Be(3);
        persisted.UpdatedAt.Should().NotBeNull();
    }

    // ---------------------------------------------------------------
    // Validação do DTO (o Inbox não é criado: o 400 acontece antes da action)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenRoleIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id);

        string inboxId = Guid.NewGuid().ToString();

        // Act & Assert
        await AssertModelValidationFailureAsync(
            reservation.Id,
            attendee.Id,
            new { Role = 99 },
            inboxId,
            tokens.Token,
            "Role");

        await AssertAttendeeUntouchedAsync(
            attendee.Id,
            AttendeeRoleEnum.Required);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenRoleIsNotSent()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id);

        string inboxId = Guid.NewGuid().ToString();

        // Act & Assert (body vazio: a propriedade "required" não veio)
        await AssertModelValidationFailureAsync(
            reservation.Id,
            attendee.Id,
            new { },
            inboxId,
            tokens.Token,
            propertyName: null);

        await AssertAttendeeUntouchedAsync(
            attendee.Id,
            AttendeeRoleEnum.Required);
    }

    // ---------------------------------------------------------------
    // Validação dos identificadores
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenReservationIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            "invalid-reservation-id",
            Guid.NewGuid().ToString(),
            dto,
            tokens.Token,
            inboxId);

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
    public async Task Update_ReturnsBadRequest_WhenAttendeeIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            Guid.NewGuid().ToString(),
            "invalid-attendee-id",
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Attendee ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Reserva e participante
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            dto,
            tokens.Token,
            inboxId);

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
    public async Task Update_ReturnsNotFound_WhenAttendeeDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            Guid.NewGuid().ToString(),
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Attendee not found.");
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenAttendeeBelongsToAnotherReservation()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var reservationA = await CreateReservationHttp(tokens);
        var reservationB = await CreateReservationHttp(tokens);

        var attendeeUser = await CreateAttendeeUserAsync();

        // O participante pertence à reserva A...
        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservationA.Id,
            attendeeUser.Id);

        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act (...mas a rota aponta para a reserva B, também do organizador)
        using var response = await SendUpdateAsync(
            reservationB.Id,
            attendee.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Attendee not found.");

        await AssertAttendeeUntouchedAsync(
            attendee.Id,
            AttendeeRoleEnum.Required);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenReservationIsCancelled()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id);

        await CancelReservationHttp(tokens, reservation.Id);

        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            attendee.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Reservation is cancelled.");

        await AssertAttendeeUntouchedAsync(
            attendee.Id,
            AttendeeRoleEnum.Required);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenReservationHasAlreadyEnded()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id);

        await MoveReservationToThePastAsync(reservation.Id);

        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            attendee.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Reservation has already ended.");

        await AssertAttendeeUntouchedAsync(
            attendee.Id,
            AttendeeRoleEnum.Required);
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            masterTokens,
            reservation.Id,
            attendeeUser.Id);

        // Usuário recém-cadastrado: sem nenhuma permissão.
        var (_, tokens) = await RegisterUserAsync();

        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            attendee.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AssertAttendeeUntouchedAsync(
            attendee.Id,
            AttendeeRoleEnum.Required);
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            masterTokens,
            reservation.Id,
            attendeeUser.Id);

        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            attendee.Id,
            dto,
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertAttendeeUntouchedAsync(
            attendee.Id,
            AttendeeRoleEnum.Required);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id);

        var dto = NewUpdateDto(AttendeeRoleEnum.Optional);

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            attendee.Id,
            dto,
            tokens.Token,
            inboxId: null);

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

        await AssertAttendeeUntouchedAsync(
            attendee.Id,
            AttendeeRoleEnum.Required);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var firstUser = await CreateAttendeeUserAsync();
        var secondUser = await CreateAttendeeUserAsync();

        var first = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            firstUser.Id);

        var second = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            secondUser.Id);

        string inboxId = Guid.NewGuid().ToString();

        // Primeira atualização.
        using (var firstResponse = await SendUpdateAsync(
                   reservation.Id,
                   first.Id,
                   NewUpdateDto(AttendeeRoleEnum.Optional),
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda atualização (outro participante) reutilizando o mesmo Inbox.
        using var response = await SendUpdateAsync(
            reservation.Id,
            second.Id,
            NewUpdateDto(AttendeeRoleEnum.Optional),
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        // Só a primeira atualização foi aplicada.
        var persistedFirst = await GetAttendeeByIdAsync(first.Id);
        persistedFirst.Role.Should().Be(AttendeeRoleEnum.Optional);

        await AssertAttendeeUntouchedAsync(
            second.Id,
            AttendeeRoleEnum.Required);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static string UpdateUrl(string reservationId, string attendeeId)
        => $"/api/v1/reservations/{Uri.EscapeDataString(reservationId)}" +
           $"/attendees/{Uri.EscapeDataString(attendeeId)}";

    private static UpdateReservationAttendeeDto NewUpdateDto(
        AttendeeRoleEnum role)
    {
        return new UpdateReservationAttendeeDto
        {
            Role = role
        };
    }

    private async Task<HttpResponseMessage> SendUpdateAsync(
        string reservationId,
        string attendeeId,
        object body,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            UpdateUrl(reservationId, attendeeId))
        {
            Content = JsonContent.Create(body, body.GetType())
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

    /// <summary>
    /// O 400 de validação do model acontece antes da action,
    /// então nenhuma linha de Inbox deve existir.
    /// </summary>
    private async Task AssertModelValidationFailureAsync(
        string reservationId,
        string attendeeId,
        object body,
        string inboxId,
        string accessToken,
        string? propertyName)
    {
        using var response = await SendUpdateAsync(
            reservationId,
            attendeeId,
            body,
            accessToken,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.ErrorValids.Should().NotBeNullOrEmpty();

        if (propertyName is not null)
        {
            http.ErrorValids!.Keys.Should().Contain(key =>
                key.EndsWith(
                    propertyName,
                    StringComparison.OrdinalIgnoreCase));
        }

        await AssertInboxNotCreatedAsync(inboxId);
    }

    /// <summary>
    /// Confere que o participante continua exatamente como foi criado:
    /// mesmo papel, Version 1 e sem UpdatedAt.
    /// </summary>
    private async Task AssertAttendeeUntouchedAsync(
        string attendeeId,
        AttendeeRoleEnum expectedRole)
    {
        var persisted = await GetAttendeeByIdAsync(attendeeId);

        persisted.Role.Should().Be(expectedRole);
        persisted.Version.Should().Be(1);
        persisted.UpdatedAt.Should().BeNull();
    }
}