using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Res;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairos.IntegrationTests.Module.Identity.Category.Controller;

[Collection("Integration Tests")]
public sealed class CategoryFindByIdControllerTest : BaseIntegrationTest
{
    private const string FindUrl = "/api/v1/categories";

    public CategoryFindByIdControllerTest(
        KairosApiFactory factory)
        : base(factory)
    {
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsOk_WhenCategoryExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindByIdAsync(
            category.Id,
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.TraceId.Should().NotBeNullOrWhiteSpace();
        http.Message.Should().Be("Category found");

        http.Data!.Id.Should().Be(category.Id);
        http.Data.Name.Should().Be(category.Name);
        http.Data.Description.Should().Be(category.Description);

        // Verifica o processamento do Inbox.
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenIdIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindByIdAsync(
            "invalid-id",
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(error =>
            error.Contains(
                "Id must be a valid UUID.",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindById_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);

        // Act
        using var response = await SendFindByIdAsync(
            category.Id,
            inboxId: null,
            accessToken: tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var nonexistentId = Guid.NewGuid().ToString();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindByIdAsync(
            nonexistentId,
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(error =>
            error.Contains(
                "Category not found.",
                StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendFindByIdAsync(
            category.Id,
            nonexistentInboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(error =>
            error.Contains(
                "Inbox is not available for processing.",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindById_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var inbox = await NewInboxAsync();

        // Primeira consulta
        using (var firstResponse = await SendFindByIdAsync(
                   category.Id,
                   inbox.Id,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda consulta utilizando o mesmo Inbox
        using var response = await SendFindByIdAsync(
            category.Id,
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        // A consulta inicial deve ter processado o Inbox.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Autenticação e autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task FindById_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var (_, tokens) = await RegisterUserAsync();

        // O usuário master cria a categoria, pois o usuário comum
        // não possui a permissão categories.create.
        var masterTokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(masterTokens);
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendFindByIdAsync(
            category.Id,
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A autorização deve bloquear a requisição antes do processamento.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        // A categoria continua persistida.
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .AnyAsync(item => item.Id == category.Id);

        exists.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Helper HTTP
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendFindByIdAsync(
        string id,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{FindUrl}/{Uri.EscapeDataString(id)}";

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