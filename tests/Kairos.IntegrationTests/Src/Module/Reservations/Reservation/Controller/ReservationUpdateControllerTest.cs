using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
public sealed class ReservationUpdateControllerTest : BaseIntegrationTest
{
    private const string UpdateUrl = "/api/v1/reservations";

    private readonly ITestOutputHelper _output;

    public ReservationUpdateControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Atualização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsOk_AndPersistsReservationAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            Title = "Updated Integration Reservation",
            Description = "Updated by integration tests."
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Message.Should().Be("Reservation updated successfully");
        http.TraceId.Should().Be(inboxId);

        http.Data!.Id.Should().Be(reservation.Id);
        http.Data.RoomId.Should().Be(reservation.RoomId);
        http.Data.CategoryId.Should().Be(reservation.CategoryId);
        http.Data.UserId.Should().Be(reservation.UserId);
        http.Data.Title.Should().Be(dto.Title);
        http.Data.Description.Should().Be(dto.Description);
        http.Data.StartsAt.Should().Be(reservation.StartsAt);
        http.Data.EndsAt.Should().Be(reservation.EndsAt);
        http.Data.Status.Should().Be(reservation.Status);

        // Verifica a persistência no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<ReservationModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reservation.Id);

        persisted.Should().NotBeNull();

        persisted!.Id.Should().Be(reservation.Id);
        persisted.RoomId.Should().Be(reservation.RoomId);
        persisted.CategoryId.Should().Be(reservation.CategoryId);
        persisted.UserId.Should().Be(reservation.UserId);
        persisted.Title.Should().Be(dto.Title);
        persisted.Description.Should().Be(dto.Description);
        persisted.StartsAt.Should().Be(reservation.StartsAt);
        persisted.EndsAt.Should().Be(reservation.EndsAt);

        persisted.CreatedAt.Should().NotBe(default);
        persisted.UpdatedAt.Should().NotBeNull();

