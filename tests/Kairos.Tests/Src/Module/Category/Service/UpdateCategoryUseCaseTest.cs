using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.DTOs.Commands;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Categories.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Category.Service;

public sealed class UpdateCategoryUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryRepository _categoryRepository;
    private readonly UpdateCategoryUseCase _sut;

    public UpdateCategoryUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _categoryRepository = Substitute.For<ICategoryRepository>();

        _unitOfWork.CategoryRepository.Returns(_categoryRepository);

        _sut = new UpdateCategoryUseCase(_unitOfWork);
    }

    // ---------------------------------------------------------------
    // Validação do comando
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string id)
    {
        // Arrange
        var dto = new UpdateCategoryDto
        {
            Name = "Updated category"
        };

        var command = new UpdateCategoryCommand(id, dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Category ID is required.");

        await _categoryRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        const string id = "invalid-uuid";

        var dto = new UpdateCategoryDto
        {
            Name = "Updated category"
        };

        var command = new UpdateCategoryCommand(id, dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Id must be a valid UUID.");

        await _categoryRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Categoria inexistente
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var dto = new UpdateCategoryDto
        {
            Name = "Updated category"
        };

        var command = new UpdateCategoryCommand(id, dto);

        _categoryRepository
            .GetByIdAsync(
                id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Category not found.");

        await _categoryRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _categoryRepository
            .DidNotReceive()
            .Update(Arg.Any<CategoryModel>());
    }

    // ---------------------------------------------------------------
    // Conflito de nome
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenNameAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var category = CreateCategory(id, "Old category");

        var dto = new UpdateCategoryDto
        {
            Name = "Existing category"
        };

        var command = new UpdateCategoryCommand(id, dto);

        _categoryRepository
            .GetByIdAsync(
                id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(category));

        _categoryRepository
            .ExistsByNameAsync(
                "Existing category",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Category with the name Existing category already exists.");

        category.Name.Should().Be("Old category");

        _categoryRepository
            .DidNotReceive()
            .Update(Arg.Any<CategoryModel>());
    }

    // ---------------------------------------------------------------
    // Atualização bem-sucedida
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_UpdatesCategory()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var category = CreateCategory(id, "Old category");

        var dto = new UpdateCategoryDto
        {
            Name = "Updated category",
            Description = null,
            IsActive = false
        };

        var command = new UpdateCategoryCommand(id, dto);

        _categoryRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<CategoryModel?>(category));

        _categoryRepository
            .ExistsByNameAsync(
                "Updated category",
                cancellationToken)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(
            command,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(category);

        category.Name.Should().Be("Updated category");
        category.Description.Should().Be("Existing description");
        category.IsActive.Should().BeFalse();
        category.Version.Should().Be(1);
        category.UpdatedAt.Should().BeNull();

        _categoryRepository
            .Received(1)
            .Update(category);
    }

    // ---------------------------------------------------------------
    // Nome inalterado
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenNameIsUnchanged_DoesNotCheckDuplicate()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var category = CreateCategory(id, "Existing category");

        var dto = new UpdateCategoryDto
        {
            Name = "Existing category"
        };

        var command = new UpdateCategoryCommand(id, dto);

        _categoryRepository
            .GetByIdAsync(
                id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(category));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(category);

        category.Name.Should().Be("Existing category");

        await _categoryRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _categoryRepository
            .Received(1)
            .Update(category);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static CategoryModel CreateCategory(
        string id,
        string name)
    {
        return new CategoryModel
        {
            Id = id,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = null,
            Name = name,
            Description = "Existing description",
            IsActive = true
        };
    }
}