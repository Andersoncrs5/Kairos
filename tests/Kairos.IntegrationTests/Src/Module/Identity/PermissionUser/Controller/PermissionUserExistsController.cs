using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Res;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.PermissionUser.Controller;

[Collection("Integration Tests")]
public sealed class PermissionUserExistsController : BaseIntegrationTest
{
    private const string ExistsUrl = "/api/v1/permission-users/exists";

    private readonly ITestOutputHelper _output;

    public PermissionUserExistsController(
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
    public async Task Exists_ReturnsTrue_WhenActiveAssociationExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        var inboxId = $"test-{Guid.NewGuid():N}";

        // Act
        using var response = await SendExistsAsync(
            association.UserId,
            association.PermissionId,
            tokens.Token,
            inboxId);

        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be(
            "Permission is actively assigned to user");
    }

    [Fact]
    public async Task Exists_ReturnsFalse_WhenAssociationIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider,
            isActive: false);

        // Act
        using var response = await SendExistsAsync(
            association.UserId,
            association.PermissionId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
        http.Message.Should().Be(
            "Permission is not actively assigned to user");
    }

    [Fact]
    public async Task Exists_ReturnsFalse_WhenAssociationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var permission = await CreatePermissionAsync(scope.ServiceProvider);

        // Act
        using var response = await SendExistsAsync(
            user.Id,
            permission.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
    }

    [Fact]
    public async Task Exists_ReturnsFalse_WhenUserDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var permission = await CreatePermissionAsync(scope.ServiceProvider);
        var unknownUserId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            unknownUserId,
            permission.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
    }

    [Fact]
    public async Task Exists_ReturnsFalse_WhenPermissionDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);
        var unknownPermissionId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            user.Id,
            unknownPermissionId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task Exists_ReturnsBadRequest_WhenPermissionIdIsInvalid(
        string permissionId)
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var user = await CreateUserAsync(scope.ServiceProvider);

        // Act
        using var response = await SendExistsAsync(
            user.Id,
            permissionId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(
            "PermissionId must be a valid UUID.");
        http.Data.Should().BeNull();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task Exists_ReturnsBadRequest_WhenUserIdIsWhitespace(
        string userId)
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var permissionId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            userId,
            permissionId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ValidationErrorResponse>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Exists_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var userId = Guid.NewGuid().ToString();
        var permissionId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            userId,
            permissionId,
            tokens.Token,
            inboxId: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().ContainSingle(
            error => error.Contains(InboxHeader));
    }

    // ------------------------------------------------------------------
    // Authentication and authorization
    // ------------------------------------------------------------------

    [Fact]
    public async Task Exists_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var permissionId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendExistsAsync(
            userId,
            permissionId,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Exists_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        // Arrange
        var (_, tokens) = await RegisterUserAsync();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        // Act
        using var response = await SendExistsAsync(
            association.UserId,
            association.PermissionId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------
    // UUID normalization
    // ------------------------------------------------------------------

    [Fact]
    public async Task Exists_ReturnsTrue_WhenPermissionIdIsUppercase()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        var uppercasePermissionId = Guid
            .Parse(association.PermissionId)
            .ToString()
            .ToUpperInvariant();

        // Act
        using var response = await SendExistsAsync(
            association.UserId,
            uppercasePermissionId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // HTTP helper
    // ------------------------------------------------------------------

    private async Task<HttpResponseMessage> SendExistsAsync(
        string userId,
        string permissionId,
        string? accessToken,
        string? inboxId = "test-inbox")
    {
        var url =
            $"{ExistsUrl}/{Uri.EscapeDataString(userId)}/{Uri.EscapeDataString(permissionId)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (inboxId is not null)
        {
            request.Headers.Add(InboxHeader, inboxId);
        }

        return await Client.SendAsync(request);
    }
}
