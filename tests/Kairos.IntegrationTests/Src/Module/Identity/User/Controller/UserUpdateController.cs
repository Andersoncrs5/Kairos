using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.User.Controller;

[Collection("Integration Tests")]
public class UserUpdateController : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;

    private const string UpdateMeUrl = "/api/v1/users/Me";

    public UserUpdateController(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    private static UpdateUserDTO ValidUpdate() => new()
    {
        FullName = "Updated Full Name",
        UserName = $"updated-{Guid.NewGuid():N}",
        Email = $"updated-{Guid.NewGuid():N}@example.com",
        PhoneNumber = "+12025550174"
    };

    private async Task<HttpResponseMessage> SendUpdateMeAsync(
        UpdateUserDTO dto,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            UpdateMeUrl)
        {
            Content = JsonContent.Create(dto)
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.Add("X-Inbox-Id", inboxId);
        }

        return await Client.SendAsync(request);
    }

    // ------------------------------------------------------------------
    // Sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateMe_ReturnsOk_AndUpdatesAllFields()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();
        UpdateUserDTO dto = ValidUpdate();

        using var response = await SendUpdateMeAsync(
            dto,
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert - resposta HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("User updated successfully");
        http.TraceId.Should().Be(inboxId);

        // Assert - persistência
        var user = await GetUserByEmailAsync(dto.Email!);

        user.Should().NotBeNull();
        user!.UserName.Should().Be(dto.UserName);
        user.Email.Should().Be(dto.Email);
        user.FullName.Should().Be(dto.FullName);
        user.PhoneNumber.Should().Be(dto.PhoneNumber);

        // Alterações nos dados de contato invalidam as confirmações anteriores.
        user.EmailConfirmed.Should().BeFalse();
        user.PhoneNumberConfirmed.Should().BeFalse();

        // Assert - processamento da inbox
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task UpdateMe_ReturnsOk_AndPreservesFieldsNotProvided()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        var userBefore = await GetUserByEmailAsync(tokens.User.Email);

        userBefore.Should().NotBeNull();

        string originalUserName = userBefore!.UserName!;
        string originalEmail = userBefore.Email!;
        string? originalPhoneNumber = userBefore.PhoneNumber;
        bool originalEmailConfirmed = userBefore.EmailConfirmed;
        bool originalPhoneNumberConfirmed = userBefore.PhoneNumberConfirmed;

        UpdateUserDTO dto = new()
        {
            FullName = "Only Full Name Changed"
        };

        // Act
        using var response = await SendUpdateMeAsync(
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();

        var userAfter = await GetUserByEmailAsync(originalEmail);

        userAfter.Should().NotBeNull();
        userAfter!.FullName.Should().Be("Only Full Name Changed");
        userAfter.UserName.Should().Be(originalUserName);
        userAfter.Email.Should().Be(originalEmail);
        userAfter.PhoneNumber.Should().Be(originalPhoneNumber);
        userAfter.EmailConfirmed.Should().Be(originalEmailConfirmed);
        userAfter.PhoneNumberConfirmed.Should().Be(originalPhoneNumberConfirmed);

        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------
    // Validação
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateMe_ReturnsBadRequest_WhenNoFieldsAreProvided()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();
        UpdateUserDTO dto = new();

        // Act
        using var response = await SendUpdateMeAsync(
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "At least one field must be provided.");
    }

    [Fact]
    public async Task UpdateMe_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        UpdateUserDTO dto = new()
        {
            Email = "invalid-email"
        };

        // Act
        using var response = await SendUpdateMeAsync(
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http =
            await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMe_ReturnsBadRequest_WhenUserNameIsTooShort()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        UpdateUserDTO dto = new()
        {
            UserName = "ab"
        };

        // Act
        using var response = await SendUpdateMeAsync(
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    // Regras de negócio
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateMe_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();

        await DeleteUserAsync(tokens.User.Email);

        string inboxId = Guid.NewGuid().ToString();
        UpdateUserDTO dto = new()
        {
            FullName = "Updated Name"
        };

        // Act
        using var response = await SendUpdateMeAsync(
            dto,
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task UpdateMe_ReturnsConflict_WhenEmailIsAlreadyRegistered()
    {
        // Arrange
        Tokens firstUser = await CreateUserHttp();
        Tokens secondUser = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        UpdateUserDTO dto = new()
        {
            Email = secondUser.User.Email
        };

        // Act
        using var response = await SendUpdateMeAsync(
            dto,
            firstUser.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().NotBeNullOrEmpty();

        // A alteração não deve substituir o email original.
        var user = await GetUserByEmailAsync(firstUser.User.Email);

        user.Should().NotBeNull();
        user!.Email.Should().Be(firstUser.User.Email);
    }

    // ------------------------------------------------------------------
    // Autenticação
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateMe_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendUpdateMeAsync(
            new UpdateUserDTO { FullName = "Updated Name" },
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateMe_ReturnsUnauthorized_WhenAccessTokenIsInvalid()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendUpdateMeAsync(
            new UpdateUserDTO { FullName = "Updated Name" },
            accessToken: "not-a-valid-jwt",
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateMe_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();

        using var response = await SendUpdateMeAsync(
            new UpdateUserDTO { FullName = "Updated Name" },
            tokens.Token,
            inboxId: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Errors.Should().Contain(
            error => error.Contains(
                "X-Inbox-Id",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateMe_ReturnsConflict_WhenInboxIsReused()
    {
        // Arrange
        Tokens tokens = await CreateUserHttp();
        string inboxId = Guid.NewGuid().ToString();

        UpdateUserDTO dto = new()
        {
            FullName = "Updated Name"
        };

        // Act - primeira requisição
        using (var firstResponse = await SendUpdateMeAsync(
            dto,
            tokens.Token,
            inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Act - reutiliza a mesma inbox
        using var secondResponse = await SendUpdateMeAsync(
            dto,
            tokens.Token,
            inboxId);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertInboxProcessedAsync(inboxId);
    }
}