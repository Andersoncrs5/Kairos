
using System.Net;
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Common.Enums;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomFeedback.Controller;

[Collection("Integration Tests")]
public sealed class RoomFeedbackUpdateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/reservations";
    private const string UpdateUrl = "/api/v1/room-feedbacks";

    private readonly ITestOutputHelper _output;

    public RoomFeedbackUpdateControllerTest(
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
    public async Task Update_ReturnsUpdatedFeedback_WhenRequesterIsAuthor()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        var dto = NewUpdateDto(
            rating: 4,
            comment: "Updated feedback comment.",
            issueType: RoomFeedbackIssueTypeEnum.Cleanliness);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            feedback.Id,
            dto,
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

        http.Message.Should().Be("Room feedback updated successfully");
        http.TraceId.Should().Be(inboxId);

        http.Data!.Id.Should().Be(feedback.Id);
        http.Data.ReservationId.Should().Be(feedback.ReservationId);
        http.Data.RoomId.Should().Be(feedback.RoomId);
        http.Data.UserId.Should().Be(feedback.UserId);
        http.Data.Rating.Should().Be(dto.Rating);
        http.Data.Comment.Should().Be(dto.Comment);
        http.Data.IssueType.Should().Be(dto.IssueType);

        await AssertFeedbackUpdatedAsync(feedback.Id, dto);
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ClearsCommentAndIssueType_WhenValuesAreNull()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        feedback.Comment.Should().NotBeNull();
        feedback.IssueType.Should().NotBeNull();

        var dto = NewUpdateDto(
            rating: 3,
            comment: null,
            issueType: null);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            feedback.Id,
            dto,
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

        http.Data!.Rating.Should().Be(3);
        http.Data.Comment.Should().BeNull();
        http.Data.IssueType.Should().BeNull();

        await AssertFeedbackUpdatedAsync(feedback.Id, dto);
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsSuccessWithoutChangingFeedback_WhenDataIsUnchanged()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        var dto = NewUpdateDto(
            rating: feedback.Rating,
            comment: feedback.Comment,
            issueType: feedback.IssueType);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            feedback.Id,
            dto,
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

        http.Data!.Id.Should().Be(feedback.Id);
        http.Data.Rating.Should().Be(feedback.Rating);
        http.Data.Comment.Should().Be(feedback.Comment);
        http.Data.IssueType.Should().Be(feedback.IssueType);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsForbidden_WhenRequesterIsNotAuthor()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var (_, otherUserTokens) = await RegisterUserAsync();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        string otherUserId = otherUserTokens.User.Id;

        await ChangeFeedbackAuthorAsync(
            feedback.Id,
            otherUserId);

        var dto = NewUpdateDto(
            rating: 2,
            comment: "Unauthorized update attempt.",
            issueType: RoomFeedbackIssueTypeEnum.Noise);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            feedback.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Only the author can update this room feedback.");

        await AssertFeedbackUnchangedAsync(feedback);
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenFeedbackDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string feedbackId = Guid.NewGuid().ToString();

        var dto = NewUpdateDto();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            feedbackId,
            dto,
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

        http.Message.Should().Be("Error updating room feedback");

        http.Errors.Should().Contain(
            "Room feedback not found.");
    }

    // ---------------------------------------------------------------
    // Identifier validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenFeedbackIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewUpdateDto();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            "invalid-uuid",
            dto,
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
    // DTO validation
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Update_ReturnsBadRequest_WhenRatingIsOutOfRange(
        int rating)
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        var dto = NewUpdateDto(rating: rating);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            feedback.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        
        await AssertFeedbackUnchangedAsync(feedback);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenCommentExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        var dto = NewUpdateDto(
            comment: new string('a', 1001));

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            feedback.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        

        await AssertFeedbackUnchangedAsync(feedback);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIssueTypeIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        var dto = NewUpdateDto(
            issueType: (RoomFeedbackIssueTypeEnum)999);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            feedback.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomFeedbackDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

       
        await AssertFeedbackUnchangedAsync(feedback);
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        string feedbackId = Guid.NewGuid().ToString();

        var dto = NewUpdateDto();

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            feedbackId,
            dto,
            accessToken: null,
            inboxId: inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        string feedbackId = Guid.NewGuid().ToString();

        var dto = NewUpdateDto();

        // Act
        using var response = await SendUpdateAsync(
            feedbackId,
            dto,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var feedback = await CreateFeedbackAsMasterAsync(tokens);

        var dto = NewUpdateDto(
            rating: 4,
            comment: "First update.");

        string inboxId = Guid.NewGuid().ToString();

        // First request
        using (var firstResponse = await SendUpdateAsync(
                   feedback.Id,
                   dto,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp =
                await ReadAsync<RoomFeedbackDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
        }

        // Act - Reuse the same Inbox ID
        using var response = await SendUpdateAsync(
            feedback.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validation helpers
    // ---------------------------------------------------------------

    private static void AssertValidationError(
        RoomFeedbackDto http,
        string property,
        string expectedMessage)
    {
        
    }

    // ---------------------------------------------------------------
    // HTTP helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendUpdateAsync(
        string feedbackId,
        UpdateRoomFeedbackDto dto,
        string? accessToken,
        string? inboxId)
    {
        string url =
            $"{UpdateUrl}/{Uri.EscapeDataString(feedbackId)}";

        return SendAsyncWithMethod(
            url,
            method: HttpMethod.Put,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private async Task<RoomFeedbackDto> CreateFeedbackAsMasterAsync(
        Tokens tokens)
    {
        var reservation = await CreateReservationHttp(tokens);

        await MarkReservationAsEndedAsync(reservation.Id);

        var dto = new CreateRoomFeedbackDto
        {
            Rating = 5,
            Comment = "Created by integration tests.",
            IssueType = RoomFeedbackIssueTypeEnum.Equipment
        };

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

    private static UpdateRoomFeedbackDto NewUpdateDto(
        int rating = 4,
        string? comment = "Updated by integration tests.",
        RoomFeedbackIssueTypeEnum? issueType =
            RoomFeedbackIssueTypeEnum.Cleanliness)
    {
        return new UpdateRoomFeedbackDto
        {
            Rating = rating,
            Comment = comment,
            IssueType = issueType
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

        int affected = await db.Set<
                Kairos.Api.Modules.Reservations.Reservation.Models.ReservationModel>()
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

    private async Task AssertFeedbackUpdatedAsync(
        string feedbackId,
        UpdateRoomFeedbackDto dto)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var feedback = await db.Set<RoomFeedbackModel>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == feedbackId);

        feedback.Should().NotBeNull();
        feedback!.Rating.Should().Be(dto.Rating);
        feedback.Comment.Should().Be(dto.Comment);
        feedback.IssueType.Should().Be(dto.IssueType);
    }

    private async Task AssertFeedbackUnchangedAsync(
        RoomFeedbackDto original)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var feedback = await db.Set<RoomFeedbackModel>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == original.Id);

        feedback.Should().NotBeNull();
        feedback!.Rating.Should().Be(original.Rating);
        feedback.Comment.Should().Be(original.Comment);
        feedback.IssueType.Should().Be(original.IssueType);
    }
}
