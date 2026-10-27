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
        // Arrange: cria um usuário comum como alvo da consulta.
        Tokens targetUser = await CreateUserHttp();

        // A requisição de listagem deve usar o token do master.
        Tokens master = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindAllAsync(
            targetUser.User.Id,
            master.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert: status e envelope da resposta.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<Page<UserDTO>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Users retrieved successfully");
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();
        http.Data!.PageNumber.Should().Be(1);
        http.Data.PageSize.Should().Be(20);
        http.Data.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        http.Data.Data.Should().Contain(user => user.Id == targetUser.User.Id);

        // Assert: a inbox usada na consulta foi processada.
        var processedInbox = await GetInboxAsync(inbox.Id);
        processedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        processedInbox.ProcessedAt.Should().NotBeNull();
        processedInbox.LockedBy.Should().BeNull();
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
        var inbox = await NewInboxAsync();

        regularUser.Permissions.Should().NotContain(ListPermission);

        // Act: usa o token do usuário comum, nunca o token do master.
        using var response = await SendFindAllAsync(
            targetUser.User.Id,
            regularUser.Token,
            inbox.Id);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert: autenticação não concede a permissão de listagem.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A autorização deve bloquear antes do processamento da inbox.
        var unchangedInbox = await GetInboxAsync(inbox.Id);
        unchangedInbox.Status.Should().Be(InboxStatusEnum.Pending);
        unchangedInbox.ProcessedAt.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Autenticação
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindAll_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        Tokens targetUser = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindAllAsync(
            targetUser.User.Id,
            accessToken: null,
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var unchangedInbox = await GetInboxAsync(inbox.Id);
        unchangedInbox.Status.Should().Be(InboxStatusEnum.Pending);
    }

    [Fact]
    public async Task FindAll_ReturnsUnauthorized_WhenAccessTokenIsInvalid()
    {
        // Arrange
        Tokens targetUser = await CreateUserHttp();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindAllAsync(
            targetUser.User.Id,
            accessToken: "not-a-valid-jwt",
            inbox.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var unchangedInbox = await GetInboxAsync(inbox.Id);
        unchangedInbox.Status.Should().Be(InboxStatusEnum.Pending);
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
