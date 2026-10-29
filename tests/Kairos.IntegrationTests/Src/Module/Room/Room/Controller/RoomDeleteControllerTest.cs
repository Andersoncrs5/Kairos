using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.Room.Controller;

[Collection("Integration Tests")]
public sealed class RoomDeleteControllerTest : BaseIntegrationTest
{
    private const string DeleteUrl = "/api/v1/rooms";

    private readonly ITestOutputHelper _output;

    public RoomDeleteControllerTest(
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
    public async Task Delete_ReturnsOk_AndRemovesRoomAndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            room.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Room deleted");
        http.Data.Should().BeTrue();

        // Verifica a exclusão física da sala.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == room.Id);

        persisted.Should().BeFalse();

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            "invalid-uuid",
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            nonexistentId,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room doesn't exist.");
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenUserLacksDeletePermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var room = await CreateRoomHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            room.Id,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A sala deve continuar persistida.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == room.Id);

        persisted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            Guid.NewGuid().ToString(),
            accessToken: null,
            inboxId: inbox.Id);

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
        var room = await CreateRoomHttp(tokens);

        // Act
        using var response = await SendDeleteAsync(
            room.Id,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        // A action não deve excluir a sala sem o header obrigatório.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == room.Id);

        persisted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            room.Id,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Inbox is not available for processing.");

        // A sala deve continuar persistida.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == room.Id);

        persisted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        // Primeira exclusão: deve ser bem-sucedida.
        using (var firstResponse = await SendDeleteAsync(
                   room.Id,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().BeTrue();
        }

        // Segunda requisição reutiliza o mesmo Inbox.
        using var response = await SendDeleteAsync(
            room.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        // A sala permanece excluída após a primeira operação.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == room.Id);

        persisted.Should().BeFalse();

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Helper HTTP
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