using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Res;
using Kairos.IntegrationTests.Config;

namespace Kairos.IntegrationTests.Module.Identity.Category.Controller;

[Collection("Integration Tests")]
public sealed class CategoryExistsByCodeControllerTest
    : BaseIntegrationTest
{
    private const string ExistsUrl =
        "/api/v1/categories/Exists/code";

    public CategoryExistsByCodeControllerTest(KairosApiFactory factory) : base(factory) { }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByCode_ReturnsTrue_WhenCategoryExists()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        string code = (
            await CreateCategoryHttp(await LoginUserMaster())
        ).Code;

        string inboxId = (await NewInboxAsync()).Id;

        // Act
        using (HttpResponseMessage response = await SendExistsAsync(
            code,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ResponseHttp<bool?> http =
                await ReadAsync<bool?>(response);

            http.Should().NotBeNull();
            http.Success.Should().BeTrue();
            http.Data.Should().BeTrue();
            http.TraceId.Should().Be(inboxId);
            http.Message.Should().Be("Category exists");
        }
    }

    [Fact]
    public async Task ExistsByCode_ReturnsFalse_WhenCategoryDoesNotExist()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        string code = $"nonexistent-category-{Guid.NewGuid():N}";

        string inboxId = (await NewInboxAsync()).Id;

        // Act
        using (HttpResponseMessage response = await SendExistsAsync(
            code,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ResponseHttp<bool?> http =
                await ReadAsync<bool?>(response);

            http.Should().NotBeNull();
            http.Success.Should().BeTrue();
            http.Data.Should().BeFalse();
            http.TraceId.Should().Be(inboxId);
            http.Message.Should().Be("Category does not exist");
        }
    }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByCode_ReturnsBadRequest_WhenCodeIsWhitespace()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        string inboxId = (await NewInboxAsync()).Id;

        // Act
        using (HttpResponseMessage response = await SendExistsAsync(
            " ",
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Com [ApiController] e [Required], a action não deve executar.
            (await GetInboxAsync(inboxId))
                .Status
                .Should()
                .Be(InboxStatusEnum.Pending);
        }
    }

    [Fact]
    public async Task ExistsByCode_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        // Act
        using (HttpResponseMessage response = await SendExistsAsync(
            "any-category",
            inboxId: null,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    // ---------------------------------------------------------------
    // Autenticação e autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByCode_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        const string code = "any-category";

        string inboxId = (await NewInboxAsync()).Id;

        // Act
        using (HttpResponseMessage response = await SendExistsAsync(
            code,
            inboxId,
            accessToken: null))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // A requisição não chega à action.
            (await GetInboxAsync(inboxId))
                .Status
                .Should()
                .Be(InboxStatusEnum.Pending);
        }
    }

    [Fact]
    public async Task ExistsByCode_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        string accessToken = (
            await RegisterUserAsync()
        ).Item2.Token;

        string code = $"category-{Guid.NewGuid():N}";

        string inboxId = (await NewInboxAsync()).Id;

        // Act
        using (HttpResponseMessage response = await SendExistsAsync(
            code,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // A requisição é bloqueada antes da execução do caso de uso.
            (await GetInboxAsync(inboxId))
                .Status
                .Should()
                .Be(InboxStatusEnum.Pending);
        }
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendExistsAsync(
        string code,
        string? inboxId,
        string? accessToken)
    {
        string url =
            $"{ExistsUrl}/{Uri.EscapeDataString(code)}";

        using (HttpRequestMessage request = new(
            HttpMethod.Get,
            url))
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        accessToken);
            }

            if (inboxId is not null)
            {
                request.Headers.Add(
                    "X-Inbox-Id",
                    inboxId);
            }

            return await Client.SendAsync(request);
        }
    }
}