
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.DTOs.Commands;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Categories.Services.Provider;
using NSubstitute;
using Xunit;

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
        UpdateCategoryDto dto = new()
        {
            Name = "Updated category"
        };

        UpdateCategoryCommand command = new(id, dto);

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

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

        UpdateCategoryDto dto = new()
        {
            Name = "Updated category"
        };

        UpdateCategoryCommand command = new(id, dto);

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

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
    // Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = new()
        {
            Name = new string('A', 101)
        };

        UpdateCategoryCommand command = new(id, dto);

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().NotBeEmpty();

        await _categoryRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeIsWhitespace_ReturnsValidationFailure()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Existing category",
            "existing-code");

        UpdateCategoryDto dto = new()
        {
            Code = "   "
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Category code cannot be empty.");

        await _categoryRepository
            .DidNotReceive()
            .GetByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _categoryRepository
            .DidNotReceive()
            .Update(Arg.Any<CategoryModel>());
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = new()
        {
            Name = "Updated category"
        };

        UpdateCategoryCommand command = new(id, dto);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        Func<Task> act = async () =>
            await _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

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
        string id = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = new()
        {
            Name = "Updated category"
        };

        UpdateCategoryCommand command = new(id, dto);

        _categoryRepository
            .GetByIdAsync(
                id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(null));

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Category not found.");

        await _categoryRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _categoryRepository
            .DidNotReceive()
            .GetByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _categoryRepository
            .DidNotReceive()
            .Update(Arg.Any<CategoryModel>());
    }

    // ---------------------------------------------------------------
    // Conflito de código
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCodeAlreadyExists_ReturnsConflict()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Old category",
            "old-code");

        CategoryModel existingCategory = CreateCategory(
            Guid.NewGuid().ToString(),
            "Existing category",
            "existing-code");

        UpdateCategoryDto dto = new()
        {
            Code = "existing-code"
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        _categoryRepository
            .GetByCodeAsync(
                "existing-code",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(existingCategory));

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Category with the code 'existing-code' already exists.");

        category.Code.Should().Be("old-code");

        await _categoryRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _categoryRepository
            .DidNotReceive()
            .Update(Arg.Any<CategoryModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeIsUnchanged_DoesNotCheckDuplicate()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Existing category",
            "existing-code");

        UpdateCategoryDto dto = new()
        {
            Code = "existing-code"
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(category);

        category.Code.Should().Be("existing-code");

        await _categoryRepository
            .DidNotReceive()
            .GetByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _categoryRepository
            .Received(1)
            .Update(category);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeIsNew_UpdatesCategory()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Existing category",
            "old-code");

        UpdateCategoryDto dto = new()
        {
            Code = "new-code"
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        _categoryRepository
            .GetByCodeAsync(
                "new-code",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(null));

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(category);

        category.Code.Should().Be("new-code");

        _categoryRepository
            .Received(1)
            .Update(category);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeHasSurroundingSpaces_TrimsCodeBeforeChecking()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Existing category",
            "old-code");

        UpdateCategoryDto dto = new()
        {
            Code = "  new-code  "
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        _categoryRepository
            .GetByCodeAsync(
                "new-code",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(null));

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.Code.Should().Be("new-code");

        await _categoryRepository
            .Received(1)
            .GetByCodeAsync(
                "new-code",
                Arg.Any<CancellationToken>());

        _categoryRepository
            .Received(1)
            .Update(category);
    }

    // ---------------------------------------------------------------
    // Conflito de nome
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenNameAlreadyExists_ReturnsConflict()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Old category",
            "old-code");

        UpdateCategoryDto dto = new()
        {
            Name = "Existing category"
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        _categoryRepository
            .ExistsByNameAsync(
                "Existing category",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Category with the name 'Existing category' already exists.");

        category.Name.Should().Be("Old category");

        _categoryRepository
            .DidNotReceive()
            .Update(Arg.Any<CategoryModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameIsUnchanged_DoesNotCheckDuplicate()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Existing category",
            "existing-code");

        UpdateCategoryDto dto = new()
        {
            Name = "Existing category"
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

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
    // Atualização bem-sucedida
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_UpdatesCategory()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        using CancellationTokenSource cts = new();

        CancellationToken cancellationToken = cts.Token;

        CategoryModel category = CreateCategory(
            id,
            "Old category",
            "old-code");

        UpdateCategoryDto dto = new()
        {
            Code = "new-code",
            Name = "Updated category",
            Description = "Updated description",
            Color = "#2563EB",
            Icon = "calendar-check",
            DisplayOrder = 10,
            IsActive = false
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category, cancellationToken);

        _categoryRepository
            .GetByCodeAsync(
                "new-code",
                cancellationToken)
            .Returns(Task.FromResult<CategoryModel?>(null));

        _categoryRepository
            .ExistsByNameAsync(
                "Updated category",
                cancellationToken)
            .Returns(Task.FromResult(false));

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(
                command,
                cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(category);

        category.Code.Should().Be("new-code");
        category.Name.Should().Be("Updated category");
        category.Description.Should().Be("Updated description");
        category.Color.Should().Be("#2563EB");
        category.Icon.Should().Be("calendar-check");
        category.DisplayOrder.Should().Be(10);
        category.IsActive.Should().BeFalse();

        _categoryRepository
            .Received(1)
            .Update(category);

        await _categoryRepository
            .Received(1)
            .GetByCodeAsync(
                "new-code",
                cancellationToken);

        await _categoryRepository
            .Received(1)
            .ExistsByNameAsync(
                "Updated category",
                cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyDescriptionIsUpdated_PreservesOtherProperties()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Existing category",
            "existing-code");

        UpdateCategoryDto dto = new()
        {
            Description = "New description"
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        category.Code.Should().Be("existing-code");
        category.Name.Should().Be("Existing category");
        category.Description.Should().Be("New description");
        category.Color.Should().Be("#2563EB");
        category.Icon.Should().Be("calendar-check");
        category.DisplayOrder.Should().Be(10);
        category.IsActive.Should().BeTrue();

        _categoryRepository
            .Received(1)
            .Update(category);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoHasNullOptionalProperties_PreservesExistingValues()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Existing category",
            "existing-code");

        UpdateCategoryDto dto = new()
        {
            Code = null,
            Name = null,
            Description = null,
            Color = null,
            Icon = null,
            DisplayOrder = null,
            IsActive = null
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        // Act
        Result<CategoryModel> result =
            await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(category);

        category.Code.Should().Be("existing-code");
        category.Name.Should().Be("Existing category");
        category.Description.Should().Be("Existing description");
        category.Color.Should().Be("#2563EB");
        category.Icon.Should().Be("calendar-check");
        category.DisplayOrder.Should().Be(10);
        category.IsActive.Should().BeTrue();

        _categoryRepository
            .Received(1)
            .Update(category);
    }

    // ---------------------------------------------------------------
    // Exceções do repositório
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenFindingCategoryFails_PropagatesException()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        UpdateCategoryDto dto = new()
        {
            Name = "Updated category"
        };

        UpdateCategoryCommand command = new(id, dto);

        InvalidOperationException exception = new(
            "Repository operation failed.");

        _categoryRepository
            .GetByIdAsync(
                id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CategoryModel?>(exception));

        // Act
        Func<Task> act = async () =>
            await _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Repository operation failed.");

        _categoryRepository
            .DidNotReceive()
            .Update(Arg.Any<CategoryModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCheckingCodeFails_PropagatesException()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Existing category",
            "old-code");

        UpdateCategoryDto dto = new()
        {
            Code = "new-code"
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        InvalidOperationException exception = new(
            "Code lookup failed.");

        _categoryRepository
            .GetByCodeAsync(
                "new-code",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CategoryModel?>(exception));

        // Act
        Func<Task> act = async () =>
            await _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Code lookup failed.");

        category.Code.Should().Be("old-code");

        _categoryRepository
            .DidNotReceive()
            .Update(Arg.Any<CategoryModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCheckingNameFails_PropagatesException()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        CategoryModel category = CreateCategory(
            id,
            "Old category",
            "existing-code");

        UpdateCategoryDto dto = new()
        {
            Name = "New category"
        };

        UpdateCategoryCommand command = new(id, dto);

        SetupCategory(category);

        InvalidOperationException exception = new(
            "Name lookup failed.");

        _categoryRepository
            .ExistsByNameAsync(
                "New category",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(exception));

        // Act
        Func<Task> act = async () =>
            await _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Name lookup failed.");

        category.Name.Should().Be("Old category");

        _categoryRepository
            .DidNotReceive()
            .Update(Arg.Any<CategoryModel>());
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void SetupCategory(
        CategoryModel category,
        CancellationToken cancellationToken = default)
    {
        _categoryRepository
            .GetByIdAsync(
                category.Id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(category));
    }

    private static CategoryModel CreateCategory(
        string id,
        string name,
        string code)
    {
        return new CategoryModel
        {
            Id = id,
            Version = 1,
            Code = code,
            Name = name,
            Description = "Existing description",
            Color = "#2563EB",
            Icon = "calendar-check",
            DisplayOrder = 10,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = null
        };
    }
}
