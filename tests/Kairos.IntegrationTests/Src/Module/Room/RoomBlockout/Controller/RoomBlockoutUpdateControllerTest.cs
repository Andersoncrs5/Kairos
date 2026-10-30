
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
public sealed class RoomBlockoutUpdateControllerTest : BaseIntegrationTest
{
    private const string UpdateUrl = "/api/v1/room-blockouts";
    private const string InboxHeader = "X-Inbox-Id";

    private readonly ITestOutputHelper _output;

    public RoomBlockoutUpdateControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsOk_WhenDataIsValid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var newReason = $"Updated reason {Guid.NewGuid():N}";
        const string newNotes = "Updated by integration tests.";

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = newReason,
            Notes = newNotes
        };

        // Act
        using var response = await SendUpdateAsync(
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
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.TraceId.Should().Be(inbox.Id);
        http.Message.Should().Be("Room blockout updated successfully");
        http.Data.Should().NotBeNull();

        var data = http.Data!;

        data.Id.Should().Be(blockout.Id);
        data.RoomId.Should().Be(blockout.RoomId);
        data.Type.Should().Be(blockout.Type);
        data.Status.Should().Be(blockout.Status);
        data.Reason.Should().Be(newReason);
        data.Notes.Should().Be(newNotes);

        // Campos omitidos devem preservar os valores anteriores.
        data.StartsAt.Should().Be(blockout.StartsAt);
        data.EndsAt.Should().Be(blockout.EndsAt);
        data.CreatedBy.Should().Be(blockout.CreatedBy);

        // Verifica a persistência no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persisted = await db.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == blockout.Id);

        persisted.Should().NotBeNull();
        persisted!.Reason.Should().Be(newReason);
        persisted.Notes.Should().Be(newNotes);
        persisted.RoomId.Should().Be(blockout.RoomId);
        persisted.Type.Should().Be(blockout.Type);
        persisted.StartsAt.Should().Be(blockout.StartsAt);
        persisted.EndsAt.Should().Be(blockout.EndsAt);

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task Update_PreservesExistingValues_WhenPropertiesAreOmitted()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var newReason = $"Partial update {Guid.NewGuid():N}";

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = newReason
        };

        // Act
        using var response = await SendUpdateAsync(
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
        http.Data.Should().NotBeNull();

        var data = http.Data!;

        data.Reason.Should().Be(newReason);

        // Notes não enviado: deve permanecer igual.
        data.Notes.Should().Be(blockout.Notes);

        // Demais propriedades também devem permanecer iguais.
        data.RoomId.Should().Be(blockout.RoomId);
        data.Type.Should().Be(blockout.Type);
        data.StartsAt.Should().Be(blockout.StartsAt);
        data.EndsAt.Should().Be(blockout.EndsAt);
    }

    // ---------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = "Updated reason"
        };

        // Act
        using var response = await SendUpdateAsync(
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
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenRoomIdIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomBlockoutDto
        {
            RoomId = "invalid-room-id"
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
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
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenRoomIdIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomBlockoutDto
        {
            RoomId = " "
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
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
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenReasonIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = " "
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
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
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenTypeIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomBlockoutDto
        {
            Type = (RoomBlockoutType)int.MaxValue
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenStartDateIsAfterEndDate()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var start = DateTimeOffset.FromUnixTimeMilliseconds(
            DateTimeOffset.UtcNow.AddDays(15).ToUnixTimeMilliseconds());

        var end = start.AddHours(1);

        var dto = new UpdateRoomBlockoutDto
        {
            StartsAt = end,
            EndsAt = start
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
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
    }

    // ---------------------------------------------------------------
    // Not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenRoomBlockoutDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var nonexistentId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = "Updated reason"
        };

        // Act
        using var response = await SendUpdateAsync(
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
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTargetRoomDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var nonexistentRoomId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomBlockoutDto
        {
            RoomId = nonexistentRoomId
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
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
    }

    // ---------------------------------------------------------------
    // Business rules
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenTargetRoomIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inactiveRoom = await CreateRoomHttp(tokens);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var room = await db.Set<RoomModel>()
                .SingleAsync(x => x.Id == inactiveRoom.Id);

            room.IsActive = false;

            await db.SaveChangesAsync();
        }

        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomBlockoutDto
        {
            RoomId = inactiveRoom.Id
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
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
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenRoomBlockoutIsCancelled()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var persisted = await db.Set<RoomBlockoutModel>()
                .SingleAsync(x => x.Id == blockout.Id);

            persisted.Status = RoomBlockoutStatus.Cancelled;

            await db.SaveChangesAsync();
        }

        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = "Trying to update cancelled blockout"
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenPeriodOverlapsAnotherBlockout()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);

        // Cria outro bloqueio no mesmo ambiente, em período sobreposto.
        using (var scope = CreateScope())
        {
            await CreateRoomBlockoutAsync(
                scope.ServiceProvider,
                roomId: blockout.RoomId,
                createdBy: blockout.CreatedBy,
                startsAt: blockout.StartsAt.AddMinutes(30),
                endsAt: blockout.EndsAt.AddMinutes(30));
        }

        var inbox = await NewInboxAsync();

        // A atualização de Reason mantém o período original.
        // O período original agora conflita com o segundo bloqueio.
        var dto = new UpdateRoomBlockoutDto
        {
            Reason = "Updated with conflicting period"
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<RoomBlockoutDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = "Updated reason"
        };

        // Act
        using var response = await SendUpdateAsync(
            Guid.NewGuid().ToString(),
            dto,
            accessToken: null,
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksUpdatePermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();

        var inbox = await NewInboxAsync();

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = "Unauthorized update"
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
            dto,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = "Update without Inbox"
        };

        // Act
        using var response = await SendUpdateAsync(
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
    public async Task Update_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);

        var nonexistentInboxId = Guid.NewGuid().ToString();

        var dto = new UpdateRoomBlockoutDto
        {
            Reason = "Update with nonexistent Inbox"
        };

        // Act
        using var response = await SendUpdateAsync(
            blockout.Id,
            dto,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var blockout = await CreateRoomBlockoutHttp(tokens);
        var inbox = await NewInboxAsync();

        var firstDto = new UpdateRoomBlockoutDto
        {
            Reason = "First update"
        };

        // Primeira atualização.
        using (var firstResponse = await SendUpdateAsync(
                   blockout.Id,
                   firstDto,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<RoomBlockoutDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
        }

        // Segunda atualização usando o Inbox já processado.
        var secondDto = new UpdateRoomBlockoutDto
        {
            Reason = "Second update"
        };

        using var response = await SendUpdateAsync(
            blockout.Id,
            secondDto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
    }

    // ---------------------------------------------------------------
    // HTTP helper
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendUpdateAsync(
        string id,
        UpdateRoomBlockoutDto dto,
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
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }
}
