using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomBlockout.Controller;

[Collection("Integration Tests")]
public sealed class RoomBlockoutFindByIdControllerTest : BaseIntegrationTest
{
    private const string FindByIdUrl = "/api/v1/room-blockouts";
    private const string InboxHeader = "X-Inbox-Id";

    private readonly ITestOutputHelper _output;

    public RoomBlockoutFindByIdControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsOk_WhenRoomBlockoutExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindByIdAsync(
            blockout.Id,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inbox.Id);
        http.Message.Should().Be("Room blockout found");
        http.Data.Should().NotBeNull();

        var data = http.Data!;

        data.Id.Should().Be(blockout.Id);
        data.RoomId.Should().Be(blockout.RoomId);
        data.Type.Should().Be(blockout.Type);
        data.Status.Should().Be(RoomBlockoutStatus.Scheduled);
        data.Reason.Should().Be(blockout.Reason);
        data.Notes.Should().Be(blockout.Notes);
        data.StartsAt.Should().Be(blockout.StartsAt);
        data.EndsAt.Should().Be(blockout.EndsAt);
        data.CreatedBy.Should().Be(blockout.CreatedBy);
    }

    // ---------------------------------------------------------------
    // Invalid ID
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenIdIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        const string invalidId = "invalid-id";

        // Act
        using var response = await SendFindByIdAsync(
            invalidId,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.TraceId.Should().Be(inbox.Id);
        http.Data.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenRoomBlockoutDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            nonexistentId,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.TraceId.Should().Be(inbox.Id);
        http.Data.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var inbox = await NewInboxAsync();

        const string nonexistentId =
            "00000000-0000-0000-0000-000000000001";

        // Act
        using var response = await SendFindByIdAsync(
            nonexistentId,
            inbox.Id,
            accessToken: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsForbidden_WhenUserDoesNotHaveReadPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();

        var blockout = await CreateRoomBlockoutHttp(masterTokens);

        // User must not have the rooms.blockouts.read permission.
        var userTokens = await CreateUserHttp();

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindByIdAsync(
            blockout.Id,
            inbox.Id,
            userTokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------
    // HTTP helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendFindByIdAsync(
        string id,
        string inboxId,
        string? accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FindByIdUrl}/{Uri.EscapeDataString(id)}");

        request.Headers.Add(InboxHeader, inboxId);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await Client.SendAsync(request);
    }
}
