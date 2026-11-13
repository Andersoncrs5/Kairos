
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Response;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Reservations.ReservationAttendees.Controller;

[Collection("Integration Tests")]
public sealed class ReservationAttendeesFindByIdControllerTest
    : BaseIntegrationTest
{
    private const string FindByIdPermission =
        "reservations.attendees.read";

    private const string ViewAllPermission =
        "reservations.read";

    private const string FindByIdUrl =
        "/api/v1/reservations";

    private readonly ITestOutputHelper _output;

    public ReservationAttendeesFindByIdControllerTest(
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
    public async Task FindById_ReturnsAttendee_WhenRequesterIsReservationOrganizer()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id,
            AttendeeRoleEnum.Optional);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            reservation.Id,
            attendee.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Reservation attendee found");
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(attendee.Id);
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

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenAttendeeDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        string nonexistentAttendeeId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            reservation.Id,
            nonexistentAttendeeId,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Message.Should().Be(
            "Error finding reservation attendee");
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().BeNull();
        http.Errors.Should().Contain("Attendee not found.");

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenAttendeeBelongsToAnotherReservation()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var firstReservation = await CreateReservationHttp(tokens);
        var secondReservation = await CreateReservationHttp(tokens);

        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            firstReservation.Id,
            attendeeUser.Id);

        string inboxId = Guid.NewGuid().ToString();

        // O participante existe, mas pertence à primeira reserva.
        // A consulta é realizada utilizando a segunda reserva.

        // Act
        using var response = await SendFindByIdAsync(
            secondReservation.Id,
            attendee.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.Errors.Should().Contain("Attendee not found.");

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Privacidade e autorização do UseCase
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsForbidden_WhenUserLacksEndpointPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);

        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            masterTokens,
            reservation.Id,
            attendeeUser.Id);

        // Usuário autenticado sem reservations.attendees.read.
        var (_, requesterTokens) = await RegisterUserAsync();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            reservation.Id,
            attendee.Id,
            requesterTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AssertInboxNotCreatedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validação dos identificadores
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenReservationIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            "invalid-reservation-id",
            Guid.NewGuid().ToString(),
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.Errors.Should().Contain(
            "Reservation ID must be a valid UUID.");

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenAttendeeIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            reservation.Id,
            "invalid-attendee-id",
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationAttendeeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.Errors.Should().Contain(
            "Attendee ID must be a valid UUID.");

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Autenticação
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertInboxNotCreatedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var attendeeUser = await CreateAttendeeUserAsync();

        var attendee = await AddReservationAttendeeHttp(
            tokens,
            reservation.Id,
            attendeeUser.Id);

        // Act
        using var response = await SendFindByIdAsync(
            reservation.Id,
            attendee.Id,
            tokens.Token,
            inboxId: null);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain(InboxHeader);
    }

    [Fact]
    public async Task FindById_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
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

        // First request
        using (var firstResponse = await SendFindByIdAsync(
            reservation.Id,
            attendee.Id,
            tokens.Token,
            inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp =
                await ReadAsync<ReservationAttendeeDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
            firstHttp.Data!.Id.Should().Be(attendee.Id);
        }

        // Second request reuses the same Inbox ID.
        using var response = await SendFindByIdAsync(
            reservation.Id,
            attendee.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static string BuildFindByIdUrl(
        string reservationId,
        string attendeeId)
    {
        return $"{FindByIdUrl}/{Uri.EscapeDataString(reservationId)}" +
               $"/attendees/{Uri.EscapeDataString(attendeeId)}";
    }

    private async Task<HttpResponseMessage> SendFindByIdAsync(
        string reservationId,
        string attendeeId,
        string? accessToken,
        string? inboxId)
    {
        string url = BuildFindByIdUrl(reservationId, attendeeId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            url);

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

    private async Task<UserModel> CreateAttendeeUserAsync()
    {
        using var scope = CreateScope();

        return await CreateUserAsync(scope.ServiceProvider);
    }

    private async Task GrantPermissionAsync(
        string userId,
        string permissionCode)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var permission = await db.Permissions
            .SingleAsync(item => item.Code == permissionCode);

        bool alreadyGranted = await db.PermissionUsers
            .AnyAsync(item =>
                item.UserId == userId &&
                item.PermissionId == permission.Id &&
                item.IsActive);

        if (alreadyGranted)
            return;

        var permissionUser = new PermissionUserModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            PermissionId = permission.Id,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await db.PermissionUsers.AddAsync(permissionUser);
        await db.SaveChangesAsync();
    }
}
