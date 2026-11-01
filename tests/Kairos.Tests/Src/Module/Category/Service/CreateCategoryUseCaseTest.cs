
using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Categories.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Category.Service;

public class CreateCategoryUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryRepository _categoryRepository;
    private readonly CreateCategoryUseCase _sut;

    public CreateCategoryUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _categoryRepository = Substitute.For<ICategoryRepository>();

        _unitOfWork.CategoryRepository.Returns(_categoryRepository);

        _sut = new CreateCategoryUseCase(_unitOfWork);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static CreateCategoryDto CreateDto()
    {
        return new CreateCategoryDto
        {
            Code = "meetings-code",
            Name = "Meetings",
            Description = "Meeting room reservations",
            Color = "#2563EB",
            Icon = "calendar-check",
            DisplayOrder = 10
        };
    }

    private void SetupNoDuplicates(
        CreateCategoryDto dto,
        CancellationToken cancellationToken)
    {
        _categoryRepository
            .ExistsByCodeAsync(dto.Code, cancellationToken)
            .Returns(Task.FromResult(false));

        _categoryRepository
            .ExistsByNameAsync(dto.Name, cancellationToken)
            .Returns(Task.FromResult(false));
    }

    // ------------------------------------------------------------------
    // Success
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_ReturnsCreatedCategory()
    {
        // Arrange
        var dto = CreateDto();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var beforeExecution = DateTimeOffset.UtcNow;

        SetupNoDuplicates(dto, cancellationToken);

        _categoryRepository
            .AddAsync(
                Arg.Any<CategoryModel>(),
                cancellationToken)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(
            dto,
            cancellationToken);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert - Result
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value.Should().NotBeNull();

        // Assert - Category
        CategoryModel category = result.Value!;

        category.Id.Should().NotBeNullOrWhiteSpace();

        Guid.TryParse(category.Id, out _)
            .Should().BeTrue();

        category.Code.Should().Be(dto.Code);
        category.Name.Should().Be(dto.Name);
        category.Description.Should().Be(dto.Description);
        category.Color.Should().Be(dto.Color);
        category.Icon.Should().Be(dto.Icon);
        category.DisplayOrder.Should().Be(dto.DisplayOrder);

        category.IsActive.Should().BeTrue();
        category.Version.Should().Be(0);
        category.UpdatedAt.Should().BeNull();

        category.CreatedAt.Should()
            .BeOnOrAfter(beforeExecution);

        category.CreatedAt.Should()
            .BeOnOrBefore(afterExecution);

        // Assert - Repository calls
        await _categoryRepository
            .Received(1)
            .ExistsByCodeAsync(dto.Code, cancellationToken);

        await _categoryRepository
            .Received(1)
            .ExistsByNameAsync(dto.Name, cancellationToken);

        await _categoryRepository
            .Received(1)
            .AddAsync(
                Arg.Is<CategoryModel>(item =>
                    item.Id == category.Id &&
                    item.Code == dto.Code &&
                    item.Name == dto.Name &&
                    item.Description == dto.Description &&
                    item.Color == dto.Color &&
                    item.Icon == dto.Icon &&
                    item.DisplayOrder == dto.DisplayOrder),
                cancellationToken);

        // Commit remains the responsibility of the orchestrator.
        _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Duplicate Code
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCategoryCodeAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var dto = CreateDto();
        var cancellationToken = CancellationToken.None;

        _categoryRepository
            .ExistsByCodeAsync(dto.Code, cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            dto,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            $"Category with the code '{dto.Code}' already exists.");

        await _categoryRepository
            .Received(1)
            .ExistsByCodeAsync(dto.Code, cancellationToken);

        // Name must not be checked after a duplicate code is found.
        await _categoryRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _categoryRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<CategoryModel>(),
                Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Duplicate Name
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCategoryNameAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var dto = CreateDto();
        var cancellationToken = CancellationToken.None;

        _categoryRepository
            .ExistsByCodeAsync(dto.Code, cancellationToken)
            .Returns(Task.FromResult(false));

        _categoryRepository
            .ExistsByNameAsync(dto.Name, cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            dto,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            $"Category with the name '{dto.Name}' already exists.");

        // Code is checked first.
        await _categoryRepository
            .Received(1)
            .ExistsByCodeAsync(dto.Code, cancellationToken);

        await _categoryRepository
            .Received(1)
            .ExistsByNameAsync(dto.Name, cancellationToken);

        await _categoryRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<CategoryModel>(),
                Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Repository exception
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCodeRepositoryFails_PropagatesException()
    {
        // Arrange
        var dto = CreateDto();
        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Database operation failed.");

        _categoryRepository
            .ExistsByCodeAsync(dto.Code, cancellationToken)
            .Returns(Task.FromException<bool>(exception));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(
            dto,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Database operation failed.");

        // No subsequent operations should be executed.
        await _categoryRepository
            .DidNotReceive()
            .ExistsByNameAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _categoryRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<CategoryModel>(),
                Arg.Any<CancellationToken>());
    }
}
