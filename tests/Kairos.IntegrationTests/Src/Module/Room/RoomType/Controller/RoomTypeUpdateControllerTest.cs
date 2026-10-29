using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomType.Controller;

[Collection("Integration Tests")]
public sealed class RoomTypeUpdateControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public RoomTypeUpdateControllerTest(
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
    public async Task Update_ReturnsOk_AndPersistsRoomType()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = $"Updated Room Type {Guid.NewGuid():N}",
            Description = "Updated by integration test.",
            IsActive = false
        };

        using var response = await SendUpdateAsync(
            roomType.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Room type updated successfully");
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(roomType.Id);
        http.Data.Name.Should().Be(dto.Name);
        http.Data.Description.Should().Be(dto.Description);
        http.Data.IsActive.Should().BeFalse();

        // Verifica a persistência da atualização.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomTypeModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == roomType.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(dto.Name);
        persisted.Description.Should().Be(dto.Description);
        persisted.IsActive.Should().BeFalse();

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenOnlyNameIsUpdated()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = $"Renamed Room Type {Guid.NewGuid():N}"
        };

        using var response = await SendUpdateAsync(
            roomType.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.Id.Should().Be(roomType.Id);
        http.Data.Name.Should().Be(dto.Name);

        // Campos omitidos não devem ser alterados.
        http.Data.Description.Should().Be(roomType.Description);
        http.Data.IsActive.Should().Be(roomType.IsActive);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = "Updated Room Type"
        };

        using var response = await SendUpdateAsync(
            "invalid-uuid",
            dto,
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

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNameIsEmpty()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = "   "
        };

        using var response = await SendUpdateAsync(
            roomType.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room type name cannot be empty.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = new string('a', 101)
        };

        using var response = await SendUpdateAsync(
            roomType.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A resposta pode conter um objeto de erros de validação em Data.
        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenRoomTypeDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = "Updated Room Type"
        };

        using var response = await SendUpdateAsync(
            nonexistentId,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room type not found.");
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenNameAlreadyExists()
    {
        var tokens = await LoginUserMaster();

        var roomTypeToUpdate = await CreateRoomTypeHttp(tokens);
        var existingRoomType = await CreateRoomTypeHttp(tokens);

        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = existingRoomType.Name
        };

        using var response = await SendUpdateAsync(
            roomTypeToUpdate.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            $"Room type with the name '{existingRoomType.Name}' already exists.");
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksUpdatePermission()
    {
        var masterTokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = $"Unauthorized Update {Guid.NewGuid():N}"
        };

        using var response = await SendUpdateAsync(
            roomType.Id,
            dto,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = "Updated Room Type"
        };

        using var response = await SendUpdateAsync(
            Guid.NewGuid().ToString(),
            dto,
            accessToken: null,
            inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);

        var dto = new UpdateRoomTypeDto
        {
            Name = $"Updated Room Type {Guid.NewGuid():N}"
        };

        using var response = await SendUpdateAsync(
            roomType.Id,
            dto,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomTypeDto
        {
            Name = $"Updated Room Type {Guid.NewGuid():N}"
        };

        using var response = await SendUpdateAsync(
            roomType.Id,
            dto,
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
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomTypeDto
        {
            Name = $"Updated Room Type {Guid.NewGuid():N}",
            Description = "Updated by integration test.",
            IsActive = false
        };

        // Primeira atualização: deve ser processada com sucesso.
        using (var firstResponse = await SendUpdateAsync(
                   roomType.Id,
                   dto,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<RoomTypeDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
            firstHttp.Data!.Name.Should().Be(dto.Name);
        }

        // Segunda requisição reutiliza o Inbox já processado.
        using var response = await SendUpdateAsync(
            roomType.Id,
            dto,
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

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendUpdateAsync(
        string id,
        UpdateRoomTypeDto dto,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/room-types/{Uri.EscapeDataString(id)}")
        {
            Content = JsonContent.Create(dto)
        };

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