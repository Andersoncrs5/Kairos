using System.Net;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Res;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.DTOs.Response;
using Kairos.Api.Modules.Categories.Models;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Kairos.IntegrationTests.Module.Identity.Category.Controller;

[Collection("Integration Tests")]
public sealed class CategoryCreateControllerTest : BaseIntegrationTest
{
    private const string CreateUrl = "/api/v1/categories";

    private readonly ITestOutputHelper _output;

    public CategoryCreateControllerTest(
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
    public async Task Create_ReturnsCreated_AndPersistsCategoryAndInbox()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        // Act
        var category = await CreateCategoryHttp(tokens);

        // Assert
        category.Should().NotBeNull();
        category.Id.Should().NotBeNullOrWhiteSpace();
        category.Name.Should().StartWith("Integration Category ");
        category.Description.Should().Be("Created by integration tests.");
    }

    // ---------------------------------------------------------------
    // Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto(name: "");
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http =
            await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().NotBeNull();

        http.Data!.Keys.Should().Contain(key =>
            key.EndsWith(
                "Name",
                StringComparison.OrdinalIgnoreCase));

        // A validação automática impede a execução da action.
        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Name == dto.Name);

        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto(name: new string('a', 101));
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http =
            await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().NotBeNull();

        http.Data!.Keys.Should().Contain(key =>
            key.EndsWith(
                "Name",
                StringComparison.OrdinalIgnoreCase));

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Name == dto.Name);

        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDescriptionExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();

        var dto = NewCategoryDto(
            description: new string('a', 256));

        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            tokens.Token);

        _output.WriteLine(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var http =
            await ReadAsync<Dictionary<string, string[]>>(response);

        http.Should().NotBeNull();
        http!.Success.Should().BeFalse();
        http.Data.Should().NotBeNull();

        http.Data!.Keys.Should().Contain(key =>
            key.EndsWith(
                "Description",
                StringComparison.OrdinalIgnoreCase));

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Name == dto.Name);

        persisted.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConflict_WhenCategoryNameAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto();

        var firstInbox = await NewInboxAsync();

        // Primeiro cadastro
        using (var firstResponse = await SendCreateAsync(
                   dto,
                   firstInbox.Id,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var firstHttp = await ReadAsync<CategoryDto>(firstResponse);

            firstHttp.Should().NotBeNull();
            firstHttp!.Success.Should().BeTrue();
            firstHttp.Data.Should().NotBeNull();
            firstHttp.Data!.Name.Should().Be(dto.Name);
        }

        // Segundo cadastro com o mesmo nome
        var secondInbox = await NewInboxAsync();

        using var response = await SendCreateAsync(
            dto,
            secondInbox.Id,
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
                $"Category with the name {dto.Name} already exists.",
                StringComparison.OrdinalIgnoreCase));

        // A categoria original continua persistida apenas uma vez.
        using var scope = CreateScope();

        var count = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .CountAsync(category => category.Name == dto.Name);

        count.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto();

        // Act
        using var response = await SendCreateAsync(
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

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Name == dto.Name);

        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto();
        var nonexistentInboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(
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

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Name == dto.Name);

        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var inbox = await NewInboxAsync();

        var firstDto = NewCategoryDto();
        var secondDto = NewCategoryDto();

        // Primeiro cadastro
        using (var firstResponse = await SendCreateAsync(
                   firstDto,
                   inbox.Id,
                   tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Reutilização do mesmo Inbox
        using var response = await SendCreateAsync(
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

        var first = await db.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category =>
                category.Name == firstDto.Name);

        var second = await db.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category =>
                category.Name == secondDto.Name);

        first.Should().NotBeNull();
        second.Should().BeNull();

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Processed);
    }

    // ---------------------------------------------------------------
    // Autenticação e autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var (_, tokens) = await RegisterUserAsync();
        var dto = NewCategoryDto();
        var inbox = await NewInboxAsync();

        // Act
        using var response = await SendCreateAsync(
            dto,
            inbox.Id,
            tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = CreateScope();

        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Name == dto.Name);

        persisted.Should().BeNull();

        (await GetInboxAsync(inbox.Id))
            .Status.Should().Be(InboxStatusEnum.Pending);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Task<HttpResponseMessage> SendCreateAsync(
        CreateCategoryDto dto,
        string? inboxId,
        string? accessToken)
    {
        return SendAsync(
            CreateUrl,
            body: dto,
            accessToken: accessToken,
            inboxId: inboxId);
    }

    private static CreateCategoryDto NewCategoryDto(
        string? name = null,
        string? description = null)
    {
        return new CreateCategoryDto
        {
            Name = name ?? $"Integration Category {Guid.NewGuid():N}",
            Description = description ?? "Created by integration tests."
        };
    }
}