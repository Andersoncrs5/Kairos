using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.Api.Modules.Rooms.Equipment.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.Equipment.Services;

public sealed class CreateEquipmentUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IEquipmentRepository> _repositoryMock = new();
    private readonly CreateEquipmentUseCase _sut;

    public CreateEquipmentUseCaseTest()
    {
        _uowMock
            .Setup(uow => uow.EquipmentRepository)
            .Returns(_repositoryMock.Object);

        _repositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<EquipmentModel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new CreateEquipmentUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsEquipment_WhenDataIsValid()
    {
        // Arrange
        var command = NewCommand();

        _repositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                command.Dto.Code,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                command.Dto.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        EquipmentModel? addedEquipment = null;

        _repositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<EquipmentModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<EquipmentModel, CancellationToken>(
                (equipment, _) => addedEquipment = equipment)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().NotBeNull();

        result.Value.Code.Should().Be(command.Dto.Code);
        result.Value.Name.Should().Be(command.Dto.Name);
        result.Value.Description.Should().Be(command.Dto.Description);
        result.Value.Id.Should().NotBeNullOrWhiteSpace();
        result.Value.Version.Should().Be(1);
        result.Value.IsActive.Should().BeTrue();
        result.Value.CreatedAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(5));

        addedEquipment.Should().BeSameAs(result.Value);

        _repositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                command.Dto.Code,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                command.Dto.Name,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<EquipmentModel>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TrimsCodeAndName_BeforeCheckingAndCreating()
    {
        // Arrange
        var command = NewCommand(code: "  EQ-01  ", name: "  Projector  ");

        _repositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                "EQ-01",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                "Projector",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        EquipmentModel? addedEquipment = null;

        _repositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<EquipmentModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<EquipmentModel, CancellationToken>(
                (equipment, _) => addedEquipment = equipment)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeFalse();
        addedEquipment.Should().NotBeNull();
        addedEquipment!.Code.Should().Be("EQ-01");
        addedEquipment.Name.Should().Be("Projector");
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepository()
    {
        // Arrange
        var command = NewCommand();
        using var cts = new CancellationTokenSource();

        _repositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                command.Dto.Code,
                cts.Token))
            .ReturnsAsync(false);

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                command.Dto.Name,
                cts.Token))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(command, cts.Token);

        // Assert
        result.IsFailure.Should().BeFalse();

        _repositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                command.Dto.Code,
                cts.Token),
            Times.Once);

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                command.Dto.Name,
                cts.Token),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // DTO Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenCodeIsEmpty()
    {
        // Arrange
        var command = NewCommand(code: "");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _repositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var command = NewCommand(name: "");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        _repositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Business Rules
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenCodeAlreadyExists()
    {
        // Arrange
        var command = NewCommand(code: "EQ-01");

        _repositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                "EQ-01",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain("Equipment with the code 'EQ-01' already exists.");

        VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenNameAlreadyExists()
    {
        // Arrange
        var command = NewCommand(name: "Projector");

        _repositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                command.Dto.Code,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                "Projector",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain("Equipment with the name 'Projector' already exists.");

        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var command = NewCommand();

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _repositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static CreateEquipmentCommand NewCommand(
        string code = "EQ-01",
        string name = "Projector",
        string? description = "4K Projector")
    {
        var dto = new CreateEquipmentDto
        {
            Code = code,
            Name = name,
            Description = description
        };

        return new CreateEquipmentCommand(dto, Guid.NewGuid().ToString());
    }

    private void VerifyAddAsyncWasNotCalled()
    {
        _repositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<EquipmentModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}