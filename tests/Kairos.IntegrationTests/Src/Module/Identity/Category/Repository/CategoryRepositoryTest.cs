using FluentAssertions;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairos.IntegrationTests.Module.Identity.Category.Repository;

[Collection("Integration Tests")]
public sealed class CategoryRepositoryTest : BaseIntegrationTest
{
    public CategoryRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenNameDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var name = $"Missing Category {Guid.NewGuid():N}";

        var result = await repository.GetByNameAsync(name);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsCategory_WhenNameExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var category = await CreateCategoryAsync(scope.ServiceProvider);

        var result = await repository.GetByNameAsync(category.Name);

        result.Should().NotBeNull();
        result!.Id.Should().Be(category.Id);
        result.Name.Should().Be(category.Name);
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsFalse_WhenNameDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var exists = await repository.ExistsByNameAsync(
            $"Missing Category {Guid.NewGuid():N}");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsTrue_WhenNameExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var category = await CreateCategoryAsync(scope.ServiceProvider);

        var exists = await repository.ExistsByNameAsync(category.Name);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveCategories()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var activeCategory = await CreateCategoryAsync(
            scope.ServiceProvider);

        var inactiveCategory = await CreateCategoryAsync(
            scope.ServiceProvider,
            category => category.IsActive = false);

        var result = await repository.GetActiveAsync();

        result.Select(category => category.Id)
            .Should()
            .Contain(activeCategory.Id);

        result.Select(category => category.Id)
            .Should()
            .NotContain(inactiveCategory.Id);
    }

    private static ICategoryRepository GetRepository(IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<ICategoryRepository>();
    }
}