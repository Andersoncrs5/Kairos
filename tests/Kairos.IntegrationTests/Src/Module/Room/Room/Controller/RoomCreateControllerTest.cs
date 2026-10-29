using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.Room.Controller;

[Collection("Integration Tests")]
public sealed class RoomCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/rooms";

    private readonly ITestOutputHelper _output;

    public RoomCreateControllerTest(KairosApiFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsCreated_AndPersistsRoomAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var dto = NewRoomDto(roomType.Id);
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<RoomDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
        http.Data.RoomTypeId.Should().Be(dto.RoomTypeId);
        http.Data.Code.Should().Be(dto.Code);
        http.Data.Name.Should().Be(dto.Name);
        http.Data.Description.Should().Be(dto.Description);
        http.Data.Location.Should().Be(dto.Location);
        http.Data.Floor.Should().Be(dto.Floor);
        http.Data.Capacity.Should().Be(dto.Capacity);
        http.Data.AreaSquareMeters.Should().Be(dto.AreaSquareMeters);
        http.Data.IsAccessible.Should().Be(dto.IsAccessible);
        http.Data.IsReservable.Should().Be(dto.IsReservable);
        http.Data.IsActive.Should().BeTrue();

        // Verifica a persistência no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(room => room.Code == dto.Code);

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(http.Data.Id);
        persisted.RoomTypeId.Should().Be(dto.RoomTypeId);
        persisted.Code.Should().Be(dto.Code);
        persisted.Name.Should().Be(dto.Name);
        persisted.Description.Should().Be(dto.Description);
        persisted.Location.Should().Be(dto.Location);
        persisted.Floor.Should().Be(dto.Floor);
        persisted.Capacity.Should().Be(dto.Capacity);
        persisted.AreaSquareMeters.Should().Be(dto.AreaSquareMeters);
        persisted.IsAccessible.Should().Be(dto.IsAccessible);
        persisted.IsReservable.Should().Be(dto.IsReservable);
        persisted.IsActive.Should().BeTrue();
        persisted.Version.Should().Be(1);
        persisted.CreatedAt.Should().NotBe(default);

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCodeIsEmpty()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            code: "");

        await AssertInvalidDtoAsync(dto, "Code", tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCodeExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            code: new string('A', 31));

        await AssertInvalidDtoAsync(dto, "Code", tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameIsEmpty()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            name: "");

        await AssertInvalidDtoAsync(dto, "Name", tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            name: new string('a', 121));

        await AssertInvalidDtoAsync(dto, "Name", tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDescriptionExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            description: new string('a', 501));

        await AssertInvalidDtoAsync(dto, "Description", tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenLocationIsEmpty()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            location: "");

        await AssertInvalidDtoAsync(dto, "Location", tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenLocationExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            location: new string('a', 151));

        await AssertInvalidDtoAsync(dto, "Location", tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenFloorExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            floor: new string('a', 31));

        await AssertInvalidDtoAsync(dto, "Floor", tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCapacityIsZero()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            capacity: 0);

        await AssertInvalidDtoAsync(dto, "Capacity", tokens.Token);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenAreaIsZero()
    {
        var tokens = await LoginUserMaster();
        var dto = NewRoomDto(
            Guid.NewGuid().ToString(),
            areaSquareMeters: 0m);

        await AssertInvalidDtoAsync(
            dto,
            "AreaSquareMeters",
            tokens.Token);
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRoomTypeIdIsNotUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewRoomDto(
            roomTypeId: "invalid-uuid");

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room type ID must be a valid UUID.");

        // O use case foi executado, pois o UUID inválido não é
        // uma falha de validação automática do DTO.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(room => room.Code == dto.Code);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsNotFound_WhenRoomTypeDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewRoomDto(
            roomTypeId: Guid.NewGuid().ToString());

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Room type doesn't exist.");

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(room => room.Code == dto.Code);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenCodeAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        RoomModel existingRoom;

        using (var scope = CreateScope())
        {
            existingRoom = await CreateRoomAsync(
                scope.ServiceProvider);
        }

        var dto = NewRoomDto(
            roomTypeId: existingRoom.RoomTypeId,
            code: existingRoom.Code);

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            $"Room with the code '{dto.Code}' already exists.");

        // A sala original deve continuar sendo a única com esse código.
        using var verificationScope = CreateScope();

        var count = await verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .CountAsync(room => room.Code == dto.Code);

        count.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewRoomDto(
            roomTypeId: Guid.NewGuid().ToString());

        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(error =>
            error.Contains(
                InboxHeader,
                StringComparison.OrdinalIgnoreCase));

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(room => room.Code == dto.Code);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var dto = NewRoomDto(roomType.Id);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
            dto,
            nonexistentInboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(room => room.Code == dto.Code);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var roomType = await CreateRoomTypeHttp(tokens);
        var inbox = await NewInboxAsync();

        var firstDto = NewRoomDto(roomType.Id);
        var secondDto = NewRoomDto(roomType.Id);

        // Primeira criação.
        using (var firstResponse = await SendCreateAsync(
                   firstDto,
                   inbox.Id,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(
                HttpStatusCode.Created);
        }

        // Segunda criação usando o mesmo Inbox.
        using var response = await SendCreateAsync(
            secondDto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var firstRoom = await db.Set<RoomModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(room =>
                room.Code == firstDto.Code);

        var secondRoom = await db.Set<RoomModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(room =>
                room.Code == secondDto.Code);

        firstRoom.Should().NotBeNull();
        secondRoom.Should().BeNull();

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        CreateRoomDto dto,
        string? inboxId,
        string? accessToken)
    {
        return SendAsync(
            CreateUrl,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private async Task AssertInvalidDtoAsync(
        CreateRoomDto dto,
        string expectedField,
        string accessToken)
    {
        var inbox = await NewInboxAsync();

        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            accessToken);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(
            response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().NotBeNull();

        http.Data!.Keys.Should().Contain(key =>
            key.EndsWith(
                expectedField,
                StringComparison.OrdinalIgnoreCase));

        // A validação automática impede a execução da action.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        // O use case não deve ter persistido a sala.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(room => room.Code == dto.Code);

        persisted.Should().BeFalse();
    }

    private static CreateRoomDto NewRoomDto(
        string roomTypeId,
        string? code = null,
        string? name = null,
        string? description = null,
        string? location = null,
        string? floor = null,
        int capacity = 10,
        decimal? areaSquareMeters = 20m)
    {
        return new CreateRoomDto
        {
            RoomTypeId = roomTypeId,
            Code = code ?? $"R{Guid.NewGuid():N}"[..30],
            Name = name ?? $"Integration Room {Guid.NewGuid():N}",
            Description = description ?? "Created by integration tests.",
            Location = location ?? "Integration Test Location",
            Floor = floor ?? "First floor",
            Capacity = capacity,
            AreaSquareMeters = areaSquareMeters,
            IsAccessible = false,
            IsReservable = true
        };
    }
}