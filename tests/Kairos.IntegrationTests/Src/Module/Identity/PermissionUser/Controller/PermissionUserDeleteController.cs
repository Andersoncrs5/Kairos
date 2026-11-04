using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.PermissionUser.Controller;

[Collection("Integration Tests")]
public sealed class PermissionUserDeleteController : BaseIntegrationTest
{
    private const string DeleteUrl = "/api/v1/permission-users";

    private readonly ITestOutputHelper _output;

    public PermissionUserDeleteController(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ------------------------------------------------------------------
    // Success
    // ------------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsOk_DeletesAssociationAndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            $"{DeleteUrl}/{association.UserId}/{association.PermissionId}",
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert: HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Revoking permission from user");

        // Assert: association removed from database
        using var verificationScope = CreateScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<PermissionUserModel>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == association.UserId &&
                x.PermissionId == association.PermissionId);

        exists.Should().BeFalse();

        // Assert: Inbox processed
        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------
    // Authorization
    // ------------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange: ordinary users do not have all permissions.
        var (_, tokens) = await RegisterUserAsync();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            $"{DeleteUrl}/{association.UserId}/{association.PermissionId}",
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------
    // Model validation
    // ------------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        // Act
        using var response = await SendDeleteAsync(
            $"{DeleteUrl}/{association.UserId}/{association.PermissionId}",
            tokens.Token,
            inboxId: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().ContainSingle(
            error => error.Contains(InboxHeader));

        await AssertAssociationExistsAsync(
            association.UserId,
            association.PermissionId);
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenPermissionIdIsNotUuid()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            $"{DeleteUrl}/{association.UserId}/invalid-uuid",
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            error => error.Contains(
                "PermissionId must be a valid UUID."));

        await AssertAssociationExistsAsync(
            association.UserId,
            association.PermissionId);
    }

    // ------------------------------------------------------------------
    // Not found
    // ------------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var permission = await CreatePermissionAsync(
            scope.ServiceProvider);

        var missingUserId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            $"{DeleteUrl}/{missingUserId}/{permission.Id}",
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenPermissionDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var missingPermissionId = Guid.NewGuid().ToString();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            $"{DeleteUrl}/{user.Id}/{missingPermissionId}",
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Permission not found.");
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenAssociationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var permission = await CreatePermissionAsync(scope.ServiceProvider);
        string inboxId = Guid.NewGuid().ToString();

        // The user and permission exist, but their association was not created.

        // Act
        using var response = await SendDeleteAsync(
            $"{DeleteUrl}/{user.Id}/{permission.Id}",
            tokens.Token,
            inboxId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "Permission-user association not found.");
    }

    // ------------------------------------------------------------------
    // Inbox
    // ------------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsOk_WhenInboxDoesNotExist()
    {
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        string inboxId = Guid.NewGuid().ToString();

        using var response = await SendDeleteAsync(
            $"{DeleteUrl}/{association.UserId}/{association.PermissionId}",
            tokens.Token,
            inboxId);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Message.Should().Be("Revoking permission from user");

        using var verificationScope = CreateScope();

        var db = verificationScope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<PermissionUserModel>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == association.UserId &&
                x.PermissionId == association.PermissionId);

        exists.Should().BeFalse();

        await AssertInboxProcessedAsync(inboxId);
    }

    // ------------------------------------------------------------------

    private async Task<HttpResponseMessage> SendDeleteAsync(
        string url,
        string? accessToken,
        string? inboxId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            url);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);
        }

        if (inboxId is not null)
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }

    private async Task AssertAssociationExistsAsync(
        string userId,
        string permissionId)
    {
        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var exists = await db.Set<PermissionUserModel>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == userId &&
                x.PermissionId == permissionId);

        exists.Should().BeTrue();
    }
}