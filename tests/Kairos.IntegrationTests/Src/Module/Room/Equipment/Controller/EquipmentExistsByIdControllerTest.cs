using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.Equipment.Controller;

[Collection("Integration Tests")]
public sealed class EquipmentExistsByIdControllerTest : BaseIntegrationTest
{
    private const string ExistsUrl = "/api/v1/equipments/Exists/id";

    private readonly ITestOutputHelper _output;

    public EquipmentExistsByIdControllerTest(
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
    public async Task ExistsById_ReturnsTrue_WhenEquipmentExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByIdAsync(
            equipment.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Equipment exists");
        http.Data.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsById_ReturnsFalse_WhenEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByIdAsync(
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
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Equipment does not exist");
        http.Data.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsById_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByIdAsync(
            "invalid-uuid",
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsById_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByIdAsync(
            equipment.Id,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExistsById_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByIdAsync(
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
    public async Task ExistsById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        // Act
        using var response = await SendExistsByIdAsync(
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
    public async Task ExistsById_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Primeira consulta: deve ser processada com sucesso.
        using (var firstResponse = await SendExistsByIdAsync(
                   equipment.Id,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().BeTrue();
        }

        // Segunda consulta reutiliza o mesmo Inbox.
        using var response = await SendExistsByIdAsync(
            equipment.Id,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }


    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendExistsByIdAsync(
        string id,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ExistsUrl}/{Uri.EscapeDataString(id)}");

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