using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Res;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.PermissionUser.Controller;

[Collection("Integration Tests")]
public sealed class PermissionUserFindByIdController : BaseIntegrationTest
{
    private const string FindUrl = "/api/v1/permission-users";

    private readonly ITestOutputHelper _output;

    public PermissionUserFindByIdController(
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
    public async Task FindById_ReturnsOk_WhenAssociationExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        // Act
        using var response = await SendGetAsync(
            association.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(association.Id);
        http.Data.UserId.Should().Be(association.UserId);
        http.Data.PermissionId.Should().Be(association.PermissionId);
        http.Data.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task FindById_ReturnsOk_WhenAssociationIsInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider,
            isActive: false);

        // Act
        using var response = await SendGetAsync(
            association.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Data.Should().NotBeNull();

        http.Data!.Id.Should().Be(association.Id);
        http.Data.UserId.Should().Be(association.UserId);
        http.Data.PermissionId.Should().Be(association.PermissionId);
        http.Data.IsActive.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task FindById_ReturnsBadRequest_WhenIdIsNotUuid(
        string id)
    {
        // Arrange
        var tokens = await LoginUserMaster();

        // Act
        using var response = await SendGetAsync(
            id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Id must be a valid UUID.");
        http.Data.Should().BeNull();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task FindById_ReturnsBadRequest_WhenIdIsEmptyOrWhitespace(
        string id)
    {
        // Arrange
        var tokens = await LoginUserMaster();

        // Act
        using var response = await SendGetAsync(
            id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<ValidationErrorResponse>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Not found
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenAssociationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var unknownId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendGetAsync(
            unknownId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain("Permission user not found.");
        http.Data.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Authentication
    // ------------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        // Act
        using var response = await SendGetAsync(
            id,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FindById_ReturnsForbidden_WhenUserLacksReadPermission()
    {
        // Arrange
        var (_, tokens) = await RegisterUserAsync();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        // Act
        using var response = await SendGetAsync(
            association.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------
    // HTTP helper
    // Every request includes X-Inbox-Id because the controller requires it.
    // This endpoint does not process the Inbox; the header is only required
    // by the current action signature.
    // ------------------------------------------------------------------

    private async Task<HttpResponseMessage> SendGetAsync(
        string id,
        string? accessToken)
    {
        var url = $"{FindUrl}/{Uri.EscapeDataString(id)}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            url);

        request.Headers.Add(
            InboxHeader,
            $"test-{Guid.NewGuid():N}");

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);
        }

        return await Client.SendAsync(request);
    }
}