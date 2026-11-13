
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
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
public sealed class RoomFeedbackFindByIdControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/reservations";
    private const string FindByIdUrl = "/api/v1/room-feedbacks";

    private readonly ITestOutputHelper _output;

    public RoomFeedbackFindByIdControllerTest(
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
    public async Task FindById_ReturnsFeedback_WhenRequesterIsAuthor()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            feedback.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Message.Should().Be("Room feedback found");
        http.TraceId.Should().Be(inboxId);

        http.Data!.Id.Should().Be(feedback.Id);
        http.Data.ReservationId.Should().Be(feedback.ReservationId);
        http.Data.RoomId.Should().Be(feedback.RoomId);
        http.Data.UserId.Should().Be(feedback.UserId);
        http.Data.Rating.Should().Be(feedback.Rating);
        http.Data.Comment.Should().Be(feedback.Comment);
        http.Data.IssueType.Should().Be(feedback.IssueType);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Moderator
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsFeedback_WhenRequesterIsModerator()
    {
        // Arrange
        var moderatorTokens = await LoginUserMaster();

        var (_, authorTokens) = await RegisterUserAsync();

        var feedback = await CreateFeedbackAsMasterAsync(
            moderatorTokens);

        string authorId = GetUserIdFromToken(authorTokens.Token);

        // Change the feedback author.
        // The master user will now be a moderator viewing someone else's feedback.
        await ChangeFeedbackAuthorAsync(
            feedback.Id,
            authorId);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            feedback.Id,
            moderatorTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Message.Should().Be("Room feedback found");
        http.TraceId.Should().Be(inboxId);

        http.Data!.Id.Should().Be(feedback.Id);
        http.Data.UserId.Should().Be(authorId);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenFeedbackDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            nonexistentId,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();

        http.Message.Should().Be("Error finding room feedback");
        http.TraceId.Should().Be(inboxId);

        http.Errors.Should().Contain(
            "Room feedback not found.");
    }

    // ---------------------------------------------------------------
    // Identifier validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenFeedbackIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            "invalid-uuid",
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Room feedback ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        string feedbackId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
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
    public async Task FindById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string feedbackId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            feedbackId,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FindById_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // First request.
        using (var firstResponse = await SendFindByIdAsync(
                   feedback.Id,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp =
                await ReadAsync<RoomFeedbackDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
            firstHttp.Data!.Id.Should().Be(feedback.Id);
        }

        // Act - Reuse the same Inbox ID.
        using var response = await SendFindByIdAsync(
            feedback.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // HTTP helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendFindByIdAsync(
        string feedbackId,
        string? accessToken,
        string? inboxId)
    {
        string url =
            $"{FindByIdUrl}/{Uri.EscapeDataString(feedbackId)}";

        return SendAsyncWithMethod(
            url,
            method: HttpMethod.Get,
            accessToken: accessToken,
            inboxId: inboxId);
    }

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
            IssueType = Kairos.Api.Common.Enums.RoomFeedbackIssueTypeEnum.Equipment
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