        // Verifica o processamento do Inbox.
        await AssertInboxProcessedAsync(inboxId);

    }

    [Fact]
    public async Task Update_ReturnsOk_WhenRoomCategoryAndPeriodAreChanged()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var reservation = await CreateReservationHttp(tokens);
        var newRoom = await CreateRoomHttp(tokens);
        var newCategory = await CreateCategoryHttp(tokens);

        var newStartsAt = reservation.StartsAt.AddDays(2);
        var newEndsAt = reservation.EndsAt.AddDays(2);

        var dto = new UpdateReservationDto
        {
            RoomId = newRoom.Id,
            CategoryId = newCategory.Id,
            StartsAt = newStartsAt,
            EndsAt = newEndsAt
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(reservation.Id);
        http.Data.RoomId.Should().Be(newRoom.Id);
        http.Data.CategoryId.Should().Be(newCategory.Id);
        http.Data.StartsAt.Should().Be(newStartsAt);
        http.Data.EndsAt.Should().Be(newEndsAt);

        // Verifica a persistência.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == reservation.Id);

        persisted.RoomId.Should().Be(newRoom.Id);
        persisted.CategoryId.Should().Be(newCategory.Id);
        persisted.StartsAt.Should().Be(newStartsAt);
        persisted.EndsAt.Should().Be(newEndsAt);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_KeepsExistingDescription_WhenDescriptionIsNull()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            Title = "Updated title",
            Description = null
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Data.Should().NotBeNull();

        http.Data!.Title.Should().Be(dto.Title);
        http.Data.Description.Should().Be(reservation.Description);
    }

    [Fact]
    public async Task Update_ClearsDescription_WhenDescriptionIsWhitespace()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            Description = "   "
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.Description.Should().BeNull();

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == reservation.Id);

        persisted.Description.Should().BeNull();
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

        var dto = new UpdateReservationDto
        {
            Title = "Updated title"
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

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Reservation ID must be a valid UUID.");
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateReservationDto
        {
            Title = "Updated title"
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

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain("Reservation not found.");
    }

    // ---------------------------------------------------------------
    // Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenTitleExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            Title = new string('a', 151)
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A validação automática impede a execução da action.
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenTitleIsWhitespace()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            Title = "   "
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Reservation title cannot be empty.");

        // A reserva permanece inalterada.
        await AssertReservationExistsAsync(reservation.Id);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenRoomIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            RoomId = ""
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain("Room ID is required.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenRoomIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            RoomId = "invalid-room-id"
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain(
            "Room ID must be a valid UUID.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenCategoryIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            CategoryId = " "
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain("Category ID is required.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenCategoryIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            CategoryId = "invalid-category-id"
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain(
            "Category ID must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Validação do período
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenStartsAtIsDefault()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            StartsAt = default(DateTimeOffset)
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain(
            "Reservation dates cannot be default values.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenEndsAtIsDefault()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            EndsAt = default(DateTimeOffset)
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain(
            "Reservation dates cannot be default values.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenStartsAtIsAfterEndsAt()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            StartsAt = reservation.EndsAt.AddHours(1)
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain(
            "Reservation start date must be earlier than end date.");
    }

    // ---------------------------------------------------------------
    // Validação da sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            RoomId = Guid.NewGuid().ToString()
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain("Room doesn't exist.");

        await AssertReservationRoomAsync(
            reservation.Id,
            reservation.RoomId);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNewRoomIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var newRoom = await CreateRoomHttp(tokens);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var persistedRoom = await db.Set<RoomModel>()
                .SingleAsync(item => item.Id == newRoom.Id);

            persistedRoom.IsActive = false;

            await db.SaveChangesAsync();
        }

        var dto = new UpdateReservationDto
        {
            RoomId = newRoom.Id
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain("Room is not active.");

        await AssertReservationRoomAsync(
            reservation.Id,
            reservation.RoomId);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNewRoomIsNotReservable()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var newRoom = await CreateRoomHttp(tokens);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var persistedRoom = await db.Set<RoomModel>()
                .SingleAsync(item => item.Id == newRoom.Id);

            persistedRoom.IsReservable = false;

            await db.SaveChangesAsync();
        }

        var dto = new UpdateReservationDto
        {
            RoomId = newRoom.Id
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain(
            "Room is not available for reservations.");

        await AssertReservationRoomAsync(
            reservation.Id,
            reservation.RoomId);
    }

    // ---------------------------------------------------------------
    // Validação da categoria
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            CategoryId = Guid.NewGuid().ToString()
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain("Category not found.");

        await AssertReservationCategoryAsync(
            reservation.Id,
            reservation.CategoryId);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNewCategoryIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var newCategory = await CreateCategoryHttp(tokens);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var persistedCategory = await db.Set<CategoryModel>()
                .SingleAsync(item => item.Id == newCategory.Id);

            persistedCategory.IsActive = false;

            await db.SaveChangesAsync();
        }

        var dto = new UpdateReservationDto
        {
            CategoryId = newCategory.Id
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain("Category is not active.");

        await AssertReservationCategoryAsync(
            reservation.Id,
            reservation.CategoryId);
    }

    // ---------------------------------------------------------------
    // Conflitos
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsConflict_WhenReservationOverlapsAnotherReservation()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var reservation = await CreateReservationHttp(tokens);
        var otherReservation = await CreateReservationHttp(tokens);

        // A reserva será movida para a sala da segunda reserva
        // e utilizará exatamente o mesmo período.
        var dto = new UpdateReservationDto
        {
            RoomId = otherReservation.RoomId,
            StartsAt = otherReservation.StartsAt,
            EndsAt = otherReservation.EndsAt
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Room already has a reservation during this period.");

        // A reserva original não deve ser modificada.
        await AssertReservationRoomAsync(
            reservation.Id,
            reservation.RoomId);

        await AssertReservationCountAsync(
            reservation.RoomId,
            expectedCount: 1);

        await AssertReservationCountAsync(
            otherReservation.RoomId,
            expectedCount: 1);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenRoomBlockoutOverlaps()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var blockoutStartsAt = reservation.StartsAt.AddDays(2);
        var blockoutEndsAt = blockoutStartsAt.AddHours(2);

        // Insere um bloqueio diretamente no banco.
        using (var scope = CreateScope())
        {
            await CreateRoomBlockoutAsync(
                scope.ServiceProvider,
                roomId: reservation.RoomId,
                startsAt: blockoutStartsAt,
                endsAt: blockoutEndsAt);
        }

        var dto = new UpdateReservationDto
        {
            StartsAt = blockoutStartsAt,
            EndsAt = blockoutEndsAt
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<ReservationDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(
            "Room is blocked during this period.");

        // O período original deve continuar persistido.
        await AssertReservationPeriodAsync(
            reservation.Id,
            reservation.StartsAt,
            reservation.EndsAt);
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksUpdatePermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateReservationDto
        {
            Title = "Unauthorized update"
        };

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertInboxProcessedAsync(inboxId);

        await AssertReservationTitleAsync(
            reservation.Id,
            reservation.Title);
    }

    // ---------------------------------------------------------------
    // Autenticação
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateReservationDto
        {
            Title = "Unauthorized update"
        };

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertInboxProcessedAsync(inboxId);
        await AssertReservationTitleAsync(
            reservation.Id,
            reservation.Title);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            Title = "Updated title"
        };

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertReservationTitleAsync(
            reservation.Id,
            reservation.Title);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new UpdateReservationDto
        {
            Title = "Updated title"
        };

        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendUpdateAsync(
            reservation.Id,
            dto,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertReservationTitleAsync(
            reservation.Id,
            reservation.Title);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var firstDto = new UpdateReservationDto
        {
            Title = "First update"
        };

        var secondDto = new UpdateReservationDto
        {
            Title = "Second update"
        };

        // Primeira atualização.
        using (var firstResponse = await SendUpdateAsync(
                   reservation.Id,
                   firstDto,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<ReservationDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
            firstHttp.Data!.Title.Should().Be(firstDto.Title);
        }

        // Segunda atualização reutiliza o Inbox processado.
        using var response = await SendUpdateAsync(
            reservation.Id,
            secondDto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);

        // A segunda atualização não deve ter sido executada.
        await AssertReservationTitleAsync(
            reservation.Id,
            firstDto.Title);
    }

    // ---------------------------------------------------------------
    // Helpers HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendUpdateAsync(
        string id,
        UpdateReservationDto dto,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{UpdateUrl}/{Uri.EscapeDataString(id)}")
        {
            Content = JsonContent.Create(dto)
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);
        }

        if (inboxId is not null)
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }

    // ---------------------------------------------------------------
    // Helpers de persistência
    // ---------------------------------------------------------------

    private async Task AssertReservationExistsAsync(string reservationId)
    {
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == reservationId);

        exists.Should().BeTrue();
    }

    private async Task AssertReservationRoomAsync(
        string reservationId,
        string expectedRoomId)
    {
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == reservationId);

        persisted.RoomId.Should().Be(expectedRoomId);
    }

    private async Task AssertReservationCategoryAsync(
        string reservationId,
        string expectedCategoryId)
    {
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == reservationId);

        persisted.CategoryId.Should().Be(expectedCategoryId);
    }

    private async Task AssertReservationPeriodAsync(
        string reservationId,
        DateTimeOffset expectedStartsAt,
        DateTimeOffset expectedEndsAt)
    {
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == reservationId);

        persisted.StartsAt.Should().Be(expectedStartsAt);
        persisted.EndsAt.Should().Be(expectedEndsAt);
    }

    private async Task AssertReservationTitleAsync(
        string reservationId,
        string expectedTitle)
    {
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == reservationId);

        persisted.Title.Should().Be(expectedTitle);
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
            .CountAsync(item => item.RoomId == roomId);

        count.Should().Be(expectedCount);
    }
}