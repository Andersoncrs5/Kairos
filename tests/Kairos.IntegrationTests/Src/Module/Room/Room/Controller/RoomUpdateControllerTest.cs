using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
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
    private const string UpdateUrl = "/api/v1/rooms";

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
    public async Task Update_ReturnsOk_AndPersistsRoomAndInbox()
    {
        // Arrange
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

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
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
        http.Data.IsAccessible.Should().Be(dto.IsAccessible!.Value);
        http.Data.IsReservable.Should().Be(dto.IsReservable!.Value);

        // Verifica a persistência da atualização no banco.
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
        persisted.IsAccessible.Should().Be(dto.IsAccessible!.Value);
        persisted.IsReservable.Should().Be(dto.IsReservable!.Value);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenOnlyNameIsUpdated()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = $"Renamed Room {Guid.NewGuid():N}"
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
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

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenRoomTypeIsChanged()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var newRoomType = await CreateRoomTypeHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        room.RoomTypeId.Should().NotBe(newRoomType.Id);

        var dto = new UpdateRoomDto
        {
            RoomTypeId = newRoomType.Id
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
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

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = "Updated Room"
        };

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

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenRoomTypeIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            RoomTypeId = "invalid-uuid"
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
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
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Code = "   "
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room code cannot be empty.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = "   "
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room name cannot be empty.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenLocationIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Location = "   "
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room location cannot be empty.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenCodeExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Code = new string('a', 31)
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = new string('a', 121)
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = "Updated Room"
        };

        // Act
        using var response = await SendUpdateAsync(
            nonexistentId,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room not found.");
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenRoomTypeDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        var nonexistentRoomTypeId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            RoomTypeId = nonexistentRoomTypeId
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Room type not found.");
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenCodeAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var roomToUpdate = await CreateRoomHttp(tokens);
        var existingRoom = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Code = existingRoom.Code
        };

        // Act
        using var response = await SendUpdateAsync(
            roomToUpdate.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
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
        // Arrange
        var masterTokens = await LoginUserMaster();
        var room = await CreateRoomHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = $"Unauthorized Update {Guid.NewGuid():N}"
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomDto
        {
            Name = "Updated Room"
        };

        // Act
        using var response = await SendUpdateAsync(
            Guid.NewGuid().ToString(),
            dto,
            accessToken: null,
            inboxId: inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var dto = new UpdateRoomDto
        {
            Name = $"Updated Room {Guid.NewGuid():N}"
        };

        // Act
        using var response = await SendUpdateAsync(
            room.Id,
            dto,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var firstDto = new UpdateRoomDto
        {
            Name = "First Update"
        };

        var secondDto = new UpdateRoomDto
        {
            Name = "Second Update"
        };

        // Primeira atualização com sucesso.
        using (var firstResponse = await SendUpdateAsync(
                   room.Id,
                   firstDto,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda tentativa reutilizando o mesmo Inbox ID.
        using var response = await SendUpdateAsync(
            room.Id,
            secondDto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendUpdateAsync(
        string id,
        UpdateRoomDto dto,
        string? accessToken,
        string? inboxId)
    {
        return SendAsync(
            $"{UpdateUrl}/{Uri.EscapeDataString(id)}",
            HttpMethod.Put,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }
    
    protected async Task<HttpResponseMessage> SendAsync(
        string url,
        HttpMethod? method = null,
        object? body = null,
        string? accessToken = null,
        string? inboxId = null)
    {
        // Se o método não for informado, assume POST por retrocompatibilidade
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, url);

        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType());

        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (inboxId is not null)
            request.Headers.Add(InboxHeader, inboxId);

        return await Client.SendAsync(request);
    }
}