using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.Api.Modules.Rooms.Equipment.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Rooms.Equipment.Services;

public sealed class FindEquipmentByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IEquipmentRepository> _repositoryMock = new();
    private readonly FindEquipmentByIdUseCase _sut;

    public FindEquipmentByIdUseCaseTest()
    {
        _uowMock
            .Setup(uow => uow.EquipmentRepository)
            .Returns(_repositoryMock.Object);

        _sut = new FindEquipmentByIdUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsEquipment_WhenIdExists()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        var equipment = new EquipmentModel
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            Code = "EQ-001",
            Name = "Projector",
            Description = "HD Projector",
            IsActive = true,
        };

        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(equipment);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeSameAs(equipment);

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepository()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();
        using var cts = new CancellationTokenSource();

        var equipment = new EquipmentModel
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            Code = "EQ-002",
            Name = "Microphone",
            IsActive = true
        };

        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(
                id,
                cts.Token))
            .ReturnsAsync(equipment);

        // Act
        var result = await _sut.ExecuteAsync(id, cts.Token);

        // Assert
        result.IsFailure.Should().BeFalse();

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                id,
                cts.Token),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsEmpty(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Equipment ID is required.");

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("invalid-id")]
    [InlineData("123")]
    [InlineData("not-a-uuid")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsInvalid(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Id must be a valid UUID.");

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Equipamento inexistente
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenEquipmentDoesNotExist()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();

        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EquipmentModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Equipment doesn't exist.");

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        string id = Guid.NewGuid().ToString();

        // Act
        var act = () => _sut.ExecuteAsync(id, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}