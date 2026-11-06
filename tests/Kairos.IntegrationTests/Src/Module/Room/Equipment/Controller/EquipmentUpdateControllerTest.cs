using System.Net;
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Response;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.Equipment.Controller;

[Collection("Integration Tests")]
public sealed class EquipmentUpdateControllerTest : BaseIntegrationTest
{
    private const string UpdateUrl = "/api/v1/equipments";

    private readonly ITestOutputHelper _output;

    public EquipmentUpdateControllerTest(
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
    public async Task Update_ReturnsOk_AndPersistsEquipmentAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateEquipmentDto
        {
            Code = $"u{Guid.NewGuid():N}"[..Math.Min(50, $"u{Guid.NewGuid():N}".Length)],
            Name = $"Updated Equipment {Guid.NewGuid():N}",
            Description = "Updated by integration test.",
            IsActive = false
        };

        // Act
        using var response = await SendUpdateAsync(
            equipment.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<EquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Equipment updated successfully");
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(equipment.Id);
        http.Data.Code.Should().Be(dto.Code);
        http.Data.Name.Should().Be(dto.Name);
        http.Data.Description.Should().Be(dto.Description);
        http.Data.IsActive.Should().Be(dto.IsActive!.Value);

        // Verifica a persistência da atualização no banco.
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persisted = await db.Set<EquipmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == equipment.Id);

        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(dto.Code);
        persisted.Name.Should().Be(dto.Name);
        persisted.Description.Should().Be(dto.Description);
        persisted.IsActive.Should().Be(dto.IsActive!.Value);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenOnlyNameIsUpdated()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateEquipmentDto
        {
            Name = $"Renamed Equipment {Guid.NewGuid():N}"
        };

        // Act
        using var response = await SendUpdateAsync(
            equipment.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<EquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(equipment.Id);
        http.Data.Name.Should().Be(dto.Name);

        // Campos omitidos devem continuar com os valores anteriores.
        http.Data.Code.Should().Be(equipment.Code);
        http.Data.Description.Should().Be(equipment.Description);
        http.Data.IsActive.Should().Be(equipment.IsActive);

        await AssertInboxProcessedAsync(inboxId);
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

        var dto = new UpdateEquipmentDto
        {
            Name = "Updated Equipment"
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

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateEquipmentDto
        {
            Name = "Updated Equipment"
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

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Equipment doesn't exist.");
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenCodeAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipmentToUpdate = await CreateEquipmentHttp(tokens);
        var existingEquipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateEquipmentDto
        {
            Code = existingEquipment.Code
        };

        // Act
        using var response = await SendUpdateAsync(
            equipmentToUpdate.Id,
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            $"Equipment with the code '{existingEquipment.Code}' already exists.");
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksUpdatePermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateEquipmentDto
        {
            Name = $"Unauthorized Update {Guid.NewGuid():N}"
        };

        // Act
        using var response = await SendUpdateAsync(
            equipment.Id,
            dto,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        string inboxId = Guid.NewGuid().ToString();

        var dto = new UpdateEquipmentDto
        {
            Name = "Updated Equipment"
        };

        // Act
        using var response = await SendUpdateAsync(
            Guid.NewGuid().ToString(),
            dto,
            accessToken: null,
            inboxId: inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        var dto = new UpdateEquipmentDto
        {
            Name = $"Updated Equipment {Guid.NewGuid():N}"
        };

        // Act
        using var response = await SendUpdateAsync(
            equipment.Id,
            dto,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var firstDto = new UpdateEquipmentDto
        {
            Name = "First Update"
        };

        var secondDto = new UpdateEquipmentDto
        {
            Name = "Second Update"
        };

        // Primeira atualização com sucesso.
        using (var firstResponse = await SendUpdateAsync(
                   equipment.Id,
                   firstDto,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda tentativa reutilizando o mesmo Inbox ID.
        using var response = await SendUpdateAsync(
            equipment.Id,
            secondDto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
    }


    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendUpdateAsync(
        string id,
        UpdateEquipmentDto dto,
        string? accessToken,
        string? inboxId)
    {
        return SendAsyncWithMethod(
            $"{UpdateUrl}/{Uri.EscapeDataString(id)}",
            HttpMethod.Put,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }
}