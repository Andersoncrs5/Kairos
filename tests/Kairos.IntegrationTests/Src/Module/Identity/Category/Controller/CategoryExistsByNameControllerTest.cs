using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.IntegrationTests.Config;
using Xunit;

namespace Kairos.IntegrationTests.Module.Identity.Category.Controller;

[Collection("Integration Tests")]
public sealed class CategoryExistsByNameControllerTest
    : BaseIntegrationTest
{
    private const string ExistsUrl =
        "/api/v1/categories/Exists/name";

    public CategoryExistsByNameControllerTest(
        KairosApiFactory factory)
        : base(factory)
    {
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByName_ReturnsTrue_WhenCategoryExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsAsync(
            category.Name,
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeTrue();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Category exists");

    }

    [Fact]
    public async Task ExistsByName_ReturnsFalse_WhenCategoryDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var name = $"Nonexistent Category {Guid.NewGuid():N}";
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsAsync(
            name,
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<bool?>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().BeFalse();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Category does not exist");
        
    }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByName_ReturnsBadRequest_WhenNameIsWhitespace()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsAsync(
            " ",
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Com [ApiController] e [Required], a action não deve executar
        // quando o nome falha na validação automática.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ExistsByName_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        // Act
        using var response = await SendExistsAsync(
            "Any Category",
            inboxId: null,
            accessToken: tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // Autenticação e autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByName_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendExistsAsync(
            "Any Category",
            inbox.Id,
            accessToken: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A requisição não chega à action.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ExistsByName_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var (_, tokens) = await RegisterUserAsync();
        var inbox = await NewInboxAsync();
        var name = $"Category {Guid.NewGuid():N}";

        // Act
        using var response = await SendExistsAsync(
            name,
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A requisição é bloqueada antes da execução do caso de uso.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendExistsAsync(
        string name,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{ExistsUrl}/{Uri.EscapeDataString(name)}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
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
}