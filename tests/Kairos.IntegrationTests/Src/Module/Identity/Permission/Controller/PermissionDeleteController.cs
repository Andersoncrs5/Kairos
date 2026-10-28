using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Permission.Controller;

[Collection("Integration Tests")]
public sealed class PermissionDeleteControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public PermissionDeleteControllerTest(
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
    public async Task Delete_ReturnsOk_AndDeletesPermissionAndProcessesInbox()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            permission.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Permission deleted");

        // A permissão não deve mais ser encontrada pelo repositório.
        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().BeNull();

        // O Inbox deve ter sido processado.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenIdIsNotValidUuid()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            "invalid-uuid",
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

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
    public async Task Delete_ReturnsNotFound_WhenPermissionDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            nonexistentId,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Permission not found.");
    }

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenPermissionIsSystem()
    {
        var tokens = await LoginUserMaster();

        PermissionsModel systemPermission;

        using (var scope = CreateScope())
        {
            systemPermission = await CreatePermissionAsync(
                scope.ServiceProvider,
                permission => permission.IsSystem = true);
        }

        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            systemPermission.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Permission is system and cannot be deleted.");

        // Uma permissão de sistema deve continuar existindo.
        using var verificationScope = CreateScope();

        var persisted = await GetPermissionRepository(verificationScope)
            .GetByIdAsync(systemPermission.Id);

        persisted.Should().NotBeNull();
        persisted!.IsSystem.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenUserLacksDeletePermission()
    {
        var masterTokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendDeleteAsync(
            permission.Id,
            regularUserTokens.Token,
            inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A requisição não deve excluir a permissão.
        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().NotBeNull();
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);

        using var response = await SendDeleteAsync(
            permission.Id,
            tokens.Token,
            inboxId: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Sem Inbox válido, a permissão não deve ser excluída.
        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        using var response = await SendDeleteAsync(
            permission.Id,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Inbox is not available for processing.");

        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        var inbox = await NewInboxAsync();

        using (var firstResponse = await SendDeleteAsync(
                   permission.Id,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Reutiliza o mesmo Inbox, já processado.
        using var response = await SendDeleteAsync(
            permission.Id,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);

        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendDeleteAsync(
        string id,
        string accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/permissions/{Uri.EscapeDataString(id)}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }
}