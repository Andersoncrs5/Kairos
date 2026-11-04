using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Permission.Controller;

[Collection("Integration Tests")]
public sealed class PermissionFindByIdControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public PermissionFindByIdControllerTest(
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
    public async Task FindById_ReturnsOk_WhenPermissionExists()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByIdAsync(
            permission.Id,
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
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByIdAsync(
            "invalid-uuid",
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenPermissionDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByIdAsync(
            nonexistentId,
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Permission not found.");
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        var masterTokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByIdAsync(
            permission.Id,
            regularUserTokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FindById_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByIdAsync(
            Guid.NewGuid().ToString(),
            accessToken: null,
            inboxId: inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);

        using var response = await SendFindByIdAsync(
            permission.Id,
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
    public async Task FindById_ReturnsOk_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendFindByIdAsync(
            permission.Id,
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
    public async Task FindById_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        using (var firstResponse = await SendFindByIdAsync(
                   permission.Id,
                   tokens.Token,
                   inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var response = await SendFindByIdAsync(
            permission.Id,
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

    private async Task<HttpResponseMessage> SendFindByIdAsync(
        string id,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/permissions/{Uri.EscapeDataString(id)}");

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