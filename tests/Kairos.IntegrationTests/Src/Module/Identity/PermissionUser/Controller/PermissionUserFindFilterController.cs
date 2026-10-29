using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.PermissionUser.Controller;

[Collection("Integration Tests")]
public sealed class PermissionUserFindFilterController : BaseIntegrationTest
{
    private const string FindUrl = "/api/v1/permission-users";
    private readonly ITestOutputHelper _output;

    public PermissionUserFindFilterController(KairosApiFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }
    
    [Fact]
    public async Task Find_ReturnsOk_WhenAssociationsExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        var query =
            $"?UserId={Uri.EscapeDataString(association.UserId)}";

        // Act
        using var response = await SendGetAsync(
            query,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PageResponse<PermissionUserDTO>>(
            response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        http.Data!.Data.Should().ContainSingle(item =>
            item.Id == association.Id &&
            item.UserId == association.UserId &&
            item.PermissionId == association.PermissionId &&
            item.IsActive == association.IsActive);

        http.Data.TotalCount.Should().Be(1);
        http.Data.PageNumber.Should().Be(1);
        http.Data.PageSize.Should().Be(20);
    }
    [Fact]
    public async Task Find_ReturnsOnlyMatchingAssociations_WhenFilteredByUserId()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        var query = $"?UserId={Uri.EscapeDataString(association.UserId)}";

        // Act
        using var response = await SendGetAsync(
            query,
            tokens.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PageResponse<PermissionUserDTO>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Data.Should().ContainSingle();

        http.Data.Data[0].Id.Should().Be(association.Id);
        http.Data.Data[0].UserId.Should().Be(association.UserId);
        http.Data.Data[0].PermissionId.Should().Be(association.PermissionId);
    }

    [Fact]
    public async Task Find_ReturnsOnlyInactiveAssociations_WhenFilteredByUserIdAndIsActive()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider,
            isActive: false);

        var query =
            $"?UserId={Uri.EscapeDataString(association.UserId)}&IsActive=false";

        // Act
        using var response = await SendGetAsync(
            query,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PageResponse<PermissionUserDTO>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Data.Should().ContainSingle();

        http.Data.Data[0].Id.Should().Be(association.Id);
        http.Data.Data[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Find_ReturnsRequestedPageSize_WhenPaginationIsSpecified()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        await CreatePermissionUserAsync(scope.ServiceProvider);
        await CreatePermissionUserAsync(scope.ServiceProvider);

        const string query = "?Page=1&PageSize=1";

        // Act
        using var response = await SendGetAsync(
            query,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PageResponse<PermissionUserDTO>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();

        http.Data!.Data.Should().ContainSingle();
        http.Data.PageNumber.Should().Be(1);
        http.Data.PageSize.Should().Be(1);
        http.Data.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Find_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Act
        using var response = await SendGetAsync(
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Find_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var (_, tokens) = await RegisterUserAsync();

        // Act
        using var response = await SendGetAsync(
            accessToken: tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> SendGetAsync(
        string queryString = "",
        string? accessToken = null)
    {
        var url = $"{FindUrl}{queryString}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            url);

        var inbox = await NewInboxAsync();

        request.Headers.Add(InboxHeader, inbox.Id);

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