using System.Net;
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
public sealed class RoomTypeCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/room-types";

    private readonly ITestOutputHelper _output;

    public RoomTypeCreateControllerTest(
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
    public async Task Example()
    {
        var tokens = await LoginUserMaster();

        var roomType = await CreateRoomTypeHttp(tokens);

        roomType.Name.Should().NotBeNullOrWhiteSpace();
    }
    
    [Fact]
    public async Task Create_ReturnsCreated_AndPersistsRoomTypeAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewRoomTypeDto();
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

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
        http.Data.Name.Should().Be(dto.Name);
        http.Data.Description.Should().Be(dto.Description);
        http.Data.IsActive.Should().BeTrue();

        // Verifica a persistência no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomTypeModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(roomType =>
                roomType.Name == dto.Name);

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(http.Data.Id);
        persisted.Name.Should().Be(dto.Name);
        persisted.Description.Should().Be(dto.Description);
        persisted.IsActive.Should().BeTrue();
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
    public async Task Create_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewRoomTypeDto(name: "");
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

        var http = await ReadAsync<Dictionary<string, string[]>>(
            response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().NotBeNull();

        http.Data!.Keys.Should().Contain(key =>
            key.EndsWith(
                "Name",
                StringComparison.OrdinalIgnoreCase));

        // A validação automática impede a execução da action.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        // A entidade não deve ter sido persistida.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(roomType => roomType.Name == dto.Name);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewRoomTypeDto(name: new string('a', 101));
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

        var http = await ReadAsync<Dictionary<string, string[]>>(
            response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().NotBeNull();

        http.Data!.Keys.Should().Contain(key =>
            key.EndsWith(
                "Name",
                StringComparison.OrdinalIgnoreCase));

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(roomType => roomType.Name == dto.Name);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDescriptionExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewRoomTypeDto(
            description: new string('a', 256));

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

        var http = await ReadAsync<Dictionary<string, string[]>>(
            response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().NotBeNull();

        http.Data!.Keys.Should().Contain(key =>
            key.EndsWith(
                "Description",
                StringComparison.OrdinalIgnoreCase));

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(roomType => roomType.Name == dto.Name);

        persisted.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConflict_WhenNameAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewRoomTypeDto();

        var firstInbox = await NewInboxAsync();

        // Primeiro cadastro.
        using (var firstResponse = await SendCreateAsync(
                   dto,
                   firstInbox.Id,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(
                HttpStatusCode.Created);

            var firstHttp = await ReadAsync<RoomTypeDto>(
                firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
            firstHttp.Data!.Name.Should().Be(dto.Name);
        }

        var secondInbox = await NewInboxAsync();

        // Segundo cadastro usando o mesmo nome.
        using var response = await SendCreateAsync(
            dto,
            secondInbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(error =>
            error.Contains(
                $"Room type with the name '{dto.Name}' already exists.",
                StringComparison.OrdinalIgnoreCase));

        // O registro original deve continuar persistido apenas uma vez.
        using var scope = CreateScope();

        var count = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomTypeModel>()
            .AsNoTracking()
            .CountAsync(roomType => roomType.Name == dto.Name);

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
        var dto = NewRoomTypeDto();

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
            .Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(roomType => roomType.Name == dto.Name);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewRoomTypeDto();
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

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(error =>
            error.Contains(
                "Inbox is not available for processing.",
                StringComparison.OrdinalIgnoreCase));

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomTypeModel>()
            .AsNoTracking()
            .AnyAsync(roomType => roomType.Name == dto.Name);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var inbox = await NewInboxAsync();

        var firstDto = NewRoomTypeDto();
        var secondDto = NewRoomTypeDto();

        // Primeiro cadastro.
        using (var firstResponse = await SendCreateAsync(
                   firstDto,
                   inbox.Id,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(
                HttpStatusCode.Created);
        }

        // Segundo cadastro utilizando o mesmo Inbox.
        using var response = await SendCreateAsync(
            secondDto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomTypeDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var first = await db.Set<RoomTypeModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(roomType =>
                roomType.Name == firstDto.Name);

        var second = await db.Set<RoomTypeModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(roomType =>
                roomType.Name == secondDto.Name);

        first.Should().NotBeNull();
        second.Should().BeNull();

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        CreateRoomTypeDto dto,
        string? inboxId,
        string? accessToken)
    {
        return SendAsync(
            CreateUrl,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private static CreateRoomTypeDto NewRoomTypeDto(
        string? name = null,
        string? description = null)
    {
        return new CreateRoomTypeDto
        {
            Name = name ?? $"Integration Room Type {Guid.NewGuid():N}",
            Description = description ?? "Created by integration tests."
        };
    }
}