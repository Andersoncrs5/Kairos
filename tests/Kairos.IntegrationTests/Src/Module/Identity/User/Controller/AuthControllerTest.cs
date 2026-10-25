using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Res;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.IntegrationTests.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.User.Controller;

[Collection("Integration Tests")]
public class AuthControllerTest : BaseIntegrationTest
{
    private const string RegisterUrl = "/api/v1/auth/Register";
    private const string InboxHeader = "X-Inbox-Id";

    private readonly ITestOutputHelper _output;

    public AuthControllerTest(KairosApiFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task Login_Success()
    {
        using var scope = CreateScope();

        Tokens tokens = await CreateUserHttp();
        var inbox = await CreateInboxAsync(scope.ServiceProvider);

        LoginUserDTO dto = new LoginUserDTO()
        {
            Email = tokens.User!.Email!,
            Password = TestPassword
        };
        
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/Login")
        {
            Content = JsonContent.Create(dto)
        };
        request.Headers.Add("X-Inbox-Id", inbox.Id);

        using var response = await Client.SendAsync(request);
        
        _output.WriteLine(await response.Content.ReadAsStringAsync());
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await response.Content.ReadFromJsonAsync<ResponseHttp<Tokens>>();

        http.Should().NotBeNull();
        http!.Data.Should().NotBeNull();
        http.Data!.Token.Should().NotBeNullOrEmpty();
        http.Data.RefreshToken.Should().NotBeNullOrEmpty();
        http.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(i => i.Id == inbox.Id);

        updated.Status.Should().Be(InboxStatusEnum.Processed);
        updated.LockedBy.Should().BeNull();
        updated.ProcessedAt.Should().NotBeNull();
    }
    
    // ------------------------------------------------------------------
    // Login: sucesso
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
    // Login: credenciais inválidas
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
    // Login: validação de model
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
    // Login: Inbox
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
    // Login: bugs conhecidos (remova o Skip depois de corrigir)
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

    // ------------------------------------------------------------------
    // Sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task Register_ReturnsCreated_AndPersistsUserTokenAndInbox()
    {
        var inbox = await NewInboxAsync();
        var dto = NewUserDto();

        using var response = await SendRegisterAsync(dto, inbox.Id);
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
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<UserModel>>();

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

        // O inbox foi consumido na mesma operação
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
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

        var inbox = await NewInboxAsync();

        using var response = await SendRegisterAsync(dto, inbox.Id);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Message.Should().Be("Validation failed");
        http.Data.Should().NotBeNull();
        http.Data!.Keys.Should().Contain(key =>
            key.EndsWith(invalidField, StringComparison.OrdinalIgnoreCase));

        // A action não rodou, então o inbox não pode ter sido tocado
        (await GetInboxAsync(inbox.Id)).Status.Should().Be(InboxStatusEnum.Pending);
        (await UserExistsAsync(dto.Email)).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Regras do Identity (senha fraca)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordViolatesIdentityRules()
    {
        // 8+ caracteres (passa no MinLength), mas sem maiúscula, dígito e símbolo
        var dto = NewUserDto(password: "abcdefgh");
        var inbox = await NewInboxAsync();

        using var response = await SendRegisterAsync(dto, inbox.Id);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<Tokens>(response);

        http!.Success.Should().BeFalse();
        http.Message.Should().Be("Error creating user");
        http.Errors.Should().NotBeNullOrEmpty();

        (await UserExistsAsync(dto.Email)).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Duplicidade
    // ------------------------------------------------------------------

    [Fact]
    public async Task Register_ReturnsConflict_WhenUserNameAlreadyExists()
    {
        var first = NewUserDto();
        using (var ok = await SendRegisterAsync(first, (await NewInboxAsync()).Id))
            ok.StatusCode.Should().Be(HttpStatusCode.Created);

        // Mesmo username, só que em maiúsculas (o Identity normaliza)
        var second = NewUserDto(userName: first.UserName.ToUpperInvariant());

        using var response = await SendRegisterAsync(second, (await NewInboxAsync()).Id);

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
        using (var ok = await SendRegisterAsync(first, (await NewInboxAsync()).Id))
            ok.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = NewUserDto(email: first.Email.ToUpperInvariant());

        using var response = await SendRegisterAsync(second, (await NewInboxAsync()).Id);

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
    public async Task Register_ReturnsConflict_WhenInboxDoesNotExist()
    {
        var dto = NewUserDto();

        using var response = await SendRegisterAsync(dto, Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<Tokens>(response);

        http!.Errors.Should().Contain("Inbox is not available for processing.");
        (await UserExistsAsync(dto.Email)).Should().BeFalse();
    }

    [Theory]
    [InlineData(InboxStatusEnum.Processing)]
    [InlineData(InboxStatusEnum.Processed)]
    [InlineData(InboxStatusEnum.Failed)]
    [InlineData(InboxStatusEnum.DeadLetter)]
    public async Task Register_ReturnsConflict_WhenInboxIsNotPending(InboxStatusEnum status)
    {
        var inbox = await NewInboxAsync(i => i.Status = status);
        var dto = NewUserDto();

        using var response = await SendRegisterAsync(dto, inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await UserExistsAsync(dto.Email)).Should().BeFalse();

        (await GetInboxAsync(inbox.Id)).Status.Should().Be(status);
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenInboxNextAttemptIsInTheFuture()
    {
        var inbox = await NewInboxAsync(
            i => i.NextAttemptAt = DateTimeOffset.UtcNow.AddHours(1));
        var dto = NewUserDto();

        using var response = await SendRegisterAsync(dto, inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await UserExistsAsync(dto.Email)).Should().BeFalse();
        (await GetInboxAsync(inbox.Id)).Status.Should().Be(InboxStatusEnum.Pending);
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenSameInboxIsReusedAfterSuccess()
    {
        var inbox = await NewInboxAsync();

        using (var first = await SendRegisterAsync(NewUserDto(), inbox.Id))
            first.StatusCode.Should().Be(HttpStatusCode.Created);

        var retry = NewUserDto();

        using var response = await SendRegisterAsync(retry, inbox.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await UserExistsAsync(retry.Email)).Should().BeFalse();
    }

    [Fact]
    public async Task Register_AllowsOnlyOneRequest_WhenSameInboxIsUsedConcurrently()
    {
        var inbox = await NewInboxAsync();
        var dtoA = NewUserDto();
        var dtoB = NewUserDto();

        var responses = await Task.WhenAll(
            SendRegisterAsync(dtoA, inbox.Id),
            SendRegisterAsync(dtoB, inbox.Id));

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

    [Fact(Skip = "Bug: inbox fica preso em Processing quando a operação falha.")]
    public async Task Register_ReleasesInbox_WhenOperationFails()
    {
        var first = NewUserDto();
        using (var ok = await SendRegisterAsync(first, (await NewInboxAsync()).Id))
            ok.StatusCode.Should().Be(HttpStatusCode.Created);

        var inbox = await NewInboxAsync();
        var duplicated = NewUserDto(email: first.Email);

        using var response = await SendRegisterAsync(duplicated, inbox.Id);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updated = await GetInboxAsync(inbox.Id);

        updated.Status.Should().NotBe(InboxStatusEnum.Processing);
        updated.LockedBy.Should().BeNull();
    }

    [Fact(Skip = "Bug: RefreshTokenExpiryTime nunca é preenchido no cadastro.")]
    public async Task Register_SetsRefreshTokenExpiry()
    {
        var dto = NewUserDto();

        using var response = await SendRegisterAsync(dto, (await NewInboxAsync()).Id);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<UserModel>>();

        var user = await userManager.FindByEmailAsync(dto.Email);

        user!.RefreshTokenExpiryTime.Should().NotBeNull();
        user.RefreshTokenExpiryTime.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static CreateUserDTO NewUserDto(
        string? userName = null,
        string? email = null,
        string? password = null,
        string? fullName = "Pochita the Chainsaw Demon")
    {
        var id = Guid.NewGuid().ToString("N");

        return new CreateUserDTO
        {
            UserName = userName ?? $"pochita{id}",
            Email = email ?? $"pochita{id}@example.com",
            Password = password ?? ValidPassword,
            FullName = fullName
        };
    }

    private async Task<HttpResponseMessage> SendRegisterAsync(CreateUserDTO dto, string? inboxId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RegisterUrl)
        {
            Content = JsonContent.Create(dto)
        };

        if (inboxId is not null)
            request.Headers.Add(InboxHeader, inboxId);

        return await Client.SendAsync(request);
    }
    

    // Escopo novo + AsNoTracking: evita ler entidade em cache do EF
    private async Task<InboxModel> GetInboxAsync(string id)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(i => i.Id == id);
    }

    private async Task<bool> UserExistsAsync(string email)
    {
        using var scope = CreateScope();
        return await GetUserRepository(scope).ExistsByEmail(email);
    }
    
    private const string LoginUrl = "/api/v1/auth/Login";

    private async Task<(CreateUserDTO Dto, Tokens Tokens)> RegisterUserAsync()
    {
        var dto = NewUserDto();

        using var response = await SendRegisterAsync(dto, (await NewInboxAsync()).Id);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var http = await ReadAsync<Tokens>(response);

        return (dto, http!.Data!);
    }

    private async Task<HttpResponseMessage> SendLoginAsync(object body, string? inboxId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LoginUrl)
        {
            Content = JsonContent.Create(body)
        };

        if (inboxId is not null)
            request.Headers.Add(InboxHeader, inboxId);

        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> LoginWithNewInboxAsync(string email, string password)
    {
        var inbox = await NewInboxAsync();

        return await SendLoginAsync(
            new LoginUserDTO { Email = email, Password = password },
            inbox.Id);
    }

    private async Task<UserModel?> GetUserByEmailAsync(string email)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserModel>>();

        return await userManager.FindByEmailAsync(email);
    }

    private async Task UpdateUserAsync(string email, Action<UserModel> configure)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserModel>>();

        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();

        configure(user!);

        var result = await userManager.UpdateAsync(user!);
        result.Succeeded.Should().BeTrue();
    }
}