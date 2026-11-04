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
public class AuthChangePasswordTests : BaseIntegrationTest
{
    private const string ChangePasswordUrl = "/api/v1/auth/changepassword";
    private const string NewPassword = "ChangedPassword123!";

    private readonly ITestOutputHelper _output;

    public AuthChangePasswordTests(KairosApiFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    private static ChangePasswordUserDto ValidChange(string currentPassword)
        => new(currentPassword, NewPassword, NewPassword);

    // ------------------------------------------------------------------
    // Sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_ReturnsOk_AndRevokesRefreshToken()
    {
        var (registered, tokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendChangePasswordAsync(
            ValidChange(registered.Password),
            tokens.Token,
            inboxId);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("Password changed successfully");
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task ChangePassword_PersistsNewPassword_AndRejectsOldPassword()
    {
        var (registered, tokens) = await RegisterUserAsync();

        using var changeResponse = await SendChangePasswordAsync(
            ValidChange(registered.Password),
            tokens.Token,
            Guid.NewGuid().ToString());

        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // A senha antiga deve deixar de funcionar
        using var oldPasswordLogin = await LoginWithNewInboxAsync(
            registered.Email,
            registered.Password);

        // A senha nova deve autenticar normalmente
        using var newPasswordLogin = await LoginWithNewInboxAsync(
            registered.Email,
            NewPassword);

        oldPasswordLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        newPasswordLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginHttp = await ReadAsync<Tokens>(newPasswordLogin);

        loginHttp.Should().NotBeNull();
        loginHttp!.Success.Should().BeTrue();
        loginHttp.Data.Should().NotBeNull();
        loginHttp.Data!.Token.Should().NotBeNullOrWhiteSpace();
        loginHttp.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    // ------------------------------------------------------------------
    // Validação de DTO (Model Validation & IValidatableObject)
    // ------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenCurrentPasswordIsNullOrEmpty()
    {
        var (registered, tokens) = await RegisterUserAsync();
        var dto = new ChangePasswordUserDto(string.Empty, NewPassword, NewPassword);

        await AssertValidationFailureAsync(dto, tokens, registered.Email);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenNewPasswordIsNullOrEmpty()
    {
        var (registered, tokens) = await RegisterUserAsync();
        var dto = new ChangePasswordUserDto(registered.Password, string.Empty, string.Empty);

        await AssertValidationFailureAsync(dto, tokens, registered.Email);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenNewPasswordIsTooShort()
    {
        var (registered, tokens) = await RegisterUserAsync();
        var dto = new ChangePasswordUserDto(registered.Password, "short", "short");

        await AssertValidationFailureAsync(dto, tokens, registered.Email);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenConfirmationDoesNotMatch()
    {
        var (registered, tokens) = await RegisterUserAsync();
        var dto = new ChangePasswordUserDto(
            registered.Password,
            NewPassword,
            "DifferentPassword123!");

        await AssertValidationFailureAsync(dto, tokens, registered.Email);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenNewPasswordEqualsCurrent()
    {
        var (registered, tokens) = await RegisterUserAsync();
        var dto = new ChangePasswordUserDto(
            registered.Password,
            registered.Password,
            registered.Password);

        await AssertValidationFailureAsync(dto, tokens, registered.Email);

        // Garante que o login com a senha atual ainda funciona normalmente
        using var login = await LoginWithNewInboxAsync(registered.Email, registered.Password);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------
    // Regras de negócio
    // ------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenCurrentPasswordIsIncorrect()
    {
        var (registered, tokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendChangePasswordAsync(
            ValidChange("WrongCurrentPassword123!"),
            tokens.Token,
            inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Message.Should().Be("Error changing password");
        http.Errors.Should().NotBeNullOrEmpty();

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenNewPasswordViolatesIdentityRules()
    {
        var (registered, tokens) = await RegisterUserAsync();

        // 8+ caracteres (passa na validação básica), mas sem regra de complexidade (ex: sem dígito ou maiúscula)
        var dto = new ChangePasswordUserDto(
            registered.Password,
            "alllowercasepass",
            "alllowercasepass");

        using var response = await SendChangePasswordAsync(
            dto, tokens.Token, Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(tokens.RefreshToken);

        using var login = await LoginWithNewInboxAsync(registered.Email, registered.Password);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var (registered, tokens) = await RegisterUserAsync();

        await DeleteUserAsync(registered.Email);

        using var response = await SendChangePasswordAsync(
            ValidChange(registered.Password),
            tokens.Token,
            Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<bool>(response);
        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("User not found.");
    }

    // ------------------------------------------------------------------
    // Autenticação
    // ------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        var (registered, tokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendChangePasswordAsync(
            ValidChange(registered.Password),
            accessToken: null,
            inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    [Fact]
    public async Task ChangePassword_ReturnsUnauthorized_WhenAccessTokenIsInvalid()
    {
        var (registered, tokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendChangePasswordAsync(
            ValidChange(registered.Password),
            "not-a-valid-jwt",
            inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var (registered, tokens) = await RegisterUserAsync();

        using var response = await SendChangePasswordAsync(
            ValidChange(registered.Password),
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
    public async Task ChangePassword_ReturnsConflict_WhenInboxIsReused()
    {
        var (registered, tokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        var dto = ValidChange(registered.Password);

        using (var first = await SendChangePasswordAsync(dto, tokens.Token, inboxId))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = await SendChangePasswordAsync(dto, tokens.Token, inboxId);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ------------------------------------------------------------------
    // Bugs conhecidos (remova o Skip depois de corrigir)
    // ------------------------------------------------------------------

    [Fact(Skip = "Bug: inbox fica preso em Processing quando a troca de senha falha.")]
    public async Task ChangePassword_ReleasesInbox_WhenCurrentPasswordIsIncorrect()
    {
        var (_, tokens) = await RegisterUserAsync();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendChangePasswordAsync(
            ValidChange("WrongCurrentPassword123!"),
            tokens.Token,
            inboxId);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------
    // Helpers Privados
    // ------------------------------------------------------------------

    private async Task AssertValidationFailureAsync(
        ChangePasswordUserDto dto,
        Tokens tokens,
        string userEmail)
    {
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendChangePasswordAsync(dto, tokens.Token, inboxId);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();

        var user = await GetUserByEmailAsync(userEmail);
        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    private Task<HttpResponseMessage> SendChangePasswordAsync(
        ChangePasswordUserDto dto,
        string? accessToken,
        string? inboxId)
    {
        return SendAsync(
            ChangePasswordUrl,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }
}