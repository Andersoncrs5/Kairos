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
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            category.Id,
            inbox.Id,
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
        var updatedInbox = await GetInboxAsync(inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        updatedInbox.ProcessedAt.Should().NotBeNull();
        updatedInbox.LockedBy.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenIdIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            "invalid-id",
            inbox.Id,
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
        var inbox = await NewInboxAsync();
        var nonexistentId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            nonexistentId,
            inbox.Id,
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
    public async Task Delete_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendDeleteAsync(
            category.Id,
            nonexistentInboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<object>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        http.Errors.Should().Contain(error =>
            error.Contains(
                "Inbox is not available for processing.",
                StringComparison.OrdinalIgnoreCase));

        // A categoria deve permanecer persistida.
        using var scope = CreateScope();

        var exists = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .AnyAsync(item => item.Id == category.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var firstCategory = await CreateCategoryHttp(tokens);
        var secondCategory = await CreateCategoryHttp(tokens);

        var inbox = await NewInboxAsync();

        // Primeira exclusão
        using (var firstResponse = await SendDeleteAsync(
                   firstCategory.Id,
                   inbox.Id,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda exclusão utilizando o mesmo Inbox
        using var response = await SendDeleteAsync(
            secondCategory.Id,
            inbox.Id,
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

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);
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

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendDeleteAsync(
            category.Id,
            inbox.Id,
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

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
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