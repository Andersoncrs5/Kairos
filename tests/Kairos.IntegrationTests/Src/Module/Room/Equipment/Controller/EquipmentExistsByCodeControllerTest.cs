using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Room.Equipment.Controller;

[Collection("Integration Tests")]
public sealed class EquipmentExistsByCodeControllerTest : BaseIntegrationTest
{
    private const string ExistsUrl = "/api/v1/equipments/Exists/Code";

    private readonly ITestOutputHelper _output;

    public EquipmentExistsByCodeControllerTest(
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
    public async Task ExistsByCode_ReturnsTrue_WhenEquipmentExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByCodeAsync(
            equipment.Code,
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
    public async Task ExistsByCode_ReturnsFalse_WhenEquipmentDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentCode = $"NONEXISTENT-{Guid.NewGuid():N}";
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByCodeAsync(
            nonexistentCode,
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
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByCode_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        // Arrange
        var masterTokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByCodeAsync(
            equipment.Code,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExistsByCode_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsByCodeAsync(
            "EQ-TEST",
            accessToken: null,
            inboxId: inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByCode_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);

        // Act
        using var response = await SendExistsByCodeAsync(
            equipment.Code,
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
    public async Task ExistsByCode_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var equipment = await CreateEquipmentHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Primeira consulta: deve ser processada com sucesso.
        using (var firstResponse = await SendExistsByCodeAsync(
                   equipment.Code,
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
        using var response = await SendExistsByCodeAsync(
            equipment.Code,
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

    private async Task<HttpResponseMessage> SendExistsByCodeAsync(
        string code,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ExistsUrl}/{Uri.EscapeDataString(code)}");

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