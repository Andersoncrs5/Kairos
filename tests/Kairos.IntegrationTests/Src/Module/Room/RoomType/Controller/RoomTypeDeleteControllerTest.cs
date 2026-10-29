using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomType.Controller;

[Collection("Integration Tests")]
public sealed class RoomTypeDeleteControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public RoomTypeDeleteControllerTest(
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
    public async Task Delete_ReturnsOk_AndRemovesRoomType()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            roomType.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("Room type deleted");
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        // Verifica a remoção no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == roomType.Id);

        exists.Should().BeFalse();

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
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            "invalid-uuid",
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

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
    public async Task Delete_ReturnsNotFound_WhenRoomTypeDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            nonexistentId,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room type doesn't exist.");
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenUserLacksDeletePermission()
    {
        var masterTokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            roomType.Id,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // O tipo de sala deve continuar existindo.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == roomType.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            Guid.NewGuid().ToString(),
            accessToken: null,
            inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);

        using var response = await SendDeleteAsync(
            roomType.Id,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        // A exclusão não deve ocorrer sem o cabeçalho obrigatório.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == roomType.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        using var response = await SendDeleteAsync(
            roomType.Id,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Inbox is not available for processing.");

        // A exclusão não deve ocorrer com um Inbox inexistente.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == roomType.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        // Primeira exclusão: deve ser processada com sucesso.
        using (var firstResponse = await SendDeleteAsync(
                   roomType.Id,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().BeTrue();
        }

        // Segunda requisição reutiliza o Inbox já processado.
        using var response = await SendDeleteAsync(
            roomType.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Inbox is not available for processing.");

        // A primeira exclusão permanece efetivada.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == roomType.Id);

        exists.Should().BeFalse();

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
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
            $"/api/v1/room-types/{Uri.EscapeDataString(id)}");

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