using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.RoomBlockout.Controller;

[Collection("Integration Tests")]
public sealed class RoomBlockoutCancelControllerTest : BaseIntegrationTest
{
    private const string CancelUrl = "/api/v1/room-blockouts";
    private const string InboxHeader = "X-Inbox-Id";

    private readonly ITestOutputHelper _output;

    public RoomBlockoutCancelControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Cancel
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsOk_WhenRoomBlockoutExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        const string cancellationReason =
            "Maintenance was no longer necessary.";

        var dto = new CancelRoomBlockoutDto
        {
            CancellationReason = cancellationReason
        };

        // Act
        using var response = await SendCancelAsync(
            blockout.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().Be(inbox.Id);
        http.Message.Should().Be("Room blockout cancelled successfully");
        http.Data.Should().NotBeNull();

        var data = http.Data!;

        data.Id.Should().Be(blockout.Id);
        data.RoomId.Should().Be(blockout.RoomId);
        data.Type.Should().Be(blockout.Type);
        data.Status.Should().Be(RoomBlockoutStatus.Cancelled);

        data.Reason.Should().Be(blockout.Reason);
        data.Notes.Should().Be(blockout.Notes);

        data.StartsAt.Should().Be(blockout.StartsAt);
        data.EndsAt.Should().Be(blockout.EndsAt);

        data.CancellationReason.Should().Be(cancellationReason);
        data.CancelledAt.Should().NotBeNull();

        // O mesmo usuário criou e cancelou o bloqueio.
        data.CancelledBy.Should().Be(blockout.CreatedBy);

        data.CreatedBy.Should().Be(blockout.CreatedBy);

        // Verifica a persistência no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == blockout.Id);

        persisted.Should().NotBeNull();

        persisted!.Status.Should().Be(RoomBlockoutStatus.Cancelled);
        persisted.CancellationReason.Should().Be(cancellationReason);
        persisted.CancelledBy.Should().Be(blockout.CreatedBy);
        persisted.CancelledAt.Should().NotBeNull();

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsBadRequest_WhenIdIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var dto = NewCancelDto();

        // Act
        using var response = await SendCancelAsync(
            "invalid-id",
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.TraceId.Should().Be(inbox.Id);
        http.Message.Should().Be("Error cancelling room blockout");
    }

    [Fact]
    public async Task Cancel_ReturnsBadRequest_WhenCancellationReasonIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new CancelRoomBlockoutDto
        {
            CancellationReason = " "
        };

        // Act
        using var response = await SendCancelAsync(
            blockout.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsNotFound_WhenRoomBlockoutDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var nonexistentId = Guid.NewGuid().ToString();
        var dto = NewCancelDto();

        // Act
        using var response = await SendCancelAsync(
            nonexistentId,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.TraceId.Should().Be(inbox.Id);
        http.Message.Should().Be("Error cancelling room blockout");
    }

    // ---------------------------------------------------------------
    // Business rules
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsConflict_WhenRoomBlockoutIsAlreadyCancelled()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);

        // Primeiro cancelamento.
        var firstInbox = await NewInboxAsync();

        using (var firstResponse = await SendCancelAsync(
                   blockout.Id,
                   NewCancelDto("First cancellation"),
                   tokens.Token,
                   firstInbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<RoomBlockoutDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
            firstHttp.Data!.Status.Should().Be(RoomBlockoutStatus.Cancelled);
        }

        // Segundo cancelamento com um Inbox novo.
        // Dessa forma, o conflito vem da regra de negócio,
        // e não da reutilização do Inbox.
        var secondInbox = await NewInboxAsync();

        // Act
        using var response = await SendCancelAsync(
            blockout.Id,
            NewCancelDto("Second cancellation"),
            tokens.Token,
            secondInbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.TraceId.Should().Be(secondInbox.Id);

        // A primeira razão de cancelamento deve ser preservada.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == blockout.Id);

        persisted.Status.Should().Be(RoomBlockoutStatus.Cancelled);
        persisted.CancellationReason.Should().Be("First cancellation");
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var inbox = await NewInboxAsync();

        var dto = NewCancelDto();

        // Act
        using var response = await SendCancelAsync(
            Guid.NewGuid().ToString(),
            dto,
            accessToken: null,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsForbidden_WhenUserLacksCancelPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();

        var inbox = await NewInboxAsync();
        var dto = NewCancelDto();

        // Act
        using var response = await SendCancelAsync(
            blockout.Id,
            dto,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // O bloqueio deve continuar agendado.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == blockout.Id);

        persisted.Status.Should().Be(RoomBlockoutStatus.Scheduled);
        persisted.CancelledAt.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);

        var dto = NewCancelDto();

        // Act
        using var response = await SendCancelAsync(
            blockout.Id,
            dto,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);

        var nonexistentInboxId = Guid.NewGuid().ToString();
        var dto = NewCancelDto();

        // Act
        using var response = await SendCancelAsync(
            blockout.Id,
            dto,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // O cancelamento não deve ser persistido.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == blockout.Id);

        persisted.Status.Should().Be(RoomBlockoutStatus.Scheduled);
        persisted.CancelledAt.Should().BeNull();
    }

    [Fact]
    public async Task Cancel_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        // Primeira execução.
        using (var firstResponse = await SendCancelAsync(
                   blockout.Id,
                   NewCancelDto("First cancellation"),
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<RoomBlockoutDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
        }

        // Segunda execução com o Inbox já processado.
        using var response = await SendCancelAsync(
            blockout.Id,
            NewCancelDto("Second cancellation"),
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();

        // O primeiro cancelamento permanece preservado.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == blockout.Id);

        persisted.Status.Should().Be(RoomBlockoutStatus.Cancelled);
        persisted.CancellationReason.Should().Be("First cancellation");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static CancelRoomBlockoutDto NewCancelDto(
        string cancellationReason = "Cancelled by integration tests.")
    {
        return new CancelRoomBlockoutDto
        {
            CancellationReason = cancellationReason
        };
    }

    private async Task<HttpResponseMessage> SendCancelAsync(
        string id,
        CancelRoomBlockoutDto dto,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{CancelUrl}/{Uri.EscapeDataString(id)}/cancel")
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
