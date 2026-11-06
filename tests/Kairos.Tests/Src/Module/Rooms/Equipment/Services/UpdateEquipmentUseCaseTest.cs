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

public sealed class UpdateEquipmentUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IEquipmentRepository> _repositoryMock = new();
    private readonly UpdateEquipmentUseCase _sut;

    public UpdateEquipmentUseCaseTest()
    {
        _uowMock
            .Setup(uow => uow.EquipmentRepository)
            .Returns(_repositoryMock.Object);

        _sut = new UpdateEquipmentUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsUpdatedEquipment_WhenDataIsValid()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();
        var existingEquipment = CreateEquipment(id);
        var dto = new UpdateEquipmentDto
        {
            Code = "EQ-02",
            Name = "Updated Projector",
            Description = "Updated description",
            IsActive = false
        };
        var command = new UpdateEquipmentCommand(id, dto);

        _repositoryMock
            .Setup(r => r.FindByIdWithForUpdateAsync(id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEquipment);

        _repositoryMock
            .Setup(r => r.ExistsByCodeAsync("EQ-02", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repositoryMock
            .Setup(r => r.ExistsByNameAsync("Updated Projector", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().NotBeNull();
        result.Value.Code.Should().Be("EQ-02");
        result.Value.Name.Should().Be("Updated Projector");
        result.Value.Description.Should().Be("Updated description");
        result.Value.IsActive.Should().BeFalse();
        result.Value.Version.Should().Be(2);
        result.Value.UpdatedAt.Should().NotBeNull();

        _repositoryMock.Verify(
            r => r.FindByIdWithForUpdateAsync(id, null, It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(r => r.Update(existingEquipment), Times.Once);
    }

    // ---------------------------------------------------------------
    // Not Found
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenEquipmentDoesNotExist()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();
        var dto = new UpdateEquipmentDto { Name = "New Name" };
        var command = new UpdateEquipmentCommand(id, dto);

        _repositoryMock
            .Setup(r => r.FindByIdWithForUpdateAsync(id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EquipmentModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Equipment doesn't exist.");

        _repositoryMock.Verify(r => r.Update(It.IsAny<EquipmentModel>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Conflicts
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenCodeAlreadyExists()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();
        var existingEquipment = CreateEquipment(id);
        var dto = new UpdateEquipmentDto { Code = "EXISTS" };
        var command = new UpdateEquipmentCommand(id, dto);

        _repositoryMock
            .Setup(r => r.FindByIdWithForUpdateAsync(id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEquipment);

        _repositoryMock
            .Setup(r => r.ExistsByCodeAsync("EXISTS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain("Equipment with the code 'EXISTS' already exists.");

        _repositoryMock.Verify(r => r.Update(It.IsAny<EquipmentModel>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenNameAlreadyExists()
    {
        // Arrange
        string id = Guid.NewGuid().ToString();
        var existingEquipment = CreateEquipment(id);
        var dto = new UpdateEquipmentDto { Name = "Existing Name" };
        var command = new UpdateEquipmentCommand(id, dto);

        _repositoryMock
            .Setup(r => r.FindByIdWithForUpdateAsync(id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEquipment);

        _repositoryMock
            .Setup(r => r.ExistsByNameAsync("Existing Name", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain("Equipment with the name 'Existing Name' already exists.");

        _repositoryMock.Verify(r => r.Update(It.IsAny<EquipmentModel>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Validation Failures
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsInvalid()
    {
        // Arrange
        var dto = new UpdateEquipmentDto { Name = "Test" };
        var command = new UpdateEquipmentCommand("invalid-uuid", dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id must be a valid UUID.");

        _repositoryMock.Verify(
            r => r.FindByIdWithForUpdateAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Never);
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

        string id = Guid.NewGuid().ToString();
        var dto = new UpdateEquipmentDto { Name = "Test" };
        var command = new UpdateEquipmentCommand(id, dto);

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---------------------------------------------------------------
    // Helper
    // ---------------------------------------------------------------

    private static EquipmentModel CreateEquipment(string id)
    {
        return new EquipmentModel
        {
            Id = id,
            Code = "EQ-01",
            Name = "Projector",
            Description = "Standard projector",
            IsActive = true,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}