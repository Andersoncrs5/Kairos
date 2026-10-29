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
public sealed class RoomTypeFindByIdControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public RoomTypeFindByIdControllerTest(
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
    public async Task FindById_ReturnsOk_WhenRoomTypeExists()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        using var response = await SendFindByIdAsync(
            roomType.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Room type found");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(roomType.Id);
        http.Data.Name.Should().Be(roomType.Name);
        http.Data.Description.Should().Be(roomType.Description);
        http.Data.IsActive.Should().Be(roomType.IsActive);

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
    public async Task FindById_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        using var response = await SendFindByIdAsync(
            "invalid-uuid",
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenRoomTypeDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();

        using var response = await SendFindByIdAsync(
            nonexistentId,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room type doesn't exist.");
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        var masterTokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(masterTokens);
        var (_, regularUserTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendFindByIdAsync(
            roomType.Id,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FindById_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var inbox = await NewInboxAsync();

        using var response = await SendFindByIdAsync(
            Guid.NewGuid().ToString(),
            accessToken: null,
            inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);

        using var response = await SendFindByIdAsync(
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
    public async Task FindById_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByIdAsync(
            roomType.Id,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Inbox is not available for processing.");
    }

    [Fact]
    public async Task FindById_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        // Primeira consulta: deve ser processada com sucesso.
        using (var firstResponse = await SendFindByIdAsync(
                   roomType.Id,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda consulta reutiliza o mesmo Inbox.
        using var response = await SendFindByIdAsync(
            roomType.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendFindByIdAsync(
        string id,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
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