using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Reservations.Reservation.Controller;

[Collection("Integration Tests")]
public sealed class ReservationCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/reservations";

    private readonly ITestOutputHelper _output;

    public ReservationCreateControllerTest(
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
    public async Task Create_ReturnsCreated_AndPersistsReservationAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var room = await CreateRoomHttp(tokens);

        var dto = NewReservationDto(
            room.Id,
            category.Id);

        await ConfigureRoomOperatingHoursForReservationHttp(
            tokens,
            room.Id,
            dto.StartsAt,
            dto.EndsAt);

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().NotBeNullOrWhiteSpace();
        http.Data.RoomId.Should().Be(dto.RoomId);
        http.Data.CategoryId.Should().Be(dto.CategoryId);
        http.Data.Title.Should().Be(dto.Title);
        http.Data.Description.Should().Be(dto.Description);
        http.Data.StartsAt.Should().Be(dto.StartsAt);
        http.Data.EndsAt.Should().Be(dto.EndsAt);
        http.Data.Status.Should().Be(ReservationStatusEnum.Confirmed);

        http.Data.UserId.Should().NotBeNullOrWhiteSpace();

        http.Data.CancelledAt.Should().BeNull();
        http.Data.CancelledBy.Should().BeNull();
        http.Data.CancellationReason.Should().BeNull();
        http.Data.CheckedInAt.Should().BeNull();
        http.Data.CheckedOutAt.Should().BeNull();

        // Verifica a persistência no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<ReservationModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(reservation =>
                reservation.Id == http.Data.Id);

        persisted.Should().NotBeNull();

        persisted!.Id.Should().Be(http.Data.Id);
        persisted.RoomId.Should().Be(dto.RoomId);
        persisted.CategoryId.Should().Be(dto.CategoryId);
        persisted.UserId.Should().Be(http.Data.UserId);
        persisted.Title.Should().Be(dto.Title);
        persisted.Description.Should().Be(dto.Description);
        persisted.StartsAt.Should().Be(dto.StartsAt);
        persisted.EndsAt.Should().Be(dto.EndsAt);
        persisted.Status.Should().Be(ReservationStatusEnum.Confirmed);
        persisted.IdempotencyKey.Should().Be(dto.IdempotencyKey);

        persisted.Version.Should().Be(1);
        persisted.CreatedAt.Should().NotBe(default);
        persisted.UpdatedAt.Should().BeNull();

        persisted.CancelledAt.Should().BeNull();
        persisted.CancelledBy.Should().BeNull();
        persisted.CancellationReason.Should().BeNull();
        persisted.CheckedInAt.Should().BeNull();
        persisted.CheckedOutAt.Should().BeNull();

        // Verifica o processamento do Inbox.
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRoomIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewReservationDto(
            roomId: "");

                string inboxId = Guid.NewGuid().ToString();


        // Act

    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCategoryIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewReservationDto(
            roomId: Guid.NewGuid().ToString(),
            categoryId: "");

                string inboxId = Guid.NewGuid().ToString();


        // Act
        await AssertModelValidationFailureAsync(
            dto,
            inboxId,
            tokens.Token,
            "CategoryId");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTitleIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewReservationDto(
            roomId: Guid.NewGuid().ToString(),
            title: "");

                string inboxId = Guid.NewGuid().ToString();


    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTitleExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewReservationDto(
            roomId: Guid.NewGuid().ToString(),
            title: new string('a', 151));

                string inboxId = Guid.NewGuid().ToString();


       
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenIdempotencyKeyExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewReservationDto(
            roomId: Guid.NewGuid().ToString(),
            idempotencyKey: new string('a', 101));

                string inboxId = Guid.NewGuid().ToString();


        // Act

    }

    // ---------------------------------------------------------------
    // Validação dos identificadores
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRoomIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewReservationDto(
            roomId: "invalid-room-id");

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Room ID must be a valid UUID.");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCategoryIdIsNotAValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewReservationDto(
            roomId: Guid.NewGuid().ToString(),
            categoryId: "invalid-category-id");

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Category ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Validação do período
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenStartsAtIsAfterEndsAt()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var room = await CreateRoomHttp(tokens);

        var now = DateTimeOffset.UtcNow;

        var dto = NewReservationDto(
            room.Id,
            category.Id,
            startsAt: now.AddDays(10).AddHours(2),
            endsAt: now.AddDays(10));

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Reservation start date must be earlier than end date.");

        await AssertReservationDoesNotExistAsync(room.Id);
    }

    // ---------------------------------------------------------------
    // Categoria
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var room = await CreateRoomHttp(tokens);

        var dto = NewReservationDto(
            room.Id,
            Guid.NewGuid().ToString());

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Category not found.");

        await AssertReservationDoesNotExistAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCategoryIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var room = await CreateRoomHttp(tokens);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var persistedCategory = await db.Set<CategoryModel>()
                .SingleAsync(item => item.Id == category.Id);

            persistedCategory.IsActive = false;

            await db.SaveChangesAsync();
        }

        var dto = NewReservationDto(
            room.Id,
            category.Id);

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Category is not active.");

        await AssertReservationDoesNotExistAsync(room.Id);
    }

    // ---------------------------------------------------------------
    // Sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);

        var nonexistentRoomId = Guid.NewGuid().ToString();

        var dto = NewReservationDto(
            nonexistentRoomId,
            category.Id);

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Room doesn't exist.");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRoomIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
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

        var dto = NewReservationDto(
            room.Id,
            category.Id);

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Room is not active.");

        await AssertReservationDoesNotExistAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRoomIsNotReservable()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var room = await CreateRoomHttp(tokens);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var persistedRoom = await db.Set<RoomModel>()
                .SingleAsync(item => item.Id == room.Id);

            persistedRoom.IsReservable = false;

            await db.SaveChangesAsync();
        }

        var dto = NewReservationDto(
            room.Id,
            category.Id);

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Room is not available for reservations.");

        await AssertReservationDoesNotExistAsync(room.Id);
    }

    // ---------------------------------------------------------------
    // Conflitos
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConflict_WhenReservationOverlaps()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        // CreateReservationHttp já configura os dias de funcionamento.
        var existingReservation = await CreateReservationHttp(tokens);

        var dto = NewReservationDto(
            existingReservation.RoomId,
            existingReservation.CategoryId,
            startsAt: existingReservation.StartsAt.AddMinutes(30),
            endsAt: existingReservation.EndsAt.AddMinutes(30));

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Room already has a reservation during this period.");

        // Somente a reserva original deve continuar persistida.
        await AssertReservationCountAsync(
            existingReservation.RoomId,
            expectedCount: 1);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenRoomBlockoutOverlaps()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
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

        var dto = NewReservationDto(
            room.Id,
            category.Id,
            startsAt: startsAt.AddMinutes(30),
            endsAt: endsAt.AddMinutes(30));

        await ConfigureRoomOperatingHoursForReservationHttp(
            tokens,
            room.Id,
            dto.StartsAt,
            dto.EndsAt);

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Room is blocked during this period.");

        await AssertReservationDoesNotExistAsync(room.Id);
    }

    // ---------------------------------------------------------------
    // Idempotência
    // ---------------------------------------------------------------

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var room = await CreateRoomHttp(tokens);

        var dto = NewReservationDto(
            room.Id,
            category.Id);

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

        await AssertReservationDoesNotExistAsync(room.Id);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var room = await CreateRoomHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        var firstDto = NewReservationDto(
            room.Id,
            category.Id);

        var secondDto = NewReservationDto(
            room.Id,
            category.Id,
            startsAt: firstDto.EndsAt.AddDays(1));

        await ConfigureRoomOperatingHoursForReservationHttp(
            tokens,
            room.Id,
            firstDto.StartsAt,
            firstDto.EndsAt);

        // Primeiro cadastro.
        using (var firstResponse = await SendCreateAsync(
                   firstDto,
                   inboxId,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(
                HttpStatusCode.Created);
        }

        // Segundo cadastro utilizando o mesmo Inbox.
        using var response = await SendCreateAsync(
            secondDto,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        await AssertReservationCountAsync(
            room.Id,
            expectedCount: 1);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Autenticação
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var room = await CreateRoomHttp(tokens);

        var dto = NewReservationDto(
            room.Id,
            category.Id);

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertReservationDoesNotExistAsync(room.Id);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        CreateReservationDto dto,
        string? inboxId,
        string? accessToken)
    {
        return SendAsync(
            CreateUrl,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private static CreateReservationDto NewReservationDto(
        string roomId,
        string? categoryId = null,
        string? title = null,
        string? description = "Created by integration tests.",
        string? idempotencyKey = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        var start = startsAt ?? DateTimeOffset.FromUnixTimeMilliseconds(
            DateTimeOffset.UtcNow.AddDays(10).ToUnixTimeMilliseconds());

        return new CreateReservationDto
        {
            RoomId = roomId,
            CategoryId = categoryId ?? Guid.NewGuid().ToString(),
            Title = title ?? $"Integration reservation {Guid.NewGuid():N}",
            Description = description,
            StartsAt = start,
            EndsAt = endsAt ?? start.AddHours(2),
            IdempotencyKey = idempotencyKey
        };
    }

    private async Task AssertModelValidationFailureAsync(
        CreateReservationDto dto,
        string inboxId,
        string accessToken,
        string propertyName)
    {
        using var response = await SendCreateAsync(
            dto,
            inboxId,
            accessToken);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(
            response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        await AssertInboxProcessedAsync(inboxId);
    }

    private async Task AssertReservationDoesNotExistAsync(string roomId)
    {
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .AnyAsync(reservation => reservation.RoomId == roomId);

        exists.Should().BeFalse();
    }

    private async Task AssertReservationCountAsync(
        string roomId,
        int expectedCount)
    {
        using var scope = CreateScope();

        var count = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .CountAsync(reservation => reservation.RoomId == roomId);

        count.Should().Be(expectedCount);
    }
}