using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Permission.Controller;

[Collection("Integration Tests")]
public sealed class PermissionCreateControllerTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;
    private const string CreateUrl = "/api/v1/permissions";

    public PermissionCreateControllerTest(
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
    public async Task Create_ReturnsCreated_AndPersistsPermissionAndInbox()
    {
        var tokens = await LoginUserMaster();
        
        var permission = await CreatePermissionHttp(tokens);

        permission.Should().NotBeNull();
        permission.Id.Should().NotBeNullOrWhiteSpace();
        permission.Code.Should().StartWith("test.permission.");
    }

    // ---------------------------------------------------------------
    // Validação do DTO (Model State Validation)
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("Code")]
    [InlineData("Name")]
    public async Task Create_ReturnsBadRequest_WhenRequiredFieldIsEmpty(string invalidField)
    {
        var tokens = await LoginUserMaster();

        var dto = invalidField switch
        {
            "Code" => NewPermissionDto(code: ""),
            "Name" => NewPermissionDto(name: ""),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField))
        };

        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        
        using var scope = CreateScope();
        var persisted = await GetPermissionRepository(scope).GetByCodeAsync(dto.Code);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCodeHasInvalidCharacters()
    {
        var tokens = await LoginUserMaster();
        var dto = NewPermissionDto(code: "invalid code with spaces");
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCodeExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var dto = NewPermissionDto(code: new string('a', 101));
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var dto = NewPermissionDto(name: new string('a', 101));
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDescriptionExceedsMaximumLength()
    {
        var tokens = await LoginUserMaster();
        var dto = NewPermissionDto(description: new string('a', 256));
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // Regras de Negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConflict_WhenCodeAlreadyExists()
    {
        var tokens = await LoginUserMaster();
        var dto = NewPermissionDto();

        using (var firstResponse = await SendCreateAsync(dto, Guid.NewGuid().ToString(), tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var response = await SendCreateAsync(dto, Guid.NewGuid().ToString(), tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<PermissionDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(error =>
            error.Contains($"Permission with code '{dto.Code}' already exists.", StringComparison.OrdinalIgnoreCase));

        using var scope = CreateScope();
        var persisted = await GetPermissionRepository(scope).GetByCodeAsync(dto.Code);
        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(dto.Code);
    }

    // ---------------------------------------------------------------
    // Autenticação e Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var dto = NewPermissionDto();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(dto, inboxId, accessToken: null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ReturnsForbidden_WhenUserLacksPermission()
    {
        var (_, tokens) = await RegisterUserAsync(); // Usuário comum sem permissão "identity.permissions.create"
        var dto = NewPermissionDto();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var tokens = await LoginUserMaster();
        var dto = NewPermissionDto();

        using var response = await SendCreateAsync(dto, inboxId: null, accessToken: tokens.Token);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().ContainSingle(error => error.Contains(InboxHeader));
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();
        var dto = NewPermissionDto();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<PermissionDto>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data!.Code.Should().Be(dto.Code);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();
        var firstDto = NewPermissionDto();
        var secondDto = NewPermissionDto();

        using (var firstResponse = await SendCreateAsync(firstDto, inboxId, tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var response = await SendCreateAsync(secondDto, inboxId, tokens.Token);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        CreatePermissionDto dto,
        string? inboxId,
        string? accessToken)
    {
        return SendAsync(
            CreateUrl,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private static CreatePermissionDto NewPermissionDto(
        string? code = null,
        string? name = null,
        string? description = null)
    {
        return new CreatePermissionDto
        {
            Code = code ?? $"test.permission.{Guid.NewGuid():N}",
            Name = name ?? "Integration Test Permission",
            Module = PermissionModuleEnum.Identity,
            Description = description ?? "Created by integration tests."
        };
    }
}