using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Auth.Controller.Auth;

[Collection("Integration Tests")]
public class AuthChangePasswordTests : BaseIntegrationTest
{
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
        var inbox = await NewInboxAsync();

        using var response = await SendChangePasswordAsync(
            ValidChange(registered.Password),
            tokens.Token,
            inbox.Id);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("Password changed successfully");
        http.TraceId.Should().Be(inbox.Id);

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();

        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task ChangePassword_PersistsNewPassword_AndRejectsOldPassword()
    {
        var (registered, tokens) = await RegisterUserAsync();

        using var changeResponse = await SendChangePasswordAsync(
            ValidChange(registered.Password),
            tokens.Token,
            (await NewInboxAsync()).Id);

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
    // Validação de model
    // ------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenConfirmationDoesNotMatch()
    {
        var (registered, tokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        ChangePasswordUserDto dto = new(
            registered.Password,
            NewPassword,
            "DifferentPassword123!");

        using var response = await SendChangePasswordAsync(dto, tokens.Token, inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Message.Should().Be("Validation failed");
        http.Data.Should().NotBeNull();
        http.Data!.Keys.Should().Contain(
            key => key.Contains(
                nameof(ChangePasswordUserDto.ConfirmPassword),
                StringComparison.OrdinalIgnoreCase));

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(tokens.RefreshToken);

        (await GetInboxAsync(inbox.Id)).Status.Should().Be(InboxStatusEnum.Pending);
    }

    // ------------------------------------------------------------------
    // Regras de negócio
    // ------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenCurrentPasswordIsIncorrect()
    {
        var (registered, tokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendChangePasswordAsync(
            ValidChange("WrongCurrentPassword123!"),
            tokens.Token,
            inbox.Id);

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
    public async Task ChangePassword_ReturnsBadRequest_WhenNewPasswordEqualsCurrent()
    {
        var (registered, tokens) = await RegisterUserAsync();

        ChangePasswordUserDto dto = new(
            registered.Password,
            registered.Password,
            registered.Password);

        using var response = await SendChangePasswordAsync(
            dto, tokens.Token, (await NewInboxAsync()).Id);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A senha antiga continua valendo e a sessão não foi revogada
        var user = await GetUserByEmailAsync(registered.Email);
        user!.RefreshToken.Should().Be(tokens.RefreshToken);

        using var login = await LoginWithNewInboxAsync(registered.Email, registered.Password);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenNewPasswordViolatesIdentityRules()
    {
        var (registered, tokens) = await RegisterUserAsync();

        // Sem maiúscula, dígito e símbolo
        ChangePasswordUserDto dto = new(
            registered.Password,
            "alllowercase",
            "alllowercase");

        using var response = await SendChangePasswordAsync(
            dto, tokens.Token, (await NewInboxAsync()).Id);

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
            (await NewInboxAsync()).Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<bool>(response);
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
        var inbox = await NewInboxAsync();

        using var response = await SendChangePasswordAsync(
            ValidChange(registered.Password),
            accessToken: null,
            inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(tokens.RefreshToken);

        (await GetInboxAsync(inbox.Id)).Status.Should().Be(InboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ChangePassword_ReturnsUnauthorized_WhenAccessTokenIsInvalid()
    {
        var (registered, tokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendChangePasswordAsync(
            ValidChange(registered.Password),
            "not-a-valid-jwt",
            inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = await GetUserByEmailAsync(registered.Email);

        user.Should().NotBeNull();
        user!.RefreshToken.Should().Be(tokens.RefreshToken);

        (await GetInboxAsync(inbox.Id)).Status.Should().Be(InboxStatusEnum.Pending);
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
        var inbox = await NewInboxAsync();
        var dto = ValidChange(registered.Password);

        using (var first = await SendChangePasswordAsync(dto, tokens.Token, inbox.Id))
            first.StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = await SendChangePasswordAsync(dto, tokens.Token, inbox.Id);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ------------------------------------------------------------------
    // Bugs conhecidos (remova o Skip depois de corrigir)
    // ------------------------------------------------------------------

    [Fact(Skip = "Bug: inbox fica preso em Processing quando a troca de senha falha.")]
    public async Task ChangePassword_ReleasesInbox_WhenCurrentPasswordIsIncorrect()
    {
        var (_, tokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();

        using var response = await SendChangePasswordAsync(
            ValidChange("WrongCurrentPassword123!"),
            tokens.Token,
            inbox.Id);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var updated = await GetInboxAsync(inbox.Id);
        updated.Status.Should().NotBe(InboxStatusEnum.Processing);
        updated.LockedBy.Should().BeNull();
    }
}