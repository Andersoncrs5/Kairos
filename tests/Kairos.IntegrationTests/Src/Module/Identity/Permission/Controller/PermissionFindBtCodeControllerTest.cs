using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Permission.Controller;

[Collection("Integration Tests")]
public sealed class PermissionFindBtCodeControllerTest
    : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public PermissionFindBtCodeControllerTest(
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
    public async Task FindByCode_ReturnsOk_WhenPermissionExists()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByCodeAsync(
            permission.Code,
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Permission found");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(permission.Id);
        http.Data.Code.Should().Be(permission.Code);
        http.Data.Name.Should().Be(permission.Name);
        http.Data.Module.Should().Be(permission.Module);
        http.Data.IsSystem.Should().BeFalse();
        http.Data.Description.Should().Be(permission.Description);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindByCode_ReturnsNotFound_WhenPermissionDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var code = $"missing.permission.{Guid.NewGuid():N}";
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByCodeAsync(
            code,
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

    }

    [Fact]
    public async Task FindByCode_ReturnsBadRequest_WhenCodeIsWhitespace()
    {
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByCodeAsync(
            "   ",
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindByCode_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        var masterTokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByCodeAsync(
            permission.Code,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FindByCode_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByCodeAsync(
            "some.permission",
            accessToken: null,
            inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindByCode_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);

        using var response = await SendFindByCodeAsync(
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
    public async Task FindByCode_ReturnsOk_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByCodeAsync(
            permission.Code,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Permission found");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(permission.Id);
        http.Data.Code.Should().Be(permission.Code);
        http.Data.Name.Should().Be(permission.Name);
        http.Data.Module.Should().Be(permission.Module);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task FindByCode_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        using (var firstResponse = await SendFindByCodeAsync(
                   permission.Code,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var response = await SendFindByCodeAsync(
            permission.Code,
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendFindByCodeAsync(
        string code,
        string? accessToken,
        string? inboxId)
    {
        var url =
            $"/api/v1/permissions/code/{Uri.EscapeDataString(code)}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            url);

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