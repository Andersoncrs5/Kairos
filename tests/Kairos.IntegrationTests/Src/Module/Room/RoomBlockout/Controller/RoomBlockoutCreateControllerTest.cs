using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomBlockout.Controller;

[Collection("Integration Tests")]
public sealed class RoomBlockoutCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/room-blockouts";

    private readonly ITestOutputHelper _output;

    public RoomBlockoutCreateControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task Example()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        // Act
        var blockout = await CreateRoomBlockoutHttp(tokens);

        // Assert
        blockout.Should().NotBeNull();
        blockout.Id.Should().NotBeNullOrWhiteSpace();
        blockout.Status.Should().Be(RoomBlockoutStatus.Scheduled);
    }
    
    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsCreated_AndPersistsRoomBlockoutAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var dto = NewRoomBlockoutDto(room.Id);
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

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
        http.Data.RoomId.Should().Be(dto.RoomId);
        http.Data.Type.Should().Be(dto.Type);
        http.Data.Status.Should().Be(RoomBlockoutStatus.Scheduled);
        http.Data.Reason.Should().Be(dto.Reason);
        http.Data.Notes.Should().Be(dto.Notes);
        http.Data.StartsAt.Should().Be(dto.StartsAt);
        http.Data.EndsAt.Should().Be(dto.EndsAt);

        http.Data.CreatedBy.Should().NotBeNullOrWhiteSpace();
        http.Data.CreatedAt.Should().NotBe(default);

        http.Data.CancelledAt.Should().BeNull();
        http.Data.CancellationReason.Should().BeNull();
        http.Data.CancelledBy.Should().BeNull();

        // Verifica a persistência no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(blockout =>
                blockout.Id == http.Data.Id);

        persisted.Should().NotBeNull();

        persisted!.Id.Should().Be(http.Data.Id);
        persisted.RoomId.Should().Be(dto.RoomId);
        persisted.Type.Should().Be(dto.Type);
        persisted.Status.Should().Be(RoomBlockoutStatus.Scheduled);
        persisted.Reason.Should().Be(dto.Reason);
        persisted.Notes.Should().Be(dto.Notes);

        persisted.CreatedBy.Should().Be(http.Data.CreatedBy);
        persisted.CreatedAt.Should().NotBe(default);
        persisted.Version.Should().Be(1);

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
    public async Task Create_ReturnsBadRequest_WhenRoomIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewRoomBlockoutDto(
            roomId: "");

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
                "RoomId",
                StringComparison.OrdinalIgnoreCase));

        // A validação automática impede a execução da action.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenReasonIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var dto = NewRoomBlockoutDto(
            room.Id,
            reason: "");

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
                "Reason",
                StringComparison.OrdinalIgnoreCase));

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        // Nenhum bloqueio deve ter sido persistido.
        await AssertRoomBlockoutDoesNotExistAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenReasonExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var dto = NewRoomBlockoutDto(
            room.Id,
            reason: new string('a', 256));

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
                "Reason",
                StringComparison.OrdinalIgnoreCase));

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        await AssertRoomBlockoutDoesNotExistAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNotesExceedMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var dto = NewRoomBlockoutDto(
            room.Id,
            notes: new string('a', 1001));

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
                "Notes",
                StringComparison.OrdinalIgnoreCase));

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        await AssertRoomBlockoutDoesNotExistAsync(room.Id);
    }

    // ---------------------------------------------------------------
    // Validação do período
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenStartsAtIsAfterEndsAt()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var now = DateTimeOffset.UtcNow;

        var dto = NewRoomBlockoutDto(
            room.Id,
            startsAt: now.AddDays(10).AddHours(2),
            endsAt: now.AddDays(10));

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

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Room blockout start date must be earlier than end date.");

        await AssertRoomBlockoutDoesNotExistAsync(room.Id);
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var nonexistentRoomId = Guid.NewGuid().ToString();
        var dto = NewRoomBlockoutDto(nonexistentRoomId);

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

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Room doesn't exist.");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRoomIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var persistedRoom = await db.Set<RoomModel>()
                .SingleAsync(item => item.Id == room.Id);

            persistedRoom.IsActive = false;

            await db.SaveChangesAsync();
        }

        var dto = NewRoomBlockoutDto(room.Id);
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

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Room is not active.");

        await AssertRoomBlockoutDoesNotExistAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenBlockoutOverlaps()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var endsAt = startsAt.AddHours(2);

        // Insere um bloqueio diretamente no banco.
        using (var scope = CreateScope())
        {
            await CreateRoomBlockoutAsync(
                scope.ServiceProvider,
                roomId: room.Id,
                startsAt: startsAt,
                endsAt: endsAt);
        }

        var dto = NewRoomBlockoutDto(
            room.Id,
            startsAt: startsAt.AddMinutes(30),
            endsAt: endsAt.AddMinutes(30));

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

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Room blockout overlaps an existing blockout.");

        // Somente o bloqueio original deve continuar persistido.
        using var verificationScope = CreateScope();

        var count = await verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomBlockoutModel>()
            .AsNoTracking()
            .CountAsync(blockout => blockout.RoomId == room.Id);

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
        var room = await CreateRoomHttp(tokens);

        var dto = NewRoomBlockoutDto(room.Id);

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

        await AssertRoomBlockoutDoesNotExistAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var dto = NewRoomBlockoutDto(room.Id);
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

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(error =>
            error.Contains(
                "Inbox is not available for processing.",
                StringComparison.OrdinalIgnoreCase));

        await AssertRoomBlockoutDoesNotExistAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var room = await CreateRoomHttp(tokens);
        var inbox = await NewInboxAsync();

        var firstDto = NewRoomBlockoutDto(room.Id);
        var secondDto = NewRoomBlockoutDto(
            room.Id,
            startsAt: firstDto.EndsAt.AddDays(1));

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

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        using var scope = CreateScope();

        var count = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomBlockoutModel>()
            .AsNoTracking()
            .CountAsync(blockout => blockout.RoomId == room.Id);

        count.Should().Be(1);

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Autenticação
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var dto = NewRoomBlockoutDto(room.Id);
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertRoomBlockoutDoesNotExistAsync(room.Id);

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        CreateRoomBlockoutDto dto,
        string? inboxId,
        string? accessToken)
    {
        return SendAsync(
            CreateUrl,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private static CreateRoomBlockoutDto NewRoomBlockoutDto(
        string roomId,
        string? reason = null,
        string? notes = "Created by integration tests.",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        var start = startsAt ?? DateTimeOffset.UtcNow.AddDays(10);

        return new CreateRoomBlockoutDto
        {
            RoomId = roomId,
            Type = RoomBlockoutType.Maintenance,
            Reason = reason ?? $"Integration blockout {Guid.NewGuid():N}",
            Notes = notes,
            StartsAt = start,
            EndsAt = endsAt ?? start.AddHours(2)
        };
    }

    private async Task AssertRoomBlockoutDoesNotExistAsync(string roomId)
    {
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<RoomBlockoutModel>()
            .AsNoTracking()
            .AnyAsync(blockout => blockout.RoomId == roomId);

        exists.Should().BeFalse();
    }
}