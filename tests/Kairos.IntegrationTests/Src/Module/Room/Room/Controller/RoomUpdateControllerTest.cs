using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.Room.Controller;

[Collection("Integration Tests")]
public sealed class RoomUpdateControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public RoomUpdateControllerTest(
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
    public async Task Update_ReturnsOk_AndPersistsRoom()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Code = $"u{Guid.NewGuid():N}"[..30],
            Name = $"Updated Room {Guid.NewGuid():N}",
            Description = "Updated by integration test.",
            Location = "Updated integration test location",
            Floor = "2",
            Capacity = 16,
            AreaSquareMeters = 42.5m,
            IsAccessible = true,
            IsReservable = false
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Room updated successfully");
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(room.Id);
        http.Data.Code.Should().Be(dto.Code);
        http.Data.Name.Should().Be(dto.Name);
        http.Data.Description.Should().Be(dto.Description);
        http.Data.Location.Should().Be(dto.Location);
        http.Data.Floor.Should().Be(dto.Floor);
        http.Data.Capacity.Should().Be(dto.Capacity);
        http.Data.AreaSquareMeters.Should().Be(dto.AreaSquareMeters);
        
        // Verifica a persistência da atualização.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == room.Id);

        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(dto.Code);
        persisted.Name.Should().Be(dto.Name);
        persisted.Description.Should().Be(dto.Description);
        persisted.Location.Should().Be(dto.Location);
        persisted.Floor.Should().Be(dto.Floor);
        persisted.Capacity.Should().Be(dto.Capacity);
        persisted.AreaSquareMeters.Should().Be(dto.AreaSquareMeters);
        
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenOnlyNameIsUpdated()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = $"Renamed Room {Guid.NewGuid():N}"
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(room.Id);
        http.Data.Name.Should().Be(dto.Name);

        // Campos omitidos devem continuar com os valores anteriores.
        http.Data.Code.Should().Be(room.Code);
        http.Data.RoomTypeId.Should().Be(room.RoomTypeId);
        http.Data.Description.Should().Be(room.Description);
        http.Data.Location.Should().Be(room.Location);
        http.Data.Floor.Should().Be(room.Floor);
        http.Data.Capacity.Should().Be(room.Capacity);
        http.Data.AreaSquareMeters.Should().Be(room.AreaSquareMeters);
        http.Data.IsAccessible.Should().Be(room.IsAccessible);
        http.Data.IsReservable.Should().Be(room.IsReservable);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenRoomTypeIsChanged()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var newRoomType = await CreateRoomTypeHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        room.RoomTypeId.Should().NotBe(newRoomType.Id);

        var dto = new UpdateRoomDto
        {
            RoomTypeId = newRoomType.Id
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.Id.Should().Be(room.Id);
        http.Data.RoomTypeId.Should().Be(newRoomType.Id);

        // Confirma que a mudança do tipo foi persistida.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == room.Id);

        persisted.Should().NotBeNull();
        persisted!.RoomTypeId.Should().Be(newRoomType.Id);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = "Updated Room"
        };

        using var response = await SendUpdateAsync(
            "invalid-uuid",
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenRoomTypeIdIsNotValidUuid()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            RoomTypeId = "invalid-uuid"
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room type ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Validação dos campos
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenCodeIsEmpty()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Code = "   "
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room code cannot be empty.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNameIsEmpty()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = "   "
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room name cannot be empty.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenLocationIsEmpty()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        UpdateRoomDto dto = new UpdateRoomDto
        {
            Location = "   "
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room location cannot be empty.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenCodeExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        UpdateRoomDto dto = new UpdateRoomDto
        {
            Code = new string('a', 31)
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        RoomDto room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        UpdateRoomDto dto = new UpdateRoomDto
        {
            Name = new string('a', 121)
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = "Updated Room"
        };

        using var response = await SendUpdateAsync(
            nonexistentId,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room not found.");
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenRoomTypeDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var nonexistentRoomTypeId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            RoomTypeId = nonexistentRoomTypeId
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room type not found.");
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenCodeAlreadyExists()
    {
        var tokens = await LoginUserMaster();

        var roomToUpdate = await CreateRoomHttp(tokens);
        var existingRoom = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Code = existingRoom.Code
        };

        using var response = await SendUpdateAsync(
            roomToUpdate.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            $"Room with the code '{existingRoom.Code}' already exists.");
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksUpdatePermission()
    {
        var masterTokens = await LoginUserMaster();
        var room = await CreateRoomHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = $"Unauthorized Update {Guid.NewGuid():N}"
        };

        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = "Updated Room"
        };

        using var response = await SendUpdateAsync(
            Guid.NewGuid().ToString(),
            dto,
            accessToken: null,
            inboxId: inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var dto = new UpdateRoomDto
        {
            Name = $"Updated Room {Guid.NewGuid():N}"
        };

        using var response = await SendUpdateAsync(
            room.Id,
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

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendUpdateAsync(
        string id,
        UpdateRoomDto dto,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/rooms/{Uri.EscapeDataString(id)}")
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