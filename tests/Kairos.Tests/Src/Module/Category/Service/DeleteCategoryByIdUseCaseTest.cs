using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Categories.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Category.Service;

public class DeleteCategoryByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryRepository _categoryRepository;
    private readonly DeleteCategoryByIdUseCase _sut;

    public DeleteCategoryByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _categoryRepository = Substitute.For<ICategoryRepository>();

        _unitOfWork.CategoryRepository.Returns(_categoryRepository);

        _sut = new DeleteCategoryByIdUseCase(_unitOfWork);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(
            id,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Id is required.");

        await _categoryRepository
            .DidNotReceive()
            .DeleteByIdAndCountAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        const string id = "invalid-uuid";

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Id must be a valid UUID.");

        await _categoryRepository
            .DidNotReceive()
            .DeleteByIdAndCountAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        _categoryRepository
            .DeleteByIdAndCountAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Category doesn't exist.");

        await _categoryRepository
            .Received(1)
            .DeleteByIdAndCountAsync(
                id,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryExists_DeletesCategory()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _categoryRepository
            .DeleteByIdAndCountAsync(id, cancellationToken)
            .Returns(Task.FromResult(1));

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().Be(true);

        await _categoryRepository
            .Received(1)
            .DeleteByIdAndCountAsync(id, cancellationToken);
    }
}