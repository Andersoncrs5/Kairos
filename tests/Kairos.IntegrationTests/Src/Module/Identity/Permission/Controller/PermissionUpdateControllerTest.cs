using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
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
    private const string BaseUrl = "/api/v1/permissions";

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
        string inboxId = Guid.NewGuid().ToString();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(
            permission.Id,
            dto,
            tokens.Token,
            inboxId);

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
        var persisted = await GetPermissionRepository(scope).GetByIdAsync(permission.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(dto.Name);
        persisted.Description.Should().Be(dto.Description);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validação do DTO e UUID
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdIsInvalidUuid()
    {
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync("invalid-uuid", dto, tokens.Token, inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<PermissionDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        var dto = new UpdatePermissionDto { Name = new string('a', 101) };
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendUpdateAsync(permission.Id, dto, tokens.Token, inboxId);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenDescriptionExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        var dto = new UpdatePermissionDto { Description = new string('a', 256) };
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendUpdateAsync(permission.Id, dto, tokens.Token, inboxId);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // Regras de Negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenPermissionDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(nonexistentId, dto, tokens.Token, inboxId);
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
        Kairos.Api.Modules.Identity.Permissions.Models.PermissionsModel systemPermission;

        using (var scope = CreateScope())
        {
            systemPermission = await CreatePermissionAsync(
                scope.ServiceProvider,
                permission => permission.IsSystem = true);
        }

        string inboxId = Guid.NewGuid().ToString();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(systemPermission.Id, dto, tokens.Token, inboxId);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var http = await ReadAsync<PermissionDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("System permissions cannot be modified.");

        using var verificationScope = CreateScope();
        var persisted = await GetPermissionRepository(verificationScope).GetByIdAsync(systemPermission.Id);
        persisted.Should().NotBeNull();
        persisted!.IsSystem.Should().BeTrue();
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
        string inboxId = Guid.NewGuid().ToString();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(permission.Id, dto, regularUserTokens.Token, inboxId);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var permissionTokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(permissionTokens);
        string inboxId = Guid.NewGuid().ToString();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(permission.Id, dto, accessToken: null, inboxId);
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

        using var response = await SendUpdateAsync(permission.Id, dto, tokens.Token, inboxId: null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().ContainSingle(error => error.Contains(InboxHeader));
    }

    [Fact]
    public async Task Update_ReturnsCreated_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();
        var dto = NewUpdatePermissionDto();

        using var response = await SendUpdateAsync(permission.Id, dto, tokens.Token, inboxId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PermissionDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        var permission = await CreatePermissionHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        var firstDto = NewUpdatePermissionDto();
        var secondDto = new UpdatePermissionDto { Name = "Second update" };

        using (var firstResponse = await SendUpdateAsync(permission.Id, firstDto, tokens.Token, inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var response = await SendUpdateAsync(permission.Id, secondDto, tokens.Token, inboxId);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
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
            $"{BaseUrl}/{Uri.EscapeDataString(id)}")
        {
            Content = JsonContent.Create(dto)
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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