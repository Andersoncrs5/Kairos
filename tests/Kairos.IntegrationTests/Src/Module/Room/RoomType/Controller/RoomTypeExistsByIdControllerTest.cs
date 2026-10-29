using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomType.Controller;

[Collection("Integration Tests")]
public sealed class RoomTypeExistsByIdControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public RoomTypeExistsByIdControllerTest(
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
    public async Task ExistsById_ReturnsTrue_WhenRoomTypeExists()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        using var response = await SendExistsByIdAsync(
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
        http.Message.Should().Be("Room type exists");
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task ExistsById_ReturnsFalse_WhenRoomTypeDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();

        using var response = await SendExistsByIdAsync(
            nonexistentId,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
        http.Message.Should().Be("Room type does not exist");
        http.TraceId.Should().NotBeNullOrWhiteSpace();

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
    public async Task ExistsById_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        using var response = await SendExistsByIdAsync(
            "invalid-uuid",
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsById_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        var masterTokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(masterTokens);
        var (_, regularUserTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendExistsByIdAsync(
            roomType.Id,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExistsById_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var inbox = await NewInboxAsync();

        using var response = await SendExistsByIdAsync(
            Guid.NewGuid().ToString(),
            accessToken: null,
            inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);

        using var response = await SendExistsByIdAsync(
            roomType.Id,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
    }

    [Fact]
    public async Task ExistsById_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        using var response = await SendExistsByIdAsync(
            roomType.Id,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

    }

    [Fact]
    public async Task ExistsById_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        // Primeira consulta: deve ser processada com sucesso.
        using (var firstResponse = await SendExistsByIdAsync(
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

        // Segunda consulta reutiliza o Inbox já processado.
        using var response = await SendExistsByIdAsync(
            roomType.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendExistsByIdAsync(
        string id,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/room-types/Exists/id/{Uri.EscapeDataString(id)}");

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