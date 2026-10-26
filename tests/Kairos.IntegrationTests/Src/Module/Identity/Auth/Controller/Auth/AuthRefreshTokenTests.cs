using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.IntegrationTests.Config;

namespace Kairos.IntegrationTests.Module.Identity.Auth.Controller.Auth;

[Collection("Integration Tests")]
public class AuthRefreshTokenTests(KairosApiFactory factory) : BaseIntegrationTest(factory)
{
    protected const string RefreshTokenUrl = "/api/v1/auth/RefreshToken";
    
    protected Task<HttpResponseMessage> SendRefreshTokenAsync(string? refreshToken, string? inboxId)
        => SendAsync(RefreshTokenUrl, new { RefreshToken = refreshToken }, inboxId: inboxId);

    protected async Task<HttpResponseMessage> RefreshWithNewInboxAsync(string refreshToken)
        => await SendRefreshTokenAsync(refreshToken, (await NewInboxAsync()).Id);

    protected async Task<(CreateUserDTO Dto, Tokens Tokens)> RegisterUserWithRefreshExpiryAsync()
    {
        var (dto, tokens) = await RegisterUserAsync();

        await UpdateUserAsync(dto.Email, user =>
            user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7));

        return (dto, tokens);
    }
    
     [Fact]
    public async Task RefreshToken_ReturnsOk_AndRotatesTokens()
    {
        var (registered, tokens) = await RegisterUserWithRefreshExpiryAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendRefreshTokenAsync(tokens.RefreshToken, inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<Tokens>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Refresh token successfully");
        http.TraceId.Should().Be(inbox.Id);
        http.Data.Should().NotBeNull();
        http.Data!.Token.Should().NotBeNullOrWhiteSpace();
        http.Data.ExpiredToken.Should().BeAfter(DateTimeOffset.UtcNow);
        http.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        http.Data.RefreshToken.Should().NotBe(tokens.RefreshToken);
        http.Data.User.Id.Should().Be(tokens.User.Id);

        // O novo refresh token e a nova expiração ficam salvos no banco
        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(http.Data.RefreshToken);
        user.RefreshTokenExpiryTime.Should().BeAfter(DateTimeOffset.UtcNow);

        // O novo JWT identifica o usuário certo
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(http.Data.Token);
        jwt.Subject.Should().Be(user.Id);

        // O inbox foi consumido
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task RefreshToken_NewTokenCanBeUsedAgain()
    {
        var (_, tokens) = await RegisterUserWithRefreshExpiryAsync();

        using var first = await RefreshWithNewInboxAsync(tokens.RefreshToken);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotated = (await ReadAsync<Tokens>(first))!.Data!;

        using var second = await RefreshWithNewInboxAsync(rotated.RefreshToken);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotatedAgain = (await ReadAsync<Tokens>(second))!.Data!;
        rotatedAgain.RefreshToken.Should().NotBe(rotated.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_NewAccessTokenAuthorizesProtectedEndpoints()
    {
        var (_, tokens) = await RegisterUserWithRefreshExpiryAsync();

        using var refresh = await RefreshWithNewInboxAsync(tokens.RefreshToken);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotated = (await ReadAsync<Tokens>(refresh))!.Data!;

        using var logout = await SendLogoutAsync(rotated.Token, (await NewInboxAsync()).Id);

        logout.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------
    // Refresh token inválido
    // ------------------------------------------------------------------

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenOldTokenIsReused()
    {
        var (_, tokens) = await RegisterUserWithRefreshExpiryAsync();

        using (var first = await RefreshWithNewInboxAsync(tokens.RefreshToken))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        // O token antigo foi substituído na rotação: reuso deve falhar
        using var replay = await RefreshWithNewInboxAsync(tokens.RefreshToken);

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var http = await ReadAsync<Tokens>(replay);

        http!.Success.Should().BeFalse();
        http.Message.Should().Be("Refresh token failed");
        http.Data.Should().BeNull();
        http.Errors.Should().Contain("Refresh token invalid");
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenTokenIsUnknown()
    {
        var unknown = Convert.ToBase64String(RandomNumberGenerator.GetBytes(128));

        using var response = await RefreshWithNewInboxAsync(unknown);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var http = await ReadAsync<Tokens>(response);

        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Refresh token invalid");
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenTokenIsExpired()
    {
        var (registered, tokens) = await RegisterUserWithRefreshExpiryAsync();

        await UpdateUserAsync(registered.Email,
            user => user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddMinutes(-1));

        using var response = await RefreshWithNewInboxAsync(tokens.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Nada foi rotacionado
        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenExpiryIsNotSet()
    {
        var (registered, tokens) = await RegisterUserAsync();

        await UpdateUserAsync(registered.Email,
            user => user.RefreshTokenExpiryTime = null);

        using var response = await RefreshWithNewInboxAsync(tokens.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenUserDoesNotExist()
    {
        var (registered, tokens) = await RegisterUserWithRefreshExpiryAsync();

        await DeleteUserAsync(registered.Email);

        using var response = await RefreshWithNewInboxAsync(tokens.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_AfterLogout()
    {
        var (_, tokens) = await RegisterUserWithRefreshExpiryAsync();

        using (var logout = await SendLogoutAsync(tokens.Token, (await NewInboxAsync()).Id))
            logout.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await RefreshWithNewInboxAsync(tokens.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_AfterPasswordChange()
    {
        var (registered, tokens) = await RegisterUserWithRefreshExpiryAsync();

        const string newPassword = "ChangedPassword123!";

        using (var change = await SendChangePasswordAsync(
                   new ChangePasswordUserDto(registered.Password, newPassword, newPassword),
                   tokens.Token,
                   (await NewInboxAsync()).Id))
        {
            change.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var response = await RefreshWithNewInboxAsync(tokens.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------
    // Validação de model
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Missing")]
    [InlineData("Empty")]
    public async Task RefreshToken_ReturnsBadRequest_WhenRefreshTokenIsBlank(string scenario)
    {
        object body = scenario switch
        {
            "Missing" => new { },
            "Empty" => new { RefreshToken = "" },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var inbox = await NewInboxAsync();

        using var response = await SendAsync(RefreshTokenUrl, body, inboxId: inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(response);
        http!.Success.Should().BeFalse();

        // A action não rodou: o inbox não foi tocado
        (await GetInboxAsync(inbox.Id)).Status.Should().Be(InboxStatusEnum.Pending);
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task RefreshToken_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var (registered, tokens) = await RegisterUserWithRefreshExpiryAsync();

        using var response = await SendRefreshTokenAsync(tokens.RefreshToken, inboxId: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);
        http!.Errors.Should().ContainSingle(e => e.Contains(InboxHeader));

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var (registered, tokens) = await RegisterUserWithRefreshExpiryAsync();

        using var response = await SendRefreshTokenAsync(
            tokens.RefreshToken,
            Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<Tokens>(response);
        http!.Errors.Should().Contain("Inbox is not available for processing.");

        // O use case nem chegou a rodar
        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_ReturnsConflict_WhenSameInboxIsReusedAfterSuccess()
    {
        var (_, tokens) = await RegisterUserWithRefreshExpiryAsync();
        var inbox = await NewInboxAsync();

        using var first = await SendRefreshTokenAsync(tokens.RefreshToken, inbox.Id);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotated = (await ReadAsync<Tokens>(first))!.Data!;

        // Mesmo com um refresh token válido, o inbox já foi consumido
        using var second = await SendRefreshTokenAsync(rotated.RefreshToken, inbox.Id);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RefreshToken_AllowsOnlyOneRequest_WhenSameInboxIsUsedConcurrently()
    {
        var (registered, tokens) = await RegisterUserWithRefreshExpiryAsync();
        var inbox = await NewInboxAsync();

        var responses = await Task.WhenAll(
            SendRefreshTokenAsync(tokens.RefreshToken, inbox.Id),
            SendRefreshTokenAsync(tokens.RefreshToken, inbox.Id));

        try
        {
            responses.Select(r => r.StatusCode)
                .Should()
                .BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });

            (await GetInboxAsync(inbox.Id)).Status.Should().Be(InboxStatusEnum.Processed);

            // O token salvo é o da requisição que ganhou
            var winner = responses.Single(r => r.StatusCode == HttpStatusCode.OK);
            var rotated = (await ReadAsync<Tokens>(winner))!.Data!;

            var user = await GetUserByEmailAsync(registered.Email);
            user!.RefreshToken.Should().Be(rotated.RefreshToken);
        }
        finally
        {
            foreach (var r in responses) r.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // Bugs e lacunas conhecidos (remova o Skip depois de corrigir)
    // ------------------------------------------------------------------

    [Fact(Skip = "Bug: inbox fica preso em Processing quando o refresh falha.")]
    public async Task RefreshToken_ReleasesInbox_WhenTokenIsInvalid()
    {
        var inbox = await NewInboxAsync();
        var unknown = Convert.ToBase64String(RandomNumberGenerator.GetBytes(128));

        using var response = await SendRefreshTokenAsync(unknown, inbox.Id);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updated = await GetInboxAsync(inbox.Id);
        updated.Status.Should().NotBe(InboxStatusEnum.Processing);
        updated.LockedBy.Should().BeNull();
    }

    [Fact(Skip = "Lacuna: sem proteção contra corrida, dois refresh simultâneos com o mesmo token (inboxes diferentes) podem ter sucesso.")]
    public async Task RefreshToken_AllowsOnlyOneRequest_WhenSameTokenIsUsedConcurrently()
    {
        var (_, tokens) = await RegisterUserWithRefreshExpiryAsync();

        var responses = await Task.WhenAll(
            SendRefreshTokenAsync(tokens.RefreshToken, (await NewInboxAsync()).Id),
            SendRefreshTokenAsync(tokens.RefreshToken, (await NewInboxAsync()).Id));

        try
        {
            responses.Select(r => r.StatusCode)
                .Should()
                .BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        }
        finally
        {
            foreach (var r in responses) r.Dispose();
        }
    }
}