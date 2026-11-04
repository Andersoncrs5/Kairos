using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FluentAssertions;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Auth.Controller.Auth;

[Collection("Integration Tests")]
public class AuthRegisterTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    private const string RegisterUrl = "/api/v1/auth/register";

    public AuthRegisterTests(KairosApiFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ------------------------------------------------------------------
    // Sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task Register_ReturnsCreated_AndPersistsUserTokenAndInbox()
    {
        string inboxId = Guid.NewGuid().ToString();
        var dto = NewUserDto();

        using var response = await SendRegisterAsync(dto, inboxId);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<Tokens>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();
        http.Data!.Token.Should().NotBeNullOrWhiteSpace();
        http.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        http.Data.ExpiredToken.Should().BeAfter(DateTimeOffset.UtcNow);
        http.Data.Permissions.Should().BeEmpty();

        // Usuário gravado no banco
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserModel>>();

        var user = await userManager.FindByEmailAsync(dto.Email);

        user.Should().NotBeNull();
        user!.UserName.Should().Be(dto.UserName);
        user.FullName.Should().Be(dto.FullName);
        user.PasswordHash.Should().NotBeNullOrEmpty();
        user.PasswordHash.Should().NotBe(dto.Password);
        user.RefreshToken.Should().Be(http.Data.RefreshToken);

        // O JWT deve identificar esse usuário
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(http.Data.Token);

        jwt.Subject.Should().Be(user.Id);
        jwt.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Email && c.Value == dto.Email);
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------
    // Validação de DTO (Campos Individuais)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenUserNameIsNullOrEmpty()
    {
        var dto = NewUserDto(userName: string.Empty);
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenUserNameIsTooShort()
    {
        var dto = NewUserDto(userName: "ab");
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenUserNameIsTooLong()
    {
        var dto = NewUserDto(userName: new string('a', 257));
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsNullOrEmpty()
    {
        var dto = NewUserDto(email: string.Empty);
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        var dto = NewUserDto(email: "invalid-email-format");
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsTooLong()
    {
        var dto = NewUserDto(email: $"{new string('a', 250)}@test.com");
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordIsNullOrEmpty()
    {
        var dto = NewUserDto(password: string.Empty);
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordIsTooShort()
    {
        var dto = NewUserDto(password: "1234567");
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenFullNameIsTooLong()
    {
        var dto = NewUserDto(fullName: new string('a', 151));
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPhoneNumberIsTooLong()
    {
        var dto = NewUserDto(phoneNumber: new string('1', 31));
        await AssertValidationFailureAsync(dto);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPhoneNumberIsInvalid()
    {
        // Utilizando caracteres especiais que o PhoneAttribute rejeita com certeza
        var dto = NewUserDto(phoneNumber: "!@#$%^&*()");
        await AssertValidationFailureAsync(dto);
    }

    // ------------------------------------------------------------------
    // Regras de negócio
    // ------------------------------------------------------------------

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordViolatesIdentityRules()
    {
        var dto = NewUserDto(password: "abcdefgh");
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendRegisterAsync(dto, inboxId);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Tokens>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Message.Should().Be("Error creating user");
        http.Errors.Should().NotBeNullOrEmpty();

        (await UserExistsAsync(dto.Email)).Should().BeFalse();
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenUserNameAlreadyExists()
    {
        var first = NewUserDto();

        using (var ok = await SendRegisterAsync(first, Guid.NewGuid().ToString()))
            ok.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = NewUserDto(userName: first.UserName.ToUpperInvariant());

        using var response = await SendRegisterAsync(
            second,
            Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<Tokens>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Username is already taken.");
        (await UserExistsAsync(second.Email)).Should().BeFalse();
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailAlreadyExists()
    {
        var first = NewUserDto();

        using (var ok = await SendRegisterAsync(first, Guid.NewGuid().ToString()))
            ok.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = NewUserDto(email: first.Email.ToUpperInvariant());

        using var response = await SendRegisterAsync(
            second,
            Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<Tokens>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Email is already registered.");
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        var dto = NewUserDto();

        using var response = await SendRegisterAsync(dto, inboxId: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().ContainSingle(e => e.Contains(InboxHeader));
        (await UserExistsAsync(dto.Email)).Should().BeFalse();
    }

    [Fact]
    public async Task Register_ReturnsCreated_WhenInboxDoesNotExist()
    {
        var dto = NewUserDto();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendRegisterAsync(dto, inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<Tokens>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        (await UserExistsAsync(dto.Email)).Should().BeTrue();

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenSameInboxIsReusedAfterSuccess()
    {
        string inboxId = Guid.NewGuid().ToString();

        using (var first = await SendRegisterAsync(NewUserDto(), inboxId))
            first.StatusCode.Should().Be(HttpStatusCode.Created);

        var retry = NewUserDto();

        using var response = await SendRegisterAsync(retry, inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await UserExistsAsync(retry.Email)).Should().BeFalse();

        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------
    // Helpers Privados
    // ------------------------------------------------------------------

    private async Task AssertValidationFailureAsync(CreateUserDTO dto)
    {
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendRegisterAsync(dto, inboxId);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();

        // O usuário não deve ter sido criado no banco
        (await UserExistsAsync(dto.Email ?? "dummy@test.com")).Should().BeFalse();
    }

    private Task<HttpResponseMessage> SendRegisterAsync(
        CreateUserDTO dto,
        string? inboxId)
    {
        return SendAsync(
            RegisterUrl,
            body: dto,
            accessToken: null,
            inboxId: inboxId);
    }

    private static CreateUserDTO NewUserDto(
        string? userName = null,
        string? email = null,
        string? password = null,
        string? fullName = "Test User FullName",
        string? phoneNumber = "+5511988887777")
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        return new CreateUserDTO
        {
            UserName = userName ?? $"user_{uniqueId}",
            Email = email ?? $"user_{uniqueId}@kairos.test",
            Password = password ?? "StrongPass123!",
            FullName = fullName,
            PhoneNumber = phoneNumber
        };
    }
}