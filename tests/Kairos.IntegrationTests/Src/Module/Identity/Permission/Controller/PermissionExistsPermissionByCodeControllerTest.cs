using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Permission.Controller;

[Collection("Integration Tests")]
public sealed class PermissionExistsPermissionByCodeControllerTest
    : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public PermissionExistsPermissionByCodeControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Consulta
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByCode_ReturnsTrue_WhenPermissionExists()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendExistsAsync(
            permission.Code,
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("Permission exists");
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task ExistsByCode_ReturnsFalse_WhenPermissionDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var code = $"missing.permission.{Guid.NewGuid():N}";
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendExistsAsync(
            code,
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
        http.Message.Should().Be("Permission does not exist");

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task ExistsByCode_ReturnsTrue_WhenCodeHasDifferentCasing()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendExistsAsync(
            permission.Code.ToUpperInvariant(),
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByCode_ReturnsBadRequest_WhenCodeExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var code = new string('a', 101);
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendExistsAsync(
            code,
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByCode_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        var masterTokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendExistsAsync(
            permission.Code,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByCode_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);

        using var response = await SendExistsAsync(
            permission.Code,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().ContainSingle(error =>
            error.Contains(InboxHeader));
    }

    [Fact]
    public async Task ExistsByCode_ReturnsOk_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            permission.Code,
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
        http.Message.Should().Be("Permission exists");

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task ExistsByCode_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        using (var firstResponse = await SendExistsAsync(
                   permission.Code,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var response = await SendExistsAsync(
            permission.Code,
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------------------------------------------------------------
    // HTTP helper
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendExistsAsync(
        string code,
        string accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/permissions/Exists/code/{Uri.EscapeDataString(code)}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }
}