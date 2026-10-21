
using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Categories.Services.Provider;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Category.Service;

public sealed class FindCategoryByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryRepository _categoryRepository;
    private readonly FindCategoryByIdUseCase _sut;

    public FindCategoryByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _categoryRepository = Substitute.For<ICategoryRepository>();

        _unitOfWork.CategoryRepository.Returns(_categoryRepository);

        _sut = new FindCategoryByIdUseCase(_unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsEmpty()
    {
        var result = await _sut.ExecuteAsync(
            string.Empty,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id is required.");

        await _categoryRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsWhitespace()
    {
        var result = await _sut.ExecuteAsync(
            "   ",
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id is required.");

        await _categoryRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid-id")]
    [InlineData("123")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsNotGuid(
        string id)
    {
        var result = await _sut.ExecuteAsync(
            id,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id must be a valid UUID.");

        await _categoryRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        var id = Guid.NewGuid();

        _categoryRepository
            .GetByIdAsync(id.ToString(), CancellationToken.None)
            .Returns(Task.FromResult<CategoryModel?>(null));

        var result = await _sut.ExecuteAsync(
            id.ToString(),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Category not found.");

        await _categoryRepository
            .Received(1)
            .GetByIdAsync(id.ToString(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCategory_WhenCategoryExists()
    {
        var id = Guid.NewGuid();

        var category = new CategoryModel
        {
            Id = id.ToString(),
            Name = "Meeting Rooms",
            Description = "Categories for meeting rooms.",
            IsActive = true,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _categoryRepository
            .GetByIdAsync(id.ToString(), CancellationToken.None)
            .Returns(Task.FromResult<CategoryModel?>(category));

        var result = await _sut.ExecuteAsync(
            id.ToString(),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.StatusCode.Should().Be(200);
        result.Errors.Should().BeEmpty();
        result.Value.Should().BeSameAs(category);

        await _categoryRepository
            .Received(1)
            .GetByIdAsync(id.ToString(), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationTokenToRepository()
    {
        var id = Guid.NewGuid();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        _categoryRepository
            .GetByIdAsync(id.ToString(), cancellationToken)
            .Returns(Task.FromResult<CategoryModel?>(null));

        await _sut.ExecuteAsync(id.ToString(), cancellationToken);

        await _categoryRepository
            .Received(1)
            .GetByIdAsync(id.ToString(), cancellationToken);
    }
}
