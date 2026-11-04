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
        await UpdateUserAsync(registered.Email, user =>
        {
            user.RefreshToken = tokens.RefreshToken;
            user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7);
        });

        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendLogoutAsync(tokens.Token, inboxId);
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

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Logout_IsIdempotent_WhenCalledAgainWithNewInbox()
    {
        var (_, tokens) = await RegisterUserAsync();

        string firstInboxId = Guid.NewGuid().ToString();

        using (var first = await SendLogoutAsync(tokens.Token, firstInboxId))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        // O access token continua válido até expirar, e o usuário já não tem refresh token
        string secondInboxId = Guid.NewGuid().ToString();

        using var second = await SendLogoutAsync(tokens.Token, secondInboxId);

        second.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertInboxProcessedAsync(firstInboxId);
        await AssertInboxProcessedAsync(secondInboxId);
    }

    [Fact]
    public async Task Logout_DoesNotAffectOtherUsers()
    {
        var (_, tokensA) = await RegisterUserAsync();
        var (registeredB, tokensB) = await RegisterUserAsync();

        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendLogoutAsync(tokensA.Token, inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var userB = await GetUserByEmailAsync(registeredB.Email);

        userB.Should().NotBeNull();
        userB!.RefreshToken.Should().Be(tokensB.RefreshToken);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Logout_ThenLogin_IssuesNewRefreshToken()
    {
        var (registered, tokens) = await RegisterUserAsync();

        string inboxId = Guid.NewGuid().ToString();

        using (var logout = await SendLogoutAsync(tokens.Token, inboxId))
            logout.StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertInboxProcessedAsync(inboxId);

        using var login = await LoginWithNewInboxAsync(
            registered.Email,
            registered.Password);

        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<Tokens>(login);

        http.Should().NotBeNull();
        http!.Data!.RefreshToken.Should().NotBeNullOrWhiteSpace();
        http.Data.RefreshToken.Should().NotBe(tokens.RefreshToken);

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(http.Data.RefreshToken);
    }

    // ------------------------------------------------------------------
    // Autenticação
    // ------------------------------------------------------------------

    [Fact]
    public async Task Logout_ReturnsUnauthorized_WhenBearerTokenIsMissing()
    {
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendLogoutAsync(
            accessToken: null,
            inboxId: inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);


    }

    [Fact]
    public async Task Logout_ReturnsUnauthorized_WhenTokenIsInvalid()
    {
        var (registered, tokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        // Lixo qualquer no lugar do JWT
        using (var garbage = await SendLogoutAsync("not-a-jwt", inboxId))
            garbage.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // JWT real com a assinatura adulterada: prova que a assinatura é validada
        using (var tampered = await SendLogoutAsync(
                   TamperSignature(tokens.Token),
                   inboxId: Guid.NewGuid().ToString()))
        {
            tampered.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        
        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
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

        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendLogoutAsync(tokens.Token, inboxId);

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

        using var response = await SendLogoutAsync(
            tokens.Token,
            inboxId: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().ContainSingle(error => error.Contains(InboxHeader));

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    [Fact]
    public async Task Logout_ReturnsConflict_WhenSameInboxIsReusedAfterSuccess()
    {
        var (_, tokens) = await RegisterUserAsync();

        string inboxId = Guid.NewGuid().ToString();

        using (var first = await SendLogoutAsync(tokens.Token, inboxId))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = await SendLogoutAsync(tokens.Token, inboxId);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------
    // Bugs conhecidos (remova o Skip depois de corrigir)
    // ------------------------------------------------------------------

    [Fact(Skip = "Bug: inbox fica preso em Processing quando o logout falha (ex.: usuário não encontrado).")]
    public async Task Logout_ReleasesInbox_WhenUserNotFound()
    {
        var (registered, tokens) = await RegisterUserAsync();

        await DeleteUserAsync(registered.Email);

        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendLogoutAsync(tokens.Token, inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
    }
}