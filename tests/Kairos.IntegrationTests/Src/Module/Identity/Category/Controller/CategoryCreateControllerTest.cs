using System.Net;
using FluentAssertions;
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
    // Validação do DTO (Code, Name, Description, Color)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCodeIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto(code: "");
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInboxProcessedAsync(inboxId);

        using var scope = CreateScope();
        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Name == dto.Name);

        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCodeExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto(code: new string('a', 101));
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto(name: "");
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInboxProcessedAsync(inboxId);

        using var scope = CreateScope();
        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Code == dto.Code);

        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto(name: new string('a', 101));
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDescriptionExceedsMaximumLength()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto(description: new string('a', 256));
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenColorIsInvalid()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto();
        // Atribui uma cor inválida (fora do padrão hexadecimal #RRGGBB)
        var invalidDto = new CreateCategoryDto
        {
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Color = "invalid-color"
        };
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(invalidDto, inboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Regras de Negócio (Duplicidades)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConflict_WhenCategoryNameAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto();

        string firstInboxId = Guid.NewGuid().ToString();

        // Primeiro cadastro
        using (var firstResponse = await SendCreateAsync(dto, firstInboxId, tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Segundo cadastro com o mesmo Nome (mas código diferente para não dar conflito de código primeiro)
        var duplicateNameDto = NewCategoryDto(name: dto.Name);
        string secondInboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(duplicateNameDto, secondInboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = CreateScope();
        var count = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .CountAsync(c => c.Name == dto.Name);

        count.Should().Be(1);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenCategoryCodeAlreadyExists()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto();

        string firstInboxId = Guid.NewGuid().ToString();

        // Primeiro cadastro
        using (var firstResponse = await SendCreateAsync(dto, firstInboxId, tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Segundo cadastro com o mesmo Code (mas nome diferente para não dar conflito de nome primeiro)
        var duplicateCodeDto = new CreateCategoryDto
        {
            Code = dto.Code,
            Name = $"Different Name {Guid.NewGuid():N}",
            Description = "Another description"
        };
        string secondInboxId = Guid.NewGuid().ToString();

        using var response = await SendCreateAsync(duplicateCodeDto, secondInboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = CreateScope();
        var count = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .CountAsync(c => c.Code == dto.Code);

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
        using var response = await SendCreateAsync(dto, inboxId: null, accessToken: tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = CreateScope();
        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Name == dto.Name);

        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenInboxDoesNotExist()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        var dto = NewCategoryDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        var tokens = await LoginUserMaster();
        string inboxId = Guid.NewGuid().ToString();

        var firstDto = NewCategoryDto();
        var secondDto = NewCategoryDto();

        // Primeiro cadastro
        using (var firstResponse = await SendCreateAsync(firstDto, inboxId, tokens.Token))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Reutilização do mesmo Inbox
        using var response = await SendCreateAsync(secondDto, inboxId, tokens.Token);
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var first = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Name == firstDto.Name);
        var second = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Name == secondDto.Name);

        first.Should().NotBeNull();
        second.Should().BeNull();

        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Autenticação e Autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var (_, tokens) = await RegisterUserAsync();
        var dto = NewCategoryDto();
        string inboxId = Guid.NewGuid().ToString();

        // Act
        using var response = await SendCreateAsync(dto, inboxId, tokens.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = CreateScope();
        var persisted = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Name == dto.Name);

        persisted.Should().BeNull();
        await AssertInboxProcessedAsync(inboxId);
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
        string? code = null,
        string? name = null,
        string? description = null,
        string? color = "#2563EB")
    {
        return new CreateCategoryDto
        {
            Code = code ?? $"integration-code-{Guid.NewGuid():N}",
            Name = name ?? $"Integration Category {Guid.NewGuid():N}",
            Description = description ?? "Created by integration tests.",
            Color = color
        };
    }
}