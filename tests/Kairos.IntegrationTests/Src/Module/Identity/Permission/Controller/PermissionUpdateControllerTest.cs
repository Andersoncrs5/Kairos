using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Permission.Controller;

[Collection("Integration Tests")]
public sealed class PermissionUpdateControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public PermissionUpdateControllerTest(
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
    public async Task Update_ReturnsOk_AndPersistsChanges()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        var inbox = await NewInboxAsync();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(
            permission.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Permission updated successfully");
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(permission.Id);
        http.Data.Name.Should().Be(dto.Name);
        http.Data.Description.Should().Be(dto.Description);
        http.Data.Code.Should().Be(permission.Code);
        http.Data.Module.Should().Be(permission.Module);
        http.Data.IsSystem.Should().BeFalse();

        // Confirma a persistência no banco.
        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(dto.Name);
        persisted.Description.Should().Be(dto.Description);
        persisted.Code.Should().Be(permission.Code);

        // Confirma o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdIsInvalidUuid()
    {
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(
            "invalid-uuid",
            dto,
            tokens.Token,
            inbox.Id);

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
    public async Task Update_ReturnsNotFound_WhenPermissionDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(
            nonexistentId,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Permission not found.");
    }

    [Fact]
    public async Task Update_ReturnsForbidden_WhenPermissionIsSystem()
    {
        var tokens = await LoginUserMaster();

        Kairos.Api.Modules.Identity.Permissions.Models.PermissionsModel
            systemPermission;

        using (var scope = CreateScope())
        {
            systemPermission = await CreatePermissionAsync(
                scope.ServiceProvider,
                permission => permission.IsSystem = true);
        }

        var inbox = await NewInboxAsync();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(
            systemPermission.Id,
            dto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "System permissions cannot be modified.");

        // A permissão de sistema não pode ser alterada.
        using var verificationScope = CreateScope();

        var persisted = await GetPermissionRepository(verificationScope)
            .GetByIdAsync(systemPermission.Id);

        persisted.Should().NotBeNull();
        persisted!.IsSystem.Should().BeTrue();
        persisted.Name.Should().Be(systemPermission.Name);
    }

    // ---------------------------------------------------------------
    // Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksUpdatePermission()
    {
        var masterTokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(masterTokens);

        var (_, regularUserTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(
            permission.Id,
            dto,
            regularUserTokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A permissão continua com os valores originais.
        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(permission.Name);
        persisted.Description.Should().Be(permission.Description);
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var permissionTokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(permissionTokens);
        var inbox = await NewInboxAsync();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(
            permission.Id,
            dto,
            accessToken: null,
            inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(
            permission.Id,
            dto,
            tokens.Token,
            inboxId: null);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().ContainSingle(error =>
            error.Contains(InboxHeader));

        // Sem Inbox, a atualização não deve ocorrer.
        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(permission.Name);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(
            permission.Id,
            dto,
            tokens.Token,
            nonexistentInboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Inbox is not available for processing.");

        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(permission.Name);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        var inbox = await NewInboxAsync();

        var firstDto = NewUpdatePermissionDto();
        var secondDto = new UpdatePermissionDto
        {
            Name = "Second update",
            Description = "This update must not execute.",
            IsActive = false
        };

        using (var firstResponse = await SendUpdateAsync(
                   permission.Id,
                   firstDto,
                   tokens.Token,
                   inbox.Id))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var response = await SendUpdateAsync(
            permission.Id,
            secondDto,
            tokens.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<PermissionDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        // A segunda atualização não deve sobrescrever a primeira.
        using var scope = CreateScope();

        var persisted = await GetPermissionRepository(scope)
            .GetByIdAsync(permission.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(firstDto.Name);
        persisted.Description.Should().Be(firstDto.Description);
        
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendUpdateAsync(
        string id,
        UpdatePermissionDto dto,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/permissions/{Uri.EscapeDataString(id)}")
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

    private static UpdatePermissionDto NewUpdatePermissionDto()
    {
        return new UpdatePermissionDto
        {
            Name = "Updated permission",
            Description = "Updated description",
            IsActive = false
        };
    }
}