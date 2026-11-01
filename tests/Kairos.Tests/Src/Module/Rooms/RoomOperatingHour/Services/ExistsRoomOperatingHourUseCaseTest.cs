using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomOperatingHour.Services;

public sealed class ExistsRoomOperatingHourUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRoomOperatingHourRepository> _repositoryMock = new();

    private readonly ExistsRoomOperatingHourUseCase _sut;

    public ExistsRoomOperatingHourUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.RoomOperatingHourRepository)
            .Returns(_repositoryMock.Object);

        _sut = new ExistsRoomOperatingHourUseCase(
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenIdIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();

        result.Errors.Should().Contain(
            "Room operating hour ID is required.");

        _repositoryMock.Verify(
            x => x.ExistsAsync(
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

        result.Errors.Should().Contain(
            "Room operating hour ID is required.");

        _repositoryMock.Verify(
            x => x.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTrue_WhenRecordExists()
    {
        // Arrange
        const string id = "room-operating-hour-123";

        _repositoryMock
            .Setup(x => x.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            x => x.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFalse_WhenRecordDoesNotExist()
    {
        // Arrange
        const string id = "room-operating-hour-123";

        _repositoryMock
            .Setup(x => x.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();

        _repositoryMock.Verify(
            x => x.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTrimId_BeforeCallingRepository()
    {
        // Arrange
        const string id = "room-operating-hour-123";

        _repositoryMock
            .Setup(x => x.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(
            $"  {id}  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            x => x.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepository()
    {
        // Arrange
        const string id = "room-operating-hour-123";

        using var cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = cancellationTokenSource.Token;

        _repositoryMock
            .Setup(x => x.ExistsAsync(id, cancellationToken))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _repositoryMock.Verify(
            x => x.ExistsAsync(id, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCancelled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        const string id = "room-operating-hour-123";

        // Act
        var act = () => _sut.ExecuteAsync(
            id,
            cancellationTokenSource.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _repositoryMock.Verify(
            x => x.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}