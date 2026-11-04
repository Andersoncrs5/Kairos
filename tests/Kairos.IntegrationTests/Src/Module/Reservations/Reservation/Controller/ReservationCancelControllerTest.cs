using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Reservations.Reservation.Controller;

[Collection("Integration Tests")]
public sealed class ReservationCancelControllerTest : BaseIntegrationTest
{
    private const string CancelUrl = "/api/v1/reservations";

    private readonly ITestOutputHelper _output;

    public ReservationCancelControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsOk_AndPersistsCancellationAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        const string reason = "  Cancelled by integration tests.  ";

        var dto = new CancelReservationDto
        {
            CancellationReason = reason
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCancelAsync(
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

        http.Message.Should().Be("Reservation cancelled successfully");
        http.TraceId.Should().Be(inboxId);

        http.Data!.Id.Should().Be(reservation.Id);
        http.Data.RoomId.Should().Be(reservation.RoomId);
        http.Data.CategoryId.Should().Be(reservation.CategoryId);
        http.Data.UserId.Should().Be(reservation.UserId);

        http.Data.Status.Should().Be(ReservationStatusEnum.Cancelled);
        http.Data.CancelledAt.Should().NotBeNull();
        http.Data.CancelledBy.Should().Be(reservation.UserId);
        http.Data.CancellationReason.Should().Be(
            "Cancelled by integration tests.");

        // Verifica a persistência.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<ReservationModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reservation.Id);

        persisted.Should().NotBeNull();

        persisted!.Status.Should().Be(ReservationStatusEnum.Cancelled);
        persisted.CancelledAt.Should().NotBeNull();
        persisted.CancelledBy.Should().Be(reservation.UserId);
        persisted.CancellationReason.Should().Be(
            "Cancelled by integration tests.");

        persisted.CreatedAt.Should().NotBe(default);
        persisted.UpdatedAt.Should().NotBeNull();

        // Verifica o processamento do Inbox.
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Cancel_ReturnsOk_WhenCancellationReasonIsNull()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new CancelReservationDto
        {
            CancellationReason = null
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCancelAsync(
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

        http.Data!.Status.Should().Be(ReservationStatusEnum.Cancelled);
        http.Data.CancellationReason.Should().BeNull();
        http.Data.CancelledAt.Should().NotBeNull();
        http.Data.CancelledBy.Should().Be(reservation.UserId);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Cancel_ReturnsOk_WhenCancellationReasonIsWhitespace()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new CancelReservationDto
        {
            CancellationReason = "   "
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCancelAsync(
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

        http.Data!.CancellationReason.Should().BeNull();
        http.Data.Status.Should().Be(ReservationStatusEnum.Cancelled);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsBadRequest_WhenReservationIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new CancelReservationDto
        {
            CancellationReason = "Invalid reservation ID test."
        };

        // Act
        using var response = await SendCancelAsync(
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
    public async Task Cancel_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new CancelReservationDto
        {
            CancellationReason = "Reservation does not exist."
        };

        // Act
        using var response = await SendCancelAsync(
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
    public async Task Cancel_ReturnsBadRequest_WhenCancellationReasonExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new CancelReservationDto
        {
            CancellationReason = new string('a', 256)
        };

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCancelAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertReservationStatusAsync(
            reservation.Id,
            ReservationStatusEnum.Confirmed);
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsConflict_WhenReservationIsAlreadyCancelled()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var firstCancellation = await CancelReservationHttp(
            tokens,
            reservation.Id,
            "First cancellation.");

        firstCancellation.Status.Should().Be(
            ReservationStatusEnum.Cancelled);

        string inboxId = Guid.NewGuid().ToString();

        var dto = new CancelReservationDto
        {
            CancellationReason = "Second cancellation."
        };

        // Act
        using var response = await SendCancelAsync(
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
            "Reservation is already cancelled.");

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == reservation.Id);

        persisted.Status.Should().Be(ReservationStatusEnum.Cancelled);
        persisted.CancellationReason.Should().Be("First cancellation.");
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsForbidden_WhenUserLacksCancelPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new CancelReservationDto
        {
            CancellationReason = "Unauthorized cancellation."
        };

        // Act
        using var response = await SendCancelAsync(
            reservation.Id,
            dto,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AssertInboxProcessedAsync(inboxId);

        await AssertReservationStatusAsync(
            reservation.Id,
            ReservationStatusEnum.Confirmed);
    }

    // ---------------------------------------------------------------
    // Autenticação
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new CancelReservationDto
        {
            CancellationReason = "Unauthorized cancellation."
        };

        // Act
        using var response = await SendCancelAsync(
            reservation.Id,
            dto,
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertInboxProcessedAsync(inboxId);
        
        await AssertReservationStatusAsync(
            reservation.Id,
            ReservationStatusEnum.Confirmed);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        var dto = new CancelReservationDto
        {
            CancellationReason = "Missing Inbox header."
        };

        // Act
        using var response = await SendCancelAsync(
            reservation.Id,
            dto,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertReservationStatusAsync(
            reservation.Id,
            ReservationStatusEnum.Confirmed);
    }

    [Fact]
    public async Task Cancel_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new CancelReservationDto
        {
            CancellationReason = "First cancellation."
        };

        // Primeiro cancelamento.
        using (var firstResponse = await SendCancelAsync(
                   reservation.Id,
                   dto,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<ReservationDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
            firstHttp.Data!.Status.Should().Be(
                ReservationStatusEnum.Cancelled);
        }

        // Segunda requisição reutiliza o Inbox processado.
        using var response = await SendCancelAsync(
            reservation.Id,
            new CancelReservationDto
            {
                CancellationReason = "Second cancellation."
            },
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == reservation.Id);

        persisted.Status.Should().Be(ReservationStatusEnum.Cancelled);
        persisted.CancellationReason.Should().Be("First cancellation.");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCancelAsync(
        string id,
        CancelReservationDto dto,
        string? accessToken,
        string? inboxId)
    {
        return SendAsync(
            $"{CancelUrl}/{Uri.EscapeDataString(id)}/cancel",
            HttpMethod.Post,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private async Task AssertReservationStatusAsync(
        string reservationId,
        ReservationStatusEnum expectedStatus)
    {
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == reservationId);

        persisted.Status.Should().Be(expectedStatus);
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