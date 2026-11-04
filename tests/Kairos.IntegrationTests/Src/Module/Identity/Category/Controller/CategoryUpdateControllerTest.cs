using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    public async Task Update_ReturnsOk_AndPersistsAllChangesAndProcessesInbox()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            code: $"updated-code-{Guid.NewGuid():N}",
            name: $"Updated category {Guid.NewGuid():N}",
            description: "Updated by integration tests.",
            color: "#2563EB",
            icon: "calendar-check",
            displayOrder: 20,
            isActive: false);

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken))
        {
            _output.WriteLine(
                await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeTrue();
            http.Data.Should().NotBeNull();

            http.Data!.Id.Should().Be(category.Id);
            http.Data.Code.Should().Be(dto.Code);
            http.Data.Name.Should().Be(dto.Name);
            http.Data.Description.Should().Be(dto.Description);
            http.Data.Color.Should().Be(dto.Color);
            http.Data.Icon.Should().Be(dto.Icon);
            http.Data.DisplayOrder.Should().Be(dto.DisplayOrder);
            http.Data.IsActive.Should().Be(dto.IsActive!.Value);

            http.TraceId.Should().Be(inboxId);
            http.Message.Should().Be("Category updated successfully");
        }

        // Confirma persistência no banco.
        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(dto.Code);
        persisted.Name.Should().Be(dto.Name);
        persisted.Description.Should().Be(dto.Description);
        persisted.Color.Should().Be(dto.Color);
        persisted.Icon.Should().Be(dto.Icon);
        persisted.DisplayOrder.Should().Be(dto.DisplayOrder);
        persisted.IsActive.Should().Be(dto.IsActive!.Value);

        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenCodeIsUnchanged()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            code: category.Code);

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeTrue();
            http.Data!.Code.Should().Be(category.Code);
        }

        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(category.Code);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenCodeIsChanged()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string inboxId = Guid.NewGuid().ToString();

        string newCode = $"new-code-{Guid.NewGuid():N}";

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            code: newCode);

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeTrue();
            http.Data!.Code.Should().Be(newCode);
        }

        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(newCode);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenCodeContainsSurroundingSpaces()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string inboxId = Guid.NewGuid().ToString();

        string newCode = $"trimmed-code-{Guid.NewGuid():N}";

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            code: $"  {newCode}  ");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeTrue();
            http.Data!.Code.Should().Be(newCode);
        }

        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(newCode);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenNameIsUnchanged()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            name: category.Name);

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ResponseHttp<CategoryDto>? http = await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeTrue();
            http.Data!.Name.Should().Be(category.Name);
        }
    }

    // ---------------------------------------------------------------
    // Validação do identificador e dos dados
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdIsInvalid()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(name: "Updated category");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            "invalid-id",
            dto,
            inboxId,
            accessToken))
        {
            _output.WriteLine(
                await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeFalse();
            http.Errors.Should().Contain("Id must be a valid UUID.");
        }
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenCategoryDoesNotExistAndIdIsValid()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;
        string inboxId = Guid.NewGuid().ToString();
        string nonexistentId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            name: "Updated category");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            nonexistentId,
            dto,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeFalse();
            http.Errors.Should().Contain("Category not found.");
        }
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenDtoContainsInvalidColor()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string originalColor = category.Color ?? string.Empty;

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            color: "invalid-color");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // A categoria não pode ser alterada.
        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Color.Should().BeNull();
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenCodeIsWhitespace()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string originalCode = category.Code;

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            code: "   ");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(originalCode);
    }

    // ---------------------------------------------------------------
    // Categoria inexistente
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        string inboxId = Guid.NewGuid().ToString();

        string nonexistentId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            name: "Updated category");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            nonexistentId,
            dto,
            inboxId,
            accessToken))
        {
            _output.WriteLine(
                await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeFalse();
            http.Errors.Should().Contain("Category not found.");
        }
    }

    // ---------------------------------------------------------------
    // Regras de negócio: código
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsConflict_WhenCategoryCodeAlreadyExists()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto categoryToUpdate =
            await CreateCategoryHttp(await LoginUserMaster());

        CategoryDto existingCategory =
            await CreateCategoryHttp(await LoginUserMaster());

        string originalCode = categoryToUpdate.Code;
        string originalName = categoryToUpdate.Name;

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            code: existingCategory.Code);

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            categoryToUpdate.Id,
            dto,
            inboxId,
            accessToken))
        {
            _output.WriteLine(
                await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeFalse();

            http.Errors.Should().Contain(
                $"Category with the code '{existingCategory.Code}' already exists.");
        }

        // A categoria original não pode ser alterada.
        CategoryModel? persisted =
            await FindPersistedCategoryAsync(categoryToUpdate.Id);

        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(originalCode);
        persisted.Name.Should().Be(originalName);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenCodeBelongsToCurrentCategory()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            code: category.Code,
            name: $"Renamed category {Guid.NewGuid():N}");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeTrue();
            http.Data!.Code.Should().Be(category.Code);
            http.Data.Name.Should().Be(dto.Name);
        }

        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Code.Should().Be(category.Code);
        persisted.Name.Should().Be(dto.Name);
        
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Regras de negócio: nome
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsConflict_WhenCategoryNameAlreadyExists()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto categoryToUpdate =
            await CreateCategoryHttp(await LoginUserMaster());

        CategoryDto existingCategory =
            await CreateCategoryHttp(await LoginUserMaster());

        string originalName = categoryToUpdate.Name;
        string originalCode = categoryToUpdate.Code;

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            name: existingCategory.Name);

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            categoryToUpdate.Id,
            dto,
            inboxId,
            accessToken))
        {
            _output.WriteLine(
                await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeFalse();

            http.Errors.Should().Contain(
                $"Category with the name '{existingCategory.Name}' already exists.");
        }

        // A categoria original não pode ser alterada.
        CategoryModel? persisted =
            await FindPersistedCategoryAsync(categoryToUpdate.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(originalName);
        persisted.Code.Should().Be(originalCode);
        
        await AssertInboxProcessedAsync(inboxId);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenNameBelongsToCurrentCategory()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            name: category.Name);

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ResponseHttp<CategoryDto>? http =
                await ReadAsync<CategoryDto>(response);

            http.Should().NotBeNull();
            http!.Success.Should().BeTrue();
            http.Data!.Name.Should().Be(category.Name);
        }

        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(category.Name);
        
        await AssertInboxProcessedAsync(inboxId);
    }

    // ---------------------------------------------------------------
    // Inbox
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenInboxHeaderIsMissing()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string originalName = category.Name;

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            name: $"Changed category {Guid.NewGuid():N}");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId: null,
            accessToken: accessToken))
        {
            _output.WriteLine(
                await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            string body = await response.Content.ReadAsStringAsync();
            
        }

        // A categoria não pode ser alterada.
        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(originalName);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxDoesNotExist()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string originalName = category.Name;

        string nonexistentInboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            name: $"Changed category {Guid.NewGuid():N}");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            nonexistentInboxId,
            accessToken))
        {
            _output.WriteLine(
                await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        // A categoria deve permanecer inalterada.
        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(originalName);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenInboxIsReusedAfterSuccess()
    {
        // Arrange
        string accessToken = (await LoginUserMaster()).Token;

        CategoryDto firstCategory =
            await CreateCategoryHttp(await LoginUserMaster());

        CategoryDto secondCategory =
            await CreateCategoryHttp(await LoginUserMaster());

        string originalSecondName = secondCategory.Name;

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto firstDto = NewUpdateCategoryDto(
            name: $"First updated category {Guid.NewGuid():N}");

        UpdateCategoryDto secondDto = NewUpdateCategoryDto(
            name: $"Second updated category {Guid.NewGuid():N}");

        // Primeira atualização.
        using (HttpResponseMessage firstResponse = await SendUpdateAsync(
            firstCategory.Id,
            firstDto,
            inboxId,
            accessToken))
        {
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Segunda atualização reutilizando o Inbox processado.
        using (HttpResponseMessage response = await SendUpdateAsync(
            secondCategory.Id,
            secondDto,
            inboxId,
            accessToken))
        {
            _output.WriteLine(
                await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        // Confirma a persistência da primeira atualização.
        CategoryModel? persistedFirst =
            await FindPersistedCategoryAsync(firstCategory.Id);

        persistedFirst.Should().NotBeNull();
        persistedFirst!.Name.Should().Be(firstDto.Name);

        // A segunda atualização não deve ser executada.
        CategoryModel? persistedSecond =
            await FindPersistedCategoryAsync(secondCategory.Id);

        persistedSecond.Should().NotBeNull();
        persistedSecond!.Name.Should().Be(originalSecondName);

    }

    // ---------------------------------------------------------------
    // Autenticação e autorização
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string originalName = category.Name;

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            name: $"Changed category {Guid.NewGuid():N}");

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            accessToken: null))
        {
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(originalName);
    }

    [Fact]
    public async Task Update_ReturnsForbidden_WhenUserLacksPermission()
    {
        // Arrange
        CategoryDto category =
            await CreateCategoryHttp(await LoginUserMaster());

        string originalName = category.Name;

        string inboxId = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = NewUpdateCategoryDto(
            name: $"Changed category {Guid.NewGuid():N}");

        string userAccessToken =
            (await RegisterUserAsync()).Item2.Token;

        // Act
        using (HttpResponseMessage response = await SendUpdateAsync(
            category.Id,
            dto,
            inboxId,
            userAccessToken))
        {
            _output.WriteLine(
                await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // A categoria não pode ser alterada sem permissão.
        CategoryModel? persisted =
            await FindPersistedCategoryAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(originalName);
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
        string url =
            $"{UpdateUrl}/{Uri.EscapeDataString(id)}";

        using (HttpRequestMessage request = new(
            HttpMethod.Put,
            url))
        {
            request.Content = JsonContent.Create(dto);

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

    private static UpdateCategoryDto NewUpdateCategoryDto(
        string? code = null,
        string? name = null,
        string? description = null,
        string? color = null,
        string? icon = null,
        int? displayOrder = null,
        bool? isActive = null)
    {
        return new UpdateCategoryDto
        {
            Code = code,
            Name = name,
            Description = description,
            Color = color,
            Icon = icon,
            DisplayOrder = displayOrder,
            IsActive = isActive
        };
    }

    private async Task<CategoryModel?> FindPersistedCategoryAsync(
        string categoryId)
    {
        using IServiceScope scope = CreateScope();

        AppDbContext context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        return await context.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                category => category.Id == categoryId);
    }
}
