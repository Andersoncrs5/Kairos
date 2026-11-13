using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.Models;
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
public sealed class RoomFeedbackDeleteControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/reservations";
    private const string DeleteUrl = "/api/v1/room-feedbacks";

    private readonly ITestOutputHelper _output;

    public RoomFeedbackDeleteControllerTest(
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
    public async Task Delete_ReturnsOk_AndRemovesFeedback_WhenRequesterIsAuthor()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            feedback.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();

        http.Message.Should().Be("Room feedback deleted successfully");
        http.TraceId.Should().Be(inboxId);

        await AssertFeedbackDoesNotExistAsync(feedback.Id);
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Moderator
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsOk_AndRemovesFeedback_WhenRequesterIsModerator()
    {
        // Arrange
        var moderatorTokens = await LoginUserMaster();

        var (_, authorTokens) = await RegisterUserAsync();

        var feedback = await CreateFeedbackAsMasterAsync(
            moderatorTokens);

        string authorId = GetUserIdFromToken(authorTokens.Token);

        // Change the feedback author.
        // The master user will now be a moderator deleting someone else's feedback.
        await ChangeFeedbackAuthorAsync(
            feedback.Id,
            authorId);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            feedback.Id,
            moderatorTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();

        http.Message.Should().Be("Room feedback deleted successfully");
        http.TraceId.Should().Be(inboxId);

        await AssertFeedbackDoesNotExistAsync(feedback.Id);
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenRequesterIsNotAuthorOrModerator()
    {
        // Arrange
        var authorTokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(authorTokens);

        var (_, requesterTokens) = await RegisterUserAsync();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            feedback.Id,
            requesterTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AssertFeedbackExistsAsync(feedback.Id);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenFeedbackDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string feedbackId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            feedbackId,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();

        http.Message.Should().Be("Error deleting room feedback");
        http.TraceId.Should().Be(inboxId);

        http.Errors.Should().Contain(
            "Room feedback not found.");
    }

    // ---------------------------------------------------------------
    // Identifier validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenFeedbackIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            "invalid-uuid",
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();

        http.Errors.Should().Contain(
            "Room feedback ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        string feedbackId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            feedbackId,
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string feedbackId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            feedbackId,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // First request.
        using (var firstResponse = await SendDeleteAsync(
                   feedback.Id,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool?>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().BeTrue();
        }

        // Act - Reuse the same Inbox ID.
        using var response = await SendDeleteAsync(
            feedback.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertFeedbackDoesNotExistAsync(feedback.Id);
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // HTTP helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendDeleteAsync(
        string feedbackId,
        string? accessToken,
        string? inboxId)
    {
        string url =
            $"{DeleteUrl}/{Uri.EscapeDataString(feedbackId)}";

        return SendAsyncWithMethod(
            url,
            method: HttpMethod.Delete,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    // ---------------------------------------------------------------
    // Feedback helpers
    // ---------------------------------------------------------------

    private async Task<RoomFeedbackDto> CreateFeedbackAsMasterAsync(
        Tokens tokens)
    {
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var dto = NewFeedbackDto();

        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendAsync(
            $"{CreateUrl}/{reservation.Id}/feedbacks",
            body: dto,
            accessToken: tokens.Token,
            inboxId: inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        await AssertInboxProcessedAsync(inboxId);

        return http.Data!;
    }

    private static CreateRoomFeedbackDto NewFeedbackDto()
    {
        return new CreateRoomFeedbackDto
        {
            Rating = 5,
            Comment = "Created by integration tests.",
            IssueType = RoomFeedbackIssueTypeEnum.Equipment
        };
    }

    // ---------------------------------------------------------------
    // Database helpers
    // ---------------------------------------------------------------

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
                .SetProperty(
                    reservation => reservation.StartsAt,
                    startsAt)
                .SetProperty(
                    reservation => reservation.EndsAt,
                    endsAt));

        affected.Should().Be(1);
    }

    private async Task ChangeFeedbackAuthorAsync(
        string feedbackId,
        string authorId)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        int affected = await db.Set<RoomFeedbackModel>()
            .Where(feedback => feedback.Id == feedbackId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    feedback => feedback.UserId,
                    authorId));

        affected.Should().Be(1);
    }

    private async Task AssertFeedbackExistsAsync(
        string feedbackId)
    {
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomFeedbackModel>()
            .AsNoTracking()
            .AnyAsync(feedback => feedback.Id == feedbackId);

        exists.Should().BeTrue();
    }

    private async Task AssertFeedbackDoesNotExistAsync(
        string feedbackId)
    {
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomFeedbackModel>()
            .AsNoTracking()
            .AnyAsync(feedback => feedback.Id == feedbackId);

        exists.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // JWT helper
    // ---------------------------------------------------------------

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