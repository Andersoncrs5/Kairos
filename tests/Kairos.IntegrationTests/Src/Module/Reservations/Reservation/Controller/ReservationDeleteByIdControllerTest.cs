using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Reservations.Reservation.Controller;

[Collection("Integration Tests")]
public sealed class ReservationDeleteByIdControllerTest : BaseIntegrationTest
{
    private const string DeleteByIdUrl = "/api/v1/reservations";

    private readonly ITestOutputHelper _output;

    public ReservationDeleteByIdControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Exclusão
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteById_ReturnsTrue_AndRemovesReservationAndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteByIdAsync(
            reservation.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("Reservation deleted successfully");
        http.TraceId.Should().Be(inboxId);

        // Verifica que a reserva não está mais disponível
        // através da consulta normal do contexto.
        await AssertReservationDoesNotExistAsync(reservation.Id);

        // Verifica o processamento do Inbox.
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task DeleteById_ReturnsFalse_WhenReservationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteByIdAsync(
            nonexistentId,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
        http.Message.Should().Be("Reservation was not found");
        http.TraceId.Should().Be(inboxId);

        // Verifica o processamento do Inbox.
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task DeleteById_ReturnsBadRequest_WhenIdIsEmpty(
        string id)
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteByIdAsync(
            id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteById_ReturnsForbidden_WhenUserLacksDeletePermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteByIdAsync(
            reservation.Id,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A autorização impede a execução da action.
        await AssertInboxProcessedAsync(inboxId);
        await AssertReservationExistsAsync(reservation.Id);
    }

    // ---------------------------------------------------------------
    // Autenticação
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteById_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteByIdAsync(
            reservation.Id,
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertInboxProcessedAsync(inboxId);

        await AssertReservationExistsAsync(reservation.Id);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);

        // Act
        using var response = await SendDeleteByIdAsync(
            reservation.Id,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertReservationExistsAsync(reservation.Id);
    }

    [Fact]
    public async Task DeleteById_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteByIdAsync(
            reservation.Id,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertReservationExistsAsync(reservation.Id);
    }

    [Fact]
    public async Task DeleteById_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var reservation = await CreateReservationHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Primeira exclusão: deve ser processada com sucesso.
        using (var firstResponse = await SendDeleteByIdAsync(
                   reservation.Id,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().BeTrue();
        }

        // Segunda exclusão reutiliza o Inbox já processado.
        using var response = await SendDeleteByIdAsync(
            reservation.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);

        // A reserva já deve ter sido removida na primeira requisição.
        await AssertReservationDoesNotExistAsync(reservation.Id);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendDeleteByIdAsync(
        string id,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{DeleteByIdUrl}/{Uri.EscapeDataString(id)}");

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

    private async Task AssertReservationExistsAsync(string reservationId)
    {
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .AnyAsync(reservation => reservation.Id == reservationId);

        exists.Should().BeTrue();
    }

    private async Task AssertReservationDoesNotExistAsync(
        string reservationId)
    {
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Set<ReservationModel>()
            .AsNoTracking()
            .AnyAsync(reservation => reservation.Id == reservationId);

        exists.Should().BeFalse();
    }
}