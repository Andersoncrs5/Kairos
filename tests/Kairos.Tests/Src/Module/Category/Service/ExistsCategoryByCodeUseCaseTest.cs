using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Categories.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Category.Service;

public class ExistsCategoryByCodeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ExistsCategoryByCodeUseCase _useCase;

    public ExistsCategoryByCodeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _categoryRepository = Substitute.For<ICategoryRepository>();

        _unitOfWork.CategoryRepository.Returns(_categoryRepository);

        _useCase = new ExistsCategoryByCodeUseCase(_unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTrue_WhenCategoryCodeExists()
    {
        // Arrange
        const string code = "meetings-code";
        using CancellationTokenSource cts = new();

        _categoryRepository
            .ExistsByCodeAsync(code, cts.Token)
            .Returns(true);

        // Act
        var result = await _useCase.ExecuteAsync(code, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _categoryRepository
            .Received(1)
            .ExistsByCodeAsync(code, cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFalse_WhenCategoryCodeDoesNotExist()
    {
        // Arrange
        const string code = "non-existing-code";
        using CancellationTokenSource cts = new();

        _categoryRepository
            .ExistsByCodeAsync(code, cts.Token)
            .Returns(false);

        // Act
        var result = await _useCase.ExecuteAsync(code, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();

        await _categoryRepository
            .Received(1)
            .ExistsByCodeAsync(code, cts.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenCodeIsInvalid(string? code)
    {
        // Arrange
        using CancellationTokenSource cts = new();

        // Act
        var result = await _useCase.ExecuteAsync(code!, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Category code is required.");

        await _categoryRepository
            .DidNotReceive()
            .ExistsByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTrimCodeBeforeCheckingExistence()
    {
        // Arrange
        const string code = "  meetings-code  ";
        const string expectedCode = "meetings-code";

        using CancellationTokenSource cts = new();

        _categoryRepository
            .ExistsByCodeAsync(expectedCode, cts.Token)
            .Returns(true);

        // Act
        var result = await _useCase.ExecuteAsync(code, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _categoryRepository
            .Received(1)
            .ExistsByCodeAsync(expectedCode, cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateException_WhenRepositoryFails()
    {
        // Arrange
        const string code = "meetings-code";
        using CancellationTokenSource cts = new();

        var exception = new InvalidOperationException(
            "Repository operation failed.");

        _categoryRepository
            .ExistsByCodeAsync(code, cts.Token)
            .Returns(Task.FromException<bool>(exception));

        // Act
        Func<Task> act = async () =>
            await _useCase.ExecuteAsync(code, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Repository operation failed.");
    }
}