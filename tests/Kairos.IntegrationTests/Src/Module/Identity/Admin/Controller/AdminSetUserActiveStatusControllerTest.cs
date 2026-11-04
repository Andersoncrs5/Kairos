
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Res;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Admin.Controller;

[Collection("Integration Tests")]
public sealed class AdminSetUserActiveStatusControllerTest : BaseIntegrationTest
{
    private const string Url = "/api/v1/admin/Users";
    private const string StatusPermission = "identity.users.status.update";
    private readonly ITestOutputHelper _output;

    public AdminSetUserActiveStatusControllerTest(
        KairosApiFactory factory, ITestOutputHelper output
        )
        : base(factory)
    {
        _output = output;
        
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<Tokens> LoginAdminAsync()
    {
        // Garante que o usuário master tenha a permissão necessária.
        // A atualização é idempotente: não duplica a permissão.
        await UpdateUserAsync(
            UserMasterOptions.Value.Email,
            user =>
            {
                string[] permissions = user.Permissions ?? [];

                if (!permissions.Contains(
                        StatusPermission,
                        StringComparer.Ordinal))
                {
                    user.Permissions = permissions
                        .Append(StatusPermission)
                        .ToArray();
                }
            });

        // Login depois da atualização para obter as claims atualizadas.
        return await LoginUserMaster();
    }

    private async Task<(Tokens Tokens, UserModel User)> CreateTargetUserAsync()
    {
        Tokens tokens = await CreateUserHttp();

        UserModel? user = await GetUserByEmailAsync(tokens.User.Email);

        user.Should().NotBeNull();

        return (tokens, user!);
    }

    private async Task<HttpResponseMessage> SendSetUserActiveStatusAsync(
        string userId,
        bool isActive,
        string? accessToken,
        string? inboxId)
    {
        string requestUrl =
            $"{Url}/{Uri.EscapeDataString(userId)}/Status";

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            requestUrl)
        {
            Content = JsonContent.Create(new
            {
                IsActive = isActive
            })
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (!string.IsNullOrWhiteSpace(inboxId))
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }
    
    // ------------------------------------------------------------------
    // Success
    // ------------------------------------------------------------------

    [Fact]
    public async Task SetUserActiveStatus_ReturnsOk_AndDeactivatesUser()
    {
        // Arrange
        string inboxId = Guid.NewGuid().ToString();
        Tokens admin = await LoginAdminAsync();
        var (_, targetUser) = await CreateTargetUserAsync();

        targetUser.IsActive.Should().BeTrue();
        targetUser.RefreshToken.Should().NotBeNullOrWhiteSpace();

        // Act
        using var response = await SendSetUserActiveStatusAsync(
            targetUser.Id,
            isActive: false,
            admin.Token,
            inboxId);

        // Assert - HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("User account deactivated successfully");
        http.TraceId.Should().Be(inboxId);

        // Assert - persistence
        var updatedUser = await GetUserByEmailAsync(targetUser.Email!);

        updatedUser.Should().NotBeNull();
        updatedUser!.IsActive.Should().BeFalse();

        // Deactivation must revoke the refresh token.
        updatedUser.RefreshToken.Should().BeNull();
        updatedUser.RefreshTokenExpiryTime.Should().BeNull();

        // Assert - Inbox
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task SetUserActiveStatus_ReturnsOk_AndActivatesUser()
    {
        // Arrange
        Tokens admin = await LoginAdminAsync();
        var (_, targetUser) = await CreateTargetUserAsync();

        string refreshToken = $"refresh-{Guid.NewGuid():N}";

        DateTimeOffset refreshTokenExpiry = DateTimeOffset
            .FromUnixTimeMilliseconds(
                DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds());

        // Deixa o usuário inativo antes da chamada.
        await UpdateUserAsync(
            targetUser.Email!,
            user =>
            {
                user.IsActive = false;
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = refreshTokenExpiry;
            });

        string inboxId = Guid.NewGuid().ToString();
        // Act
        using var response = await SendSetUserActiveStatusAsync(
            targetUser.Id,
            isActive: true,
            admin.Token,
            inboxId);

        // Assert - HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.Message.Should().Be("User account activated successfully");
        http.TraceId.Should().Be(inboxId);

        // Assert - persistence
        var updatedUser = await GetUserByEmailAsync(targetUser.Email!);

        updatedUser.Should().NotBeNull();
        updatedUser!.IsActive.Should().BeTrue();

        // Reactivation must not revoke the existing refresh token.
        updatedUser.RefreshToken.Should().Be(refreshToken);
        updatedUser.RefreshTokenExpiryTime.Should().Be(refreshTokenExpiry);

        // Assert - Inbox
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task SetUserActiveStatus_ReturnsOk_WithoutUpdating_WhenUserAlreadyHasRequestedStatus()
    {
        // Arrange
        Tokens admin = await LoginAdminAsync();
        var (_, targetUser) = await CreateTargetUserAsync();

        targetUser.IsActive.Should().BeTrue();

        long originalVersion = targetUser.Version;
        string? originalRefreshToken = targetUser.RefreshToken;

        string inboxId = Guid.NewGuid().ToString();
                
        // Act
        using var response = await SendSetUserActiveStatusAsync(
            targetUser.Id,
            isActive: true,
            admin.Token,
            inboxId);
        
        _output.WriteLine(await response.Content.ReadAsStringAsync());
        
        // Assert - HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.TraceId.Should().Be(inboxId);

        // Assert - no unnecessary database update
        var updatedUser = await GetUserByEmailAsync(targetUser.Email!);

        updatedUser.Should().NotBeNull();
        updatedUser!.IsActive.Should().BeTrue();
        updatedUser.Version.Should().Be(originalVersion);
        updatedUser.RefreshToken.Should().Be(originalRefreshToken);

        // Assert - Inbox
        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------
    // Not found
    // ------------------------------------------------------------------

    [Fact]
    public async Task SetUserActiveStatus_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        Tokens admin = await LoginAdminAsync();
        string userId = Guid.NewGuid().ToString();

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendSetUserActiveStatusAsync(
            userId,
            isActive: false,
            admin.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<bool>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("User not found.");
        http.TraceId.Should().Be(inboxId);
    }

    // ------------------------------------------------------------------
    // Authorization
    // ------------------------------------------------------------------

    [Fact]
    public async Task SetUserActiveStatus_ReturnsUnauthorized_WhenAccessTokenIsMissing()
    {
        // Arrange
        var (_, targetUser) = await CreateTargetUserAsync();
                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendSetUserActiveStatusAsync(
            targetUser.Id,
            isActive: false,
            accessToken: null,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetUserActiveStatus_ReturnsUnauthorized_WhenAccessTokenIsInvalid()
    {
        // Arrange
        var (_, targetUser) = await CreateTargetUserAsync();
                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendSetUserActiveStatusAsync(
            targetUser.Id,
            isActive: false,
            accessToken: "not-a-valid-jwt",
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetUserActiveStatus_ReturnsForbidden_WhenUserDoesNotHavePermission()
    {
        // Arrange
        Tokens unauthorizedUser = await CreateUserHttp();
        var (_, targetUser) = await CreateTargetUserAsync();

                string inboxId = Guid.NewGuid().ToString();


        // Act
        using var response = await SendSetUserActiveStatusAsync(
            targetUser.Id,
            isActive: false,
            unauthorizedUser.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The target user must remain unchanged.
        var persistedUser = await GetUserByEmailAsync(targetUser.Email!);

        persistedUser.Should().NotBeNull();
        persistedUser!.IsActive.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task SetUserActiveStatus_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        Tokens admin = await LoginAdminAsync();
        var (_, targetUser) = await CreateTargetUserAsync();

        // Act
        using var response = await SendSetUserActiveStatusAsync(
            targetUser.Id,
            isActive: false,
            admin.Token,
            inboxId: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            error => error.Contains(
                "X-Inbox-Id",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetUserActiveStatus_ReturnsConflict_WhenInboxIsReused()
    {
        // Arrange
        Tokens admin = await LoginAdminAsync();
        var (_, targetUser) = await CreateTargetUserAsync();

                string inboxId = Guid.NewGuid().ToString();


        // Act - first request
        using (var firstResponse = await SendSetUserActiveStatusAsync(
            targetUser.Id,
            isActive: false,
            admin.Token,
            inboxId))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstHttp = await ReadAsync<bool>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
        }

        // Act - reuse the same Inbox
        using var secondResponse = await SendSetUserActiveStatusAsync(
            targetUser.Id,
            isActive: false,
            admin.Token,
            inboxId);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The first request must have completed successfully.
        await AssertInboxProcessedAsync(inboxId);

        // The user must remain deactivated.
        var updatedUser = await GetUserByEmailAsync(targetUser.Email!);

        updatedUser.Should().NotBeNull();
        updatedUser!.IsActive.Should().BeFalse();
        updatedUser.RefreshToken.Should().BeNull();
        updatedUser.RefreshTokenExpiryTime.Should().BeNull();
    }
}
