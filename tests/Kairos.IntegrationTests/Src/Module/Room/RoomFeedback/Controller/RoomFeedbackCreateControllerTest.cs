
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomFeedback.Controller;

[Collection("Integration Tests")]
public sealed class RoomFeedbackCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/reservations";

    private readonly ITestOutputHelper _output;

    public RoomFeedbackCreateControllerTest(
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
    public async Task Create_ReturnsCreated_AndPersistsFeedbackAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var dto = NewFeedbackDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
        http.Data.ReservationId.Should().Be(reservation.Id);
        http.Data.RoomId.Should().Be(reservation.RoomId);
        http.Data.UserId.Should().Be(reservation.UserId);
        http.Data.Rating.Should().Be(dto.Rating);
        http.Data.Comment.Should().Be(dto.Comment);
        http.Data.IssueType.Should().Be(dto.IssueType);

        // Verify persistence.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomFeedbackModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(feedback =>
                feedback.ReservationId == reservation.Id &&
                feedback.UserId == reservation.UserId);

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(http.Data.Id);
        persisted.ReservationId.Should().Be(reservation.Id);
        persisted.RoomId.Should().Be(reservation.RoomId);
        persisted.UserId.Should().Be(reservation.UserId);
        persisted.Rating.Should().Be(dto.Rating);
        persisted.Comment.Should().Be(dto.Comment);
        persisted.IssueType.Should().Be(dto.IssueType);
        persisted.Version.Should().Be(1);
        persisted.CreatedAt.Should().NotBe(default);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Accepted attendee
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsCreated_WhenRequesterIsAcceptedAttendee()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        // Register another user to become the reservation organizer.
        var (_, organizerTokens) = await RegisterUserAsync();

        string organizerId = GetUserIdFromToken(organizerTokens.Token);
        string requesterId = GetUserIdFromToken(tokens.Token);

        // The master user will be an accepted attendee,
        // not the reservation organizer.
        await ChangeReservationOrganizerAsync(
            reservation.Id,
            organizerId);

        await CreateReservationAttendeeAsync(
            reservation.Id,
            requesterId,
            organizerId,
            AttendeeStatusEnum.Accepted);

        var dto = NewFeedbackDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.ReservationId.Should().Be(reservation.Id);
        http.Data.UserId.Should().Be(requesterId);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // DTO validation
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Create_ReturnsBadRequest_WhenRatingIsOutOfRange(
        int rating)
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var dto = NewFeedbackDto(rating: rating);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotBeNullOrWhiteSpace();

        await AssertFeedbackDoesNotExistAsync(reservation.Id);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCommentExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var dto = NewFeedbackDto(
            comment: new string('a', 1001));

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertFeedbackDoesNotExistAsync(reservation.Id);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenIssueTypeIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var dto = NewFeedbackDto(
            issueType: (RoomFeedbackIssueTypeEnum)999);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertFeedbackDoesNotExistAsync(reservation.Id);
    }

    // ---------------------------------------------------------------
    // Identifier validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenReservationIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewFeedbackDto();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            "invalid-uuid",
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Reservation ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Reservation validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string reservationId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = NewFeedbackDto();

        // Act
        using var response = await SendCreateAsync(
            reservationId,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Reservation not found.");

        await AssertFeedbackDoesNotExistAsync(reservationId);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenReservationHasNotEnded()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = NewFeedbackDto();
        string inboxId = Guid.NewGuid().ToString();

        // CreateReservationHttp creates a future reservation.

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Reservation has not ended yet.");

        await AssertFeedbackDoesNotExistAsync(reservation.Id);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenReservationIsCancelled()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);
        await CancelReservationAsync(reservation.Id);

        var dto = NewFeedbackDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Cancelled reservations cannot be reviewed.");

        await AssertFeedbackDoesNotExistAsync(reservation.Id);
    }

    // ---------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsForbidden_WhenRequesterIsNotOrganizerOrAttendee()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var (_, organizerTokens) = await RegisterUserAsync();

        string organizerId = GetUserIdFromToken(organizerTokens.Token);

        // Change the organizer so the authenticated user
        // is neither organizer nor attendee.
        await ChangeReservationOrganizerAsync(
            reservation.Id,
            organizerId);

        var dto = NewFeedbackDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Only the organizer or attendees who accepted the invitation can review this reservation.");

        await AssertFeedbackDoesNotExistAsync(reservation.Id);
    }

    [Fact]
    public async Task Create_ReturnsForbidden_WhenAttendeeHasNotAcceptedInvitation()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var (_, organizerTokens) = await RegisterUserAsync();

        string organizerId = GetUserIdFromToken(organizerTokens.Token);
        string requesterId = GetUserIdFromToken(tokens.Token);

        await ChangeReservationOrganizerAsync(
            reservation.Id,
            organizerId);

        await CreateReservationAttendeeAsync(
            reservation.Id,
            requesterId,
            organizerId,
            AttendeeStatusEnum.Pending);

        var dto = NewFeedbackDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Only the organizer or attendees who accepted the invitation can review this reservation.");

        await AssertFeedbackDoesNotExistAsync(reservation.Id);
    }

    // ---------------------------------------------------------------
    // Duplicate feedback
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConflict_WhenUserAlreadyReviewedReservation()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var firstDto = NewFeedbackDto(rating: 5);
        var secondDto = NewFeedbackDto(rating: 4);

        string firstInboxId = Guid.NewGuid().ToString();
        string secondInboxId = Guid.NewGuid().ToString();

        // First feedback.
        using (var firstResponse = await SendCreateAsync(
                   reservation.Id,
                   firstDto,
                   firstInboxId,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Act - Second feedback for the same user and reservation.
        using var response = await SendCreateAsync(
            reservation.Id,
            secondDto,
            secondInboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "You have already reviewed this reservation.");

        using var scope = CreateScope();

        var count = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomFeedbackModel>()
            .AsNoTracking()
            .CountAsync(feedback =>
                feedback.ReservationId == reservation.Id &&
                feedback.UserId == reservation.UserId);

        count.Should().Be(1);

        await AssertInboxProcessedAsync(firstInboxId);
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var dto = NewFeedbackDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            reservation.Id,
            dto,
            inboxId,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertFeedbackDoesNotExistAsync(reservation.Id);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var dto = NewFeedbackDto();

        // Act
        using var response = await SendCreateAsync(
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

        await AssertFeedbackDoesNotExistAsync(reservation.Id);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        string inboxId = Guid.NewGuid().ToString();

        var firstDto = NewFeedbackDto(rating: 5);
        var secondDto = NewFeedbackDto(rating: 4);

        // First request.
        using (var firstResponse = await SendCreateAsync(
                   reservation.Id,
                   firstDto,
                   inboxId,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Act - Reuse the same Inbox ID with different content.
        using var response = await SendCreateAsync(
            reservation.Id,
            secondDto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        using var scope = CreateScope();

        var count = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomFeedbackModel>()
            .AsNoTracking()
            .CountAsync(feedback =>
                feedback.ReservationId == reservation.Id &&
                feedback.UserId == reservation.UserId);

        count.Should().Be(1);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        string reservationId,
        CreateRoomFeedbackDto dto,
        string? inboxId,
        string? accessToken)
    {
        string url =
            $"{CreateUrl}/{Uri.EscapeDataString(reservationId)}/feedbacks";

        return SendAsync(
            url,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private static CreateRoomFeedbackDto NewFeedbackDto(
        int rating = 5,
        string? comment = "Created by integration tests.",
        RoomFeedbackIssueTypeEnum? issueType =
            RoomFeedbackIssueTypeEnum.Equipment)
    {
        return new CreateRoomFeedbackDto
        {
            Rating = rating,
            Comment = comment,
            IssueType = issueType
        };
    }

    private async Task MarkReservationAsEndedAsync(
        string reservationId)
    {
        DateTimeOffset endsAt = DateTimeOffset.UtcNow.AddHours(-1);
        DateTimeOffset startsAt = endsAt.AddHours(-2);

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        int affected = await db.Set<ReservationModel>()
            .Where(reservation => reservation.Id == reservationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(reservation => reservation.StartsAt, startsAt)
                .SetProperty(reservation => reservation.EndsAt, endsAt));

        affected.Should().Be(1);
    }

    private async Task CancelReservationAsync(string reservationId)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        int affected = await db.Set<ReservationModel>()
            .Where(reservation => reservation.Id == reservationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    reservation => reservation.Status,
                    ReservationStatusEnum.Cancelled));

        affected.Should().Be(1);
    }

    private async Task ChangeReservationOrganizerAsync(
        string reservationId,
        string organizerId)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        int affected = await db.Set<ReservationModel>()
            .Where(reservation => reservation.Id == reservationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    reservation => reservation.UserId,
                    organizerId));

        affected.Should().Be(1);
    }

    private async Task CreateReservationAttendeeAsync(
        string reservationId,
        string userId,
        string inviterId,
        AttendeeStatusEnum status)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        ReservationAttendeeModel attendee = new()
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = reservationId,
            UserId = userId,
            InvitedBy = inviterId,
            Role = AttendeeRoleEnum.Required,
            Status = status,
            RespondedAt = status == AttendeeStatusEnum.Accepted
                ? DateTimeOffset.UtcNow
                : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Add(attendee);

        await db.SaveChangesAsync();
    }

    private async Task AssertFeedbackDoesNotExistAsync(
        string reservationId)
    {
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomFeedbackModel>()
            .AsNoTracking()
            .AnyAsync(feedback =>
                feedback.ReservationId == reservationId);

        exists.Should().BeFalse();
    }

    private static string GetUserIdFromToken(string accessToken)
    {
        JwtSecurityToken jwt =
            new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        string? userId =
            jwt.Claims.FirstOrDefault(claim =>
                claim.Type == JwtRegisteredClaimNames.Sid)?.Value
            ?? jwt.Claims.FirstOrDefault(claim =>
                claim.Type == JwtRegisteredClaimNames.Sub)?.Value;

        return userId
            ?? throw new InvalidOperationException(
                "The JWT does not contain the authenticated user ID.");
    }
}
