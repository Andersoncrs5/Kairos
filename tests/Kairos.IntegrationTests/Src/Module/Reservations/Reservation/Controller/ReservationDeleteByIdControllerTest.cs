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
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteByIdAsync(
            reservation.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("Reservation deleted successfully");
        http.TraceId.Should().Be(inbox.Id);

        // Verifica que a reserva não está mais disponível
        // através da consulta normal do contexto.
        await AssertReservationDoesNotExistAsync(reservation.Id);

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task DeleteById_ReturnsFalse_WhenReservationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteByIdAsync(
            nonexistentId,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
        http.Message.Should().Be("Reservation was not found");
        http.TraceId.Should().Be(inbox.Id);

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
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
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteByIdAsync(
            id,
            tokens.Token,
            inbox.Id);

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
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteByIdAsync(
            reservation.Id,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A autorização impede a execução da action.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Pending);
        updatedInbox.ProcessedAt.Should().BeNull();

        // A reserva original deve continuar disponível.
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
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteByIdAsync(
            reservation.Id,
            accessToken: null,
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Pending);
        updatedInbox.ProcessedAt.Should().BeNull();

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
        var inbox = await NewInboxAsync();

        // Primeira exclusão: deve ser processada com sucesso.
        using (var firstResponse = await SendDeleteByIdAsync(
                   reservation.Id,
                   tokens.Token,
                   inbox.Id))
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
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();

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