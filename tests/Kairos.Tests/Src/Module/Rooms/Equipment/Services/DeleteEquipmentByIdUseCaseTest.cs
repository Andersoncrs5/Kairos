using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.Api.Modules.Rooms.Equipment.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.Equipment.Services;

public sealed class DeleteEquipmentByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IEquipmentRepository> _equipmentRepository;
    private readonly DeleteEquipmentByIdUseCase _sut;

    public DeleteEquipmentByIdUseCaseTest()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _equipmentRepository = new Mock<IEquipmentRepository>();

        _unitOfWork
            .SetupGet(uow => uow.EquipmentRepository)
            .Returns(_equipmentRepository.Object);

        _sut = new DeleteEquipmentByIdUseCase(_unitOfWork.Object);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_AndDeletesEquipment_WhenEquipmentExists()
    {
        var id = Guid.NewGuid().ToString();
        var equipment = CreateEquipment(id);

        _equipmentRepository
            .Setup(repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(equipment);

        var result = await _sut.ExecuteAsync(id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _equipmentRepository.Verify(
            repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _equipmentRepository.Verify(
            repository => repository.Delete(equipment),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsEmpty(
        string id)
    {
        var result = await _sut.ExecuteAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Equipment ID is required.");

        _equipmentRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _equipmentRepository.Verify(
            repository => repository.Delete(
                It.IsAny<EquipmentModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsNotValidUuid()
    {
        var result = await _sut.ExecuteAsync("invalid-uuid");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Id must be a valid UUID.");

        _equipmentRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _equipmentRepository.Verify(
            repository => repository.Delete(
                It.IsAny<EquipmentModel>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenEquipmentDoesNotExist()
    {
        var id = Guid.NewGuid().ToString();

        _equipmentRepository
            .Setup(repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EquipmentModel?)null);

        var result = await _sut.ExecuteAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Equipment doesn't exist.");

        _equipmentRepository.Verify(
            repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _equipmentRepository.Verify(
            repository => repository.Delete(
                It.IsAny<EquipmentModel>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancellationIsRequested()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var act = async () => await _sut.ExecuteAsync(
            Guid.NewGuid().ToString(),
            cancellationTokenSource.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _equipmentRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _equipmentRepository.Verify(
            repository => repository.Delete(
                It.IsAny<EquipmentModel>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------

    private static EquipmentModel CreateEquipment(string id)
    {
        return new EquipmentModel
        {
            Id = id,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            Code = $"EQ-{Guid.NewGuid():N}"[..Math.Min(50, $"EQ-{Guid.NewGuid():N}".Length)],
            Name = "Unit Test Equipment",
            Description = "Equipment created for unit testing.",
            IsActive = true
        };
    }
}