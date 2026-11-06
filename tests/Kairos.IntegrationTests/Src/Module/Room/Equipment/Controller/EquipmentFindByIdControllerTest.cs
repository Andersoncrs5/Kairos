using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.Equipment.Controller;

[Collection("Integration Tests")]
public sealed class EquipmentFindByIdControllerTest : BaseIntegrationTest
{
    private const string FindUrl = "/api/v1/equipments";

    private readonly ITestOutputHelper _output;

    public EquipmentFindByIdControllerTest(
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
    public async Task FindById_ReturnsOk_WhenEquipmentExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            equipment.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<EquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Equipment found");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(equipment.Id);
        http.Data.Code.Should().Be(equipment.Code);
        http.Data.Name.Should().Be(equipment.Name);
        http.Data.Description.Should().Be(equipment.Description);
        http.Data.IsActive.Should().Be(equipment.IsActive);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            "invalid-uuid",
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<EquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            nonexistentId,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<EquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            equipment.Id,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FindById_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            Guid.NewGuid().ToString(),
            accessToken: null,
            inboxId: inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        // Act
        using var response = await SendFindByIdAsync(
            equipment.Id,
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
    public async Task FindById_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Primeira consulta: deve ser processada com sucesso.
        using (var firstResponse = await SendFindByIdAsync(
                   equipment.Id,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda consulta reutiliza o mesmo Inbox.
        using var response = await SendFindByIdAsync(
            equipment.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<EquipmentDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Helper para criação de Equipment via HTTP
    // ---------------------------------------------------------------


    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendFindByIdAsync(
        string id,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FindUrl}/{Uri.EscapeDataString(id)}");

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