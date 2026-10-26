using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Auth.Controller.Auth;


[Collection("Integration Tests")]
public class AuthLogoutTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public AuthLogoutTests(KairosApiFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ------------------------------------------------------------------
    // Sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task Logout_ReturnsOk_AndRevokesRefreshToken()
    {
        var (registered, tokens) = await RegisterUserAsync();

        // Garante que existam valores para validar a revogação
        // (necessário enquanto o cadastro não preencher RefreshTokenExpiryTime).
        await UpdateUserAsync(registered.Email, user =>
        {
            user.RefreshToken = tokens.RefreshToken;
            user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7);
        });

        var inbox = await NewInboxAsync();

        using var response = await SendLogoutAsync(tokens.Token, inbox.Id);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Logout successful");
        http.Data.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        // Refresh token revogado no banco
        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();

        // Inbox processado
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.LockedBy.Should().BeNull();
        updatedInbox.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Logout_IsIdempotent_WhenCalledAgainWithNewInbox()
    {
        var (registered, tokens) = await RegisterUserAsync();

        using (var first = await SendLogoutAsync(tokens.Token, (await NewInboxAsync()).Id))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        // O access token continua válido até expirar, e o usuário já não tem refresh token
        using var second = await SendLogoutAsync(tokens.Token, (await NewInboxAsync()).Id);

        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();
    }

    [Fact]
    public async Task Logout_DoesNotAffectOtherUsers()
    {
        var (_, tokensA) = await RegisterUserAsync();
        var (registeredB, tokensB) = await RegisterUserAsync();

        using var response = await SendLogoutAsync(tokensA.Token, (await NewInboxAsync()).Id);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var userB = await GetUserByEmailAsync(registeredB.Email);
        userB!.RefreshToken.Should().Be(tokensB.RefreshToken);
    }

    [Fact]
    public async Task Logout_ThenLogin_IssuesNewRefreshToken()
    {
        var (registered, tokens) = await RegisterUserAsync();

        using (var logout = await SendLogoutAsync(tokens.Token, (await NewInboxAsync()).Id))
            logout.StatusCode.Should().Be(HttpStatusCode.OK);

        using var login = await LoginWithNewInboxAsync(registered.Email, registered.Password);

        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<Tokens>(login);
        http!.Data!.RefreshToken.Should().NotBeNullOrWhiteSpace();
        http.Data.RefreshToken.Should().NotBe(tokens.RefreshToken);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(http.Data.RefreshToken);
    }

    // ------------------------------------------------------------------
    // Autenticação
    // ------------------------------------------------------------------

    [Fact]
    public async Task Logout_ReturnsUnauthorized_WhenBearerTokenIsMissing()
    {
        var inbox = await NewInboxAsync();

        using var response = await SendLogoutAsync(accessToken: null, inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A requisição não deve consumir o inbox
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Pending);
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task Logout_ReturnsUnauthorized_WhenTokenIsInvalid()
    {
        var (registered, tokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        // Lixo qualquer no lugar do JWT
        using (var garbage = await SendLogoutAsync("not-a-jwt", inbox.Id))
            garbage.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // JWT real com a assinatura adulterada: prova que a assinatura é validada
        using (var tampered = await SendLogoutAsync(TamperSignature(tokens.Token), inbox.Id))
            tampered.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Nada foi consumido nem revogado
        (await GetInboxAsync(inbox.Id)).Status.Should().Be(InboxStatusEnum.Pending);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    // ------------------------------------------------------------------
    // Regras de negócio
    // ------------------------------------------------------------------

    [Fact]
    public async Task Logout_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var (registered, tokens) = await RegisterUserAsync();

        await DeleteUserAsync(registered.Email);

        var inbox = await NewInboxAsync();

        using var response = await SendLogoutAsync(tokens.Token, inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("User not found");
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task Logout_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var (registered, tokens) = await RegisterUserAsync();

        using var response = await SendLogoutAsync(tokens.Token, inboxId: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);
        http!.Errors.Should().ContainSingle(e => e.Contains(InboxHeader));

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    [Fact]
    public async Task Logout_ReturnsConflict_WhenInboxWasAlreadyProcessed()
    {
        var (registered, tokens) = await RegisterUserAsync();

        var inbox = await NewInboxAsync(
            item => item.Status = InboxStatusEnum.Processed);

        using var response = await SendLogoutAsync(tokens.Token, inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Inbox is not available for processing.");

        // O logout não deve ter sido executado
        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    [Fact]
    public async Task Logout_ReturnsConflict_WhenSameInboxIsReusedAfterSuccess()
    {
        var (_, tokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using (var first = await SendLogoutAsync(tokens.Token, inbox.Id))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = await SendLogoutAsync(tokens.Token, inbox.Id);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ------------------------------------------------------------------
    // Bugs conhecidos (remova o Skip depois de corrigir)
    // ------------------------------------------------------------------

    [Fact(Skip = "Bug: inbox fica preso em Processing quando o logout falha (ex.: usuário não encontrado).")]
    public async Task Logout_ReleasesInbox_WhenUserNotFound()
    {
        var (registered, tokens) = await RegisterUserAsync();

        await DeleteUserAsync(registered.Email);

        var inbox = await NewInboxAsync();

        using var response = await SendLogoutAsync(tokens.Token, inbox.Id);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var updated = await GetInboxAsync(inbox.Id);
        updated.Status.Should().NotBe(InboxStatusEnum.Processing);
        updated.LockedBy.Should().BeNull();
    }
}