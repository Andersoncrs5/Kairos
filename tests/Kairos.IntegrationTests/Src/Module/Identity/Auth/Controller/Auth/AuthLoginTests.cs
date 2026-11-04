using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Res;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
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
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendLoginAsync(
            new LoginUserDTO { Email = registered.Email, Password = registered.Password },
            inboxId);
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

        http.Data.RefreshToken.Should().NotBe(registeredTokens.RefreshToken);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(http.Data.RefreshToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(http.Data.Token);
        jwt.Subject.Should().Be(user.Id);

        await AssertInboxProcessedAsync(inboxId);
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
    // Validação de DTO (Model State Validation)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenEmailIsNullOrEmpty()
    {
        var dto = new LoginUserDTO { Email = string.Empty, Password = ValidPassword };
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        var dto = new LoginUserDTO { Email = "not-an-email", Password = ValidPassword };
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenEmailIsTooLong()
    {
        var dto = new LoginUserDTO { Email = $"{new string('a', 250)}@test.com", Password = ValidPassword };
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenPasswordIsNullOrEmpty()
    {
        var dto = new LoginUserDTO { Email = "someone@example.com", Password = string.Empty };
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenPasswordIsTooShort()
    {
        var dto = new LoginUserDTO { Email = "someone@example.com", Password = "1234567" };
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenPasswordIsTooLong()
    {
        var dto = new LoginUserDTO { Email = "someone@example.com", Password = new string('a', 101) };
        await AssertValidationFailureAsync(dto);
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

        http.Should().NotBeNull();
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

        http.Should().NotBeNull();
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

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Login invalid");

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(registeredTokens.RefreshToken);
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
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http!.Errors.Should().ContainSingle(e => e.Contains(InboxHeader));
    }

    [Fact]
    public async Task Login_ReturnsConflict_WhenSameInboxIsReusedAfterSuccess()
    {
        var (registered, _) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();
        var body = new LoginUserDTO { Email = registered.Email, Password = registered.Password };

        using (var first = await SendLoginAsync(body, inboxId))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenAfterFirst = (await GetUserByEmailAsync(registered.Email))!.RefreshToken;

        using var second = await SendLoginAsync(body, inboxId);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await GetUserByEmailAsync(registered.Email))!.RefreshToken.Should().Be(tokenAfterFirst);
        
        await AssertInboxProcessedAsync(inboxId);
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
        var inboxId  = Guid.NewGuid().ToString();

        using var response = await SendLoginAsync(
            new LoginUserDTO { Email = registered.Email, Password = "Wrong#Password1" },
            inboxId);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertInboxProcessedAsync(inboxId);
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

    // ------------------------------------------------------------------
    // Helpers Privados
    // ------------------------------------------------------------------

    private async Task AssertValidationFailureAsync(LoginUserDTO dto)
    {
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendLoginAsync(dto, inboxId);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
    }
}