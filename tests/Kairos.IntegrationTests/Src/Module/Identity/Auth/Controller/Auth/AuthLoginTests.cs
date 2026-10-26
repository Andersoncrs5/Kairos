using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Auth.Controller.Auth;


[Collection("Integration Tests")]
public class AuthLoginTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    public AuthLoginTests(KairosApiFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ------------------------------------------------------------------
    // Sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task Login_ReturnsOk_AndRotatesRefreshToken()
    {
        var (registered, registeredTokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendLoginAsync(
            new LoginUserDTO { Email = registered.Email, Password = registered.Password },
            inbox.Id);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<Tokens>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Welcome again");
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();
        http.Data!.Token.Should().NotBeNullOrWhiteSpace();
        http.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        http.Data.ExpiredToken.Should().BeAfter(DateTimeOffset.UtcNow);
        http.Data.User.Id.Should().Be(registeredTokens.User.Id);
        http.Data.User.Email.Should().Be(registered.Email);

        // O refresh token é rotacionado e o novo valor fica salvo no banco
        http.Data.RefreshToken.Should().NotBe(registeredTokens.RefreshToken);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(http.Data.RefreshToken);

        // O JWT identifica o usuário certo
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(http.Data.Token);
        jwt.Subject.Should().Be(user.Id);

        // O inbox foi consumido
        var updatedInbox = await GetInboxAsync(inbox.Id);
        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task Login_ReturnsOk_WhenEmailHasDifferentCasing()
    {
        var (registered, _) = await RegisterUserAsync();

        using var response = await LoginWithNewInboxAsync(
            registered.Email.ToUpperInvariant(),
            registered.Password);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_ResetsFailedAttemptsAndExpiredLockout_OnSuccess()
    {
        var (registered, _) = await RegisterUserAsync();

        await UpdateUserAsync(registered.Email, u =>
        {
            u.AccessFailedCount = 2;
            u.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-5); // bloqueio já expirado
        });

        using var response = await LoginWithNewInboxAsync(registered.Email, registered.Password);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Credenciais inválidas
    // ------------------------------------------------------------------

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsWrong()
    {
        var (registered, registeredTokens) = await RegisterUserAsync();

        using var response = await LoginWithNewInboxAsync(registered.Email, "Wrong#Password1");
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var http = await ReadAsync<Tokens>(response);

        http!.Success.Should().BeFalse();
        http.Message.Should().Be("Login failed");
        http.Data.Should().BeNull();
        http.Errors.Should().Contain("Login invalid");

        // Nenhum token novo foi emitido
        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(registeredTokens.RefreshToken);
    }

    [Fact]
    public async Task Login_ReturnsSameError_WhenEmailDoesNotExist()
    {
        // Mesma mensagem de "senha errada": não revela se a conta existe
        using var response = await LoginWithNewInboxAsync(
            $"ghost{Guid.NewGuid():N}@example.com",
            ValidPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var http = await ReadAsync<Tokens>(response);

        http!.Success.Should().BeFalse();
        http.Message.Should().Be("Login failed");
        http.Errors.Should().Contain("Login invalid");
    }

    [Fact]
    public async Task Login_ReturnsTooManyRequests_WhenAccountIsLockedOut()
    {
        var (registered, registeredTokens) = await RegisterUserAsync();

        await UpdateUserAsync(registered.Email,
            u => u.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1));

        // Mesmo com a senha correta, enquanto estiver bloqueado, é 429
        using var response = await LoginWithNewInboxAsync(registered.Email, registered.Password);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var http = await ReadAsync<Tokens>(response);

        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Login invalid");

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(registeredTokens.RefreshToken);
    }

    // ------------------------------------------------------------------
    // Validação de model
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("MissingPassword")]
    [InlineData("MissingEmail")]
    public async Task Login_ReturnsBadRequest_WhenBodyIsIncomplete(string scenario)
    {
        object body = scenario switch
        {
            "MissingPassword" => new { Email = "someone@example.com" },
            "MissingEmail" => new { Password = ValidPassword },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var inbox = await NewInboxAsync();

        using var response = await SendLoginAsync(body, inbox.Id);

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
    public async Task Login_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        using var response = await SendLoginAsync(
            new LoginUserDTO { Email = "someone@example.com", Password = ValidPassword },
            inboxId: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);
        http!.Errors.Should().ContainSingle(e => e.Contains(InboxHeader));
    }

    [Fact]
    public async Task Login_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var (registered, registeredTokens) = await RegisterUserAsync();

        using var response = await SendLoginAsync(
            new LoginUserDTO { Email = registered.Email, Password = registered.Password },
            Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<Tokens>(response);
        http!.Errors.Should().Contain("Inbox is not available for processing.");

        // O use case nem chegou a rodar
        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(registeredTokens.RefreshToken);
    }

    [Fact]
    public async Task Login_ReturnsConflict_WhenSameInboxIsReusedAfterSuccess()
    {
        var (registered, _) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();
        var body = new LoginUserDTO { Email = registered.Email, Password = registered.Password };

        using (var first = await SendLoginAsync(body, inbox.Id))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenAfterFirst = (await GetUserByEmailAsync(registered.Email))!.RefreshToken;

        using var second = await SendLoginAsync(body, inbox.Id);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Não houve nova rotação de refresh token
        (await GetUserByEmailAsync(registered.Email))!.RefreshToken.Should().Be(tokenAfterFirst);
    }

    [Fact]
    public async Task Login_AllowsOnlyOneRequest_WhenSameInboxIsUsedConcurrently()
    {
        var (registered, _) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();
        var body = new LoginUserDTO { Email = registered.Email, Password = registered.Password };

        var responses = await Task.WhenAll(
            SendLoginAsync(body, inbox.Id),
            SendLoginAsync(body, inbox.Id));

        try
        {
            responses.Select(r => r.StatusCode)
                .Should()
                .BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });

            (await GetInboxAsync(inbox.Id)).Status.Should().Be(InboxStatusEnum.Processed);
        }
        finally
        {
            foreach (var r in responses) r.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // Bugs conhecidos (remova o Skip depois de corrigir)
    // ------------------------------------------------------------------

    [Fact(Skip = "Bug: o rollback do UnitOfWork desfaz o AccessFailedCount quando o login falha.")]
    public async Task Login_PersistsFailedAttempt_WhenPasswordIsWrong()
    {
        var (registered, _) = await RegisterUserAsync();

        using var response = await LoginWithNewInboxAsync(registered.Email, "Wrong#Password1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.AccessFailedCount.Should().Be(1);
    }

    [Fact(Skip = "Bug: o rollback do UnitOfWork desfaz o AccessFailedCount, então o bloqueio nunca acontece.")]
    public async Task Login_LocksAccount_AfterThreeWrongPasswords()
    {
        var (registered, _) = await RegisterUserAsync();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var wrong = await LoginWithNewInboxAsync(registered.Email, "Wrong#Password1");
            wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // Quarta tentativa, agora com a senha certa: deve estar bloqueado
        using var response = await LoginWithNewInboxAsync(registered.Email, registered.Password);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.LockoutEnd.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact(Skip = "Bug: inbox fica preso em Processing quando o login falha.")]
    public async Task Login_ReleasesInbox_WhenCredentialsAreInvalid()
    {
        var (registered, _) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendLoginAsync(
            new LoginUserDTO { Email = registered.Email, Password = "Wrong#Password1" },
            inbox.Id);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updated = await GetInboxAsync(inbox.Id);
        updated.Status.Should().NotBe(InboxStatusEnum.Processing);
        updated.LockedBy.Should().BeNull();
    }

    [Fact(Skip = "Bug: RefreshTokenExpiryTime nunca é preenchido no login.")]
    public async Task Login_SetsRefreshTokenExpiry()
    {
        var (registered, _) = await RegisterUserAsync();

        using var response = await LoginWithNewInboxAsync(registered.Email, registered.Password);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshTokenExpiryTime.Should().NotBeNull();
        user.RefreshTokenExpiryTime.Should().BeAfter(DateTimeOffset.UtcNow);
    }
}