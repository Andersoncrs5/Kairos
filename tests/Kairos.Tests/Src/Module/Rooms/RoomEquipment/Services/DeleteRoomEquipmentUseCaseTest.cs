using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomEquipment.Services;

public sealed class DeleteRoomEquipmentUseCaseTest
{
    private const string NotFoundMessage = "Room equipment not found.";

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRoomEquipmentRepository> _repositoryMock = new();

    private readonly DeleteRoomEquipmentUseCase _sut;

    public DeleteRoomEquipmentUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.RoomEquipmentRepository)
            .Returns(_repositoryMock.Object);

        _sut = new DeleteRoomEquipmentUseCase(
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenIdIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room equipment ID is required.");

        _repositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenIdIsEmpty(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room equipment ID is required.");

        _repositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenIdIsNotValidUuid()
    {
        // Act
        var result = await _sut.ExecuteAsync("invalid-uuid");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Room equipment ID must be a valid UUID.");

        _repositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTrue_WhenRecordIsDeleted()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        _repositoryMock
            .Setup(x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRecordDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        _repositoryMock
            .Setup(x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain(NotFoundMessage);

        _repositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTrue_WhenMultipleRecordsAreDeleted()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        _repositoryMock
            .Setup(x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTrimId_BeforeCallingRepository()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        _repositoryMock
            .Setup(x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.ExecuteAsync($"  {id}  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepository()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _repositoryMock
            .Setup(x => x.DeleteByIdAndCountAsync(
                id,
                cancellationToken))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _repositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                id,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCancelled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var id = Guid.NewGuid().ToString();

        // Act
        var act = () => _sut.ExecuteAsync(
            id,
            cancellationTokenSource.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _repositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}