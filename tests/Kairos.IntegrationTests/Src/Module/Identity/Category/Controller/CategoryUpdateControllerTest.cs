using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Res;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.DTOs.Response;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Category.Controller;

[Collection("Integration Tests")]
public sealed class CategoryUpdateControllerTest : BaseIntegrationTest
{
    private const string UpdateUrl = "/api/v1/categories";

    private readonly ITestOutputHelper _output;

    public CategoryUpdateControllerTest(
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
    public async Task Update_ReturnsOk_AndPersistsChangesAndProcessesInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var inbox = await NewInboxAsync();

        var dto = NewUpdateCategoryDto();

        // Act
        using var response = await SendUpdateAsync(
            category.Id,
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeTrue();
        http.Data.Should().NotBeNull();
        http.Data!.Id.Should().Be(category.Id);
        http.Data.Name.Should().Be(dto.Name);
        http.Data.Description.Should().Be(dto.Description);
        http.TraceId.Should().NotBeNullOrWhiteSpace();

        // Confirma a persistência das alterações.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(dto.Name);
        persisted.Description.Should().Be(dto.Description);

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
    public async Task Update_ReturnsBadRequest_WhenIdIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();
        var dto = NewUpdateCategoryDto();

        // Act
        using var response = await SendUpdateAsync(
            "invalid-id",
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();
        var nonexistentId = Guid.NewGuid().ToString();
        var dto = NewUpdateCategoryDto();

        // Act
        using var response = await SendUpdateAsync(
            nonexistentId,
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsConflict_WhenCategoryNameAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var categoryToUpdate = await CreateCategoryHttp(tokens);
        var existingCategory = await CreateCategoryHttp(tokens);
        var inbox = await NewInboxAsync();

        var originalName = categoryToUpdate.Name;

        var dto = NewUpdateCategoryDto(
            name: existingCategory.Name);

        // Act
        using var response = await SendUpdateAsync(
            categoryToUpdate.Id,
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        // A categoria original não pode ser alterada.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == categoryToUpdate.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(originalName);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var originalName = category.Name;

        var dto = NewUpdateCategoryDto();

        // Act
        using var response = await SendUpdateAsync(
            category.Id,
            dto,
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

        // A categoria não pode ser alterada.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(originalName);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);
        var originalName = category.Name;

        var nonexistentInboxId = Guid.NewGuid().ToString();
        var dto = NewUpdateCategoryDto();

        // Act
        using var response = await SendUpdateAsync(
            category.Id,
            dto,
            nonexistentInboxId,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Errors.Should().Contain(error =>
            error.Contains(
                "Inbox is not available for processing.",
                StringComparison.OrdinalIgnoreCase));

        // A categoria deve permanecer inalterada.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(originalName);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var firstCategory = await CreateCategoryHttp(tokens);
        var secondCategory = await CreateCategoryHttp(tokens);

        var originalSecondName = secondCategory.Name;
        var inbox = await NewInboxAsync();

        var firstDto = NewUpdateCategoryDto();
        var secondDto = NewUpdateCategoryDto();

        // Primeira atualização.
        using (var firstResponse = await SendUpdateAsync(
                   firstCategory.Id,
                   firstDto,
                   inbox.Id,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda atualização reutilizando o Inbox processado.
        using var response = await SendUpdateAsync(
            secondCategory.Id,
            secondDto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var http = await ReadAsync<CategoryDto>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();

        using var scope = CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persistedFirst = await db.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == firstCategory.Id);

        var persistedSecond = await db.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == secondCategory.Id);

        // A primeira atualização foi persistida.
        persistedFirst.Should().NotBeNull();
        persistedFirst!.Name.Should().Be(firstDto.Name);

        // A segunda atualização não deve ser executada.
        persistedSecond.Should().NotBeNull();
        persistedSecond!.Name.Should().Be(originalSecondName);

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Autenticação e autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var category = await CreateCategoryHttp(tokens);

        var originalName = category.Name;
        var inbox = await NewInboxAsync();
        var dto = NewUpdateCategoryDto();

        var (_, userTokens) = await RegisterUserAsync();

        // Act
        using var response = await SendUpdateAsync(
            category.Id,
            dto,
            inbox.Id,
            userTokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A categoria não pode ser alterada sem a permissão.
        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(originalName);

        // A requisição não autorizada não deve processar o Inbox.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<HttpResponseMessage> SendUpdateAsync(
        string id,
        UpdateCategoryDto dto,
        string? inboxId,
        string? accessToken)
    {
        var url = $"{UpdateUrl}/{Uri.EscapeDataString(id)}";

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            url)
        {
            Content = JsonContent.Create(dto)
        };

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

    private static UpdateCategoryDto NewUpdateCategoryDto(
        string? name = null,
        string? description = null)
    {
        return new UpdateCategoryDto
        {
            Name = name ?? $"Updated Integration Category {Guid.NewGuid():N}",
            Description = description ?? "Updated by integration tests."
        };
    }
}