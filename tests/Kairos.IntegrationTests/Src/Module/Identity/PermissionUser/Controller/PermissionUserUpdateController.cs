using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Xunit;

namespace Kairos.IntegrationTests.Module.Identity.PermissionUser.Controller;

[Collection("Integration Tests")]
public sealed class PermissionUserUpdateController : BaseIntegrationTest
{
    private const string UpdateUrl = "/api/v1/permission-users";

    public PermissionUserUpdateController(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenAssociationExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        // Act
        using var response = await SendPutAsync(
            association.Id,
            dto,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.Id.Should().Be(association.Id);
        http.Data.UserId.Should().Be(association.UserId);
        http.Data.PermissionId.Should().Be(association.PermissionId);
        http.Data.IsActive.Should().BeFalse();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenAssociationIsAlreadyInactive()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider,
            isActive: false);

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = true
        };

        // Act
        using var response = await SendPutAsync(
            association.Id,
            dto,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.Id.Should().Be(association.Id);
        http.Data.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task Update_ReturnsBadRequest_WhenIdIsInvalid(string id)
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        // Act
        using var response = await SendPutAsync(
            id,
            dto,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.Errors.Should().Contain("Id must be a valid UUID.");
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenAssociationDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var id = Guid.NewGuid().ToString();

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        // Act
        using var response = await SendPutAsync(
            id,
            dto,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<PermissionUserDTO>(response);

        http.Success.Should().BeFalse();
        http.Data.Should().BeNull();
        http.Errors.Should().Contain(
            "Permission-user association not found.");
    }

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        // Act
        using var response = await SendPutAsync(
            id,
            dto,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var (_, tokens) = await RegisterUserAsync();

        using var scope = CreateScope();

        var association = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        var dto = new UpdatePermissionUserDTO
        {
            IsActive = false
        };

        // Act
        using var response = await SendPutAsync(
            association.Id,
            dto,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> SendPutAsync(
        string id,
        UpdatePermissionUserDTO? dto,
        string? accessToken,
        bool includeInboxHeader = true)
    {
        var url = $"{UpdateUrl}/{Uri.EscapeDataString(id)}";

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            url)
        {
            Content = JsonContent.Create(dto)
        };

        if (includeInboxHeader)
        {
            var inbox = await NewInboxAsync();

            request.Headers.Add(InboxHeader, inbox.Id);
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await Client.SendAsync(request);
    }
}