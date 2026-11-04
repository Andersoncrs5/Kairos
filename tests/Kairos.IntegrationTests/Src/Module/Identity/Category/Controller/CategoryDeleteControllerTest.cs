using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Res;
using Kairos.Api.Config.DB;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Category.Controller;

[Collection("Integration Tests")]
public sealed class CategoryDeleteControllerTest : BaseIntegrationTest
{
    private const string DeleteUrl = "/api/v1/categories";

    private readonly ITestOutputHelper _output;

    public CategoryDeleteControllerTest(
        KairosApiFactory factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsOk_AndRemovesCategoryAndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            category.Id,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        // Confirma a remoção no banco.
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .AnyAsync(item => item.Id == category.Id);

        exists.Should().BeFalse();

        // Confirma o processamento do Inbox.
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenIdIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            "invalid-id",
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(error =>
            error.Contains(
                "Id must be a valid UUID.",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenCategoryDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();
        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            nonexistentId,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(error =>
            error.Contains(
                "Category doesn't exist.",
                StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);

        // Act
        using var response = await SendDeleteAsync(
            category.Id,
            inboxId: null,
            accessToken: tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(error =>
            error.Contains(
                InboxHeader,
                StringComparison.OrdinalIgnoreCase));

        // A action não deve remover a categoria.
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .AnyAsync(item => item.Id == category.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ReturnsOk_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            category.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();

        // A categoria foi removida.
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .AnyAsync(item => item.Id == category.Id);

        exists.Should().BeFalse();

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var firstCategory = await CreateCategoryHttp(tokens);
        var secondCategory = await CreateCategoryHttp(tokens);

        string inboxId = Guid.NewGuid().ToString();

        // Primeira exclusão
        using (var firstResponse = await SendDeleteAsync(
                   firstCategory.Id,
                   inboxId,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda exclusão utilizando o mesmo Inbox
        using var response = await SendDeleteAsync(
            secondCategory.Id,
            inboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var firstExists = await db.Categories
            .AsNoTracking()
            .AnyAsync(item => item.Id == firstCategory.Id);

        var secondExists = await db.Categories
            .AsNoTracking()
            .AnyAsync(item => item.Id == secondCategory.Id);

        firstExists.Should().BeFalse();
        secondExists.Should().BeTrue();

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Autenticação e autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var (_, tokens) = await RegisterUserAsync();
        var category = await CreateCategoryHttp(
            await LoginUserMaster());

        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            category.Id,
            inboxId,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A categoria não pode ser removida sem a permissão.
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .AnyAsync(item => item.Id == category.Id);

        exists.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendDeleteAsync(
        string id,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{DeleteUrl}/{Uri.EscapeDataString(id)}";

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
}