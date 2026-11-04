using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.User.Controller;

[Collection("Integration Tests")]
public class UserFindAllController : BaseIntegrationTest
{
    private const string FindAllUrl = "/api/v1/users";
    private const string ListPermission = "identity.users.list";

    private readonly ITestOutputHelper _output;

    public UserFindAllController(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    private static string BuildFindAllUrl(
        string userId,
        int page = 1,
        int pageSize = 20)
    {
        string createdAtMin = Uri.EscapeDataString(
            DateTimeOffset.MinValue.ToString("O"));

        string createdAtMax = Uri.EscapeDataString(
            DateTimeOffset.MaxValue.ToString("O"));

        return $"{FindAllUrl}?Id={Uri.EscapeDataString(userId)}" +
               $"&CreatedAtMin={createdAtMin}" +
               $"&CreatedAtMax={createdAtMax}" +
               $"&Page={page}" +
               $"&PageSize={pageSize}";
    }

    private async Task<HttpResponseMessage> SendFindAllAsync(
        string userId,
        string? accessToken,
        string? inboxId,
        int page = 1,
        int pageSize = 20)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildFindAllUrl(userId, page, pageSize));

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

    // ------------------------------------------------------------------
    // Sucesso: somente o usuário master possui a permissão de listagem.
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindAll_ReturnsOk_WhenMasterListsUsers()
    {
        // Arrange
        Tokens targetUser = await CreateUserHttp();
        Tokens master = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindAllAsync(
            targetUser.User.Id,
            master.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert: status HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

    }
    // ------------------------------------------------------------------
    // Permissões: um usuário comum não pode listar usuários.
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindAll_ReturnsForbidden_WhenRegularUserDoesNotHaveListPermission()
    {
        // Arrange
        Tokens targetUser = await CreateUserHttp();
        Tokens regularUser = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        regularUser.Permissions.Should().NotContain(ListPermission);

        // Act: usa o token do usuário comum, nunca o token do master.
        using var response = await SendFindAllAsync(
            targetUser.User.Id,
            regularUser.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------
    // Autenticação
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindAll_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        Tokens targetUser = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindAllAsync(
            targetUser.User.Id,
            accessToken: null,
            inboxId);

        
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FindAll_ReturnsUnauthorized_WhenAccessTokenIsInvalid()
    {
        // Arrange
        Tokens targetUser = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindAllAsync(
            targetUser.User.Id,
            accessToken: "not-a-valid-jwt",
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindAll_ReturnsBadRequest_WhenMasterRequestHasNoInboxHeader()
    {
        // Arrange: usa master para não falhar primeiro na autorização.
        Tokens targetUser = await CreateUserHttp();
        Tokens master = await LoginUserMaster();

        // Act
        using var response = await SendFindAllAsync(
            targetUser.User.Id,
            master.Token,
            inboxId: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
