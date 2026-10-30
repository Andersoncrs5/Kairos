using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomBlockout.Controller;

[Collection("Integration Tests")]
public sealed class RoomBlockoutDeleteControllerTest : BaseIntegrationTest
{
    private const string DeleteUrl = "/api/v1/room-blockouts";
    private const string InboxHeader = "X-Inbox-Id";

    private readonly ITestOutputHelper _output;

    public RoomBlockoutDeleteControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsOk_WhenRoomBlockoutExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            blockout.Id,
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
        http.Message.Should().Be("Room blockout deleted");
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        // Verifica a exclusão no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == blockout.Id);

        exists.Should().BeFalse();

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenIdIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        const string invalidId = "invalid-id";

        // Act
        using var response = await SendDeleteAsync(
            invalidId,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.Message.Should().Be("Error deleting room blockout");
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenRoomBlockoutDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            nonexistentId,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.Message.Should().Be("Error deleting room blockout");
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var inbox = await NewInboxAsync();
        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            nonexistentId,
            accessToken: null,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenUserLacksDeletePermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();

        var blockout = await CreateRoomBlockoutHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            blockout.Id,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);

        // Act
        using var response = await SendDeleteAsync(
            blockout.Id,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);

        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            blockout.Id,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // O registro não deve ter sido excluído.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == blockout.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        // Primeira exclusão: deve ser processada com sucesso.
        using (var firstResponse = await SendDeleteAsync(
                   blockout.Id,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().BeTrue();
        }

        // Segunda exclusão utilizando o Inbox já processado.
        using var response = await SendDeleteAsync(
            blockout.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
    }

    // ---------------------------------------------------------------
    // HTTP helper
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendDeleteAsync(
        string id,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{DeleteUrl}/{Uri.EscapeDataString(id)}");

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }
}
