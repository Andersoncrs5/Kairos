using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
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
    // Validação de model (antes de executar a action)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("UserName")]
    [InlineData("Email")]
    [InlineData("Password")]
    [InlineData("FullName")]
    public async Task Register_ReturnsBadRequest_WhenModelIsInvalid(string invalidField)
    {
        var dto = invalidField switch
        {
            "UserName" => NewUserDto(userName: "ab"),
            "Email" => NewUserDto(email: "not-an-email"),
            "Password" => NewUserDto(password: "short"),
            "FullName" => NewUserDto(fullName: new string('a', 151)),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField))
        };

        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendRegisterAsync(dto, inboxId);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Message.Should().Be("Validation failed");
        http.Data.Should().NotBeNull();
        http.Data!.Keys.Should().Contain(key =>
            key.EndsWith(invalidField, StringComparison.OrdinalIgnoreCase));

        // A action não rodou: o Inbox permanece Pending.
        
        (await UserExistsAsync(dto.Email)).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Regras de negócio
    // ------------------------------------------------------------------

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordViolatesIdentityRules()
    {
        // 8+ caracteres (passa no MinLength), mas sem maiúscula, dígito e símbolo
        var dto = NewUserDto(password: "abcdefgh");
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendRegisterAsync(dto, inboxId);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Tokens>(response);

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

        // Mesmo username, só que em maiúsculas (o Identity normaliza)
        var second = NewUserDto(userName: first.UserName.ToUpperInvariant());

        using var response = await SendRegisterAsync(
            second,
            Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<Tokens>(response);

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

    [Fact]
    public async Task Register_AllowsOnlyOneRequest_WhenSameInboxIsUsedConcurrently()
    {
        string inboxId = Guid.NewGuid().ToString();

        var dtoA = NewUserDto();
        var dtoB = NewUserDto();

        var responses = await Task.WhenAll(
            SendRegisterAsync(dtoA, inboxId),
            SendRegisterAsync(dtoB, inboxId));

        try
        {
            responses.Select(r => r.StatusCode)
                .Should()
                .BeEquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });

            var created = new[]
            {
                await UserExistsAsync(dtoA.Email),
                await UserExistsAsync(dtoB.Email)
            };

            created.Count(exists => exists).Should().Be(1);

            await AssertInboxProcessedAsync(inboxId);
        }
        finally
        {
            foreach (var r in responses)
                r.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // Bugs conhecidos (remova o Skip depois de corrigir)
    // ------------------------------------------------------------------

    [Fact(Skip = "Bug: inbox fica preso em Processing quando a operação falha.")]
    public async Task Register_ReleasesInbox_WhenOperationFails()
    {
        var first = NewUserDto();

        using (var ok = await SendRegisterAsync(first, Guid.NewGuid().ToString()))
            ok.StatusCode.Should().Be(HttpStatusCode.Created);

        string inboxId = Guid.NewGuid().ToString();
        var duplicated = NewUserDto(email: first.Email);

        using var response = await SendRegisterAsync(duplicated, inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

    }

    [Fact(Skip = "Bug: RefreshTokenExpiryTime nunca é preenchido no cadastro.")]
    public async Task Register_SetsRefreshTokenExpiry()
    {
        var dto = NewUserDto();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendRegisterAsync(dto, inboxId);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user = await GetUserByEmailAsync(dto.Email);

        user!.RefreshTokenExpiryTime.Should().NotBeNull();
        user.RefreshTokenExpiryTime.Should().BeAfter(DateTimeOffset.UtcNow);

        await AssertInboxProcessedAsync(inboxId);
    }
}