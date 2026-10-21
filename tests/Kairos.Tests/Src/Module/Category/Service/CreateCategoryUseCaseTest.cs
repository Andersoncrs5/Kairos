using FluentAssertions;
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

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_ReturnsCreatedCategory()
    {
        // Arrange
        var dto = new CreateCategoryDto
        {
            Name = "Meetings",
            Description = "Meeting room reservations"
        };

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var beforeExecution = DateTimeOffset.UtcNow;

        _categoryRepository
            .ExistsByNameAsync(dto.Name, cancellationToken)
            .Returns(Task.FromResult(false));

        _categoryRepository
            .AddAsync(
                Arg.Any<CategoryModel>(),
                cancellationToken)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(dto, cancellationToken);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value.Should().NotBeNull();

        var category = result.Value!;

        category.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(category.Id, out _).Should().BeTrue();

        category.Name.Should().Be(dto.Name);
        category.Description.Should().Be(dto.Description);
        category.Version.Should().Be(0);
        category.UpdatedAt.Should().BeNull();

        category.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        category.CreatedAt.Should().BeOnOrBefore(afterExecution);

        await _categoryRepository
            .Received(1)
            .ExistsByNameAsync(dto.Name, cancellationToken);

        await _categoryRepository
            .Received(1)
            .AddAsync(
                Arg.Is<CategoryModel>(x =>
                    x.Id == category.Id &&
                    x.Name == dto.Name &&
                    x.Description == dto.Description),
                cancellationToken);

        // O commit fica sob responsabilidade do serviço orquestrador.
        _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryNameAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var dto = new CreateCategoryDto
        {
            Name = "Meetings",
            Description = "Meeting room reservations"
        };

        var cancellationToken = CancellationToken.None;

        _categoryRepository
            .ExistsByNameAsync(dto.Name, cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(dto, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            $"Category with the name {dto.Name} already exists.");

        await _categoryRepository
            .Received(1)
            .ExistsByNameAsync(dto.Name, cancellationToken);

        await _categoryRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<CategoryModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesException()
    {
        // Arrange
        var dto = new CreateCategoryDto
        {
            Name = "Meetings",
            Description = "Meeting room reservations"
        };

        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Database operation failed.");

        _categoryRepository
            .ExistsByNameAsync(dto.Name, cancellationToken)
            .Returns(Task.FromResult(false));

        _categoryRepository
            .AddAsync(
                Arg.Any<CategoryModel>(),
                cancellationToken)
            .Returns(Task.FromException(exception));

        // Act
        var act = () => _sut.ExecuteAsync(dto, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Database operation failed.");
    }
}