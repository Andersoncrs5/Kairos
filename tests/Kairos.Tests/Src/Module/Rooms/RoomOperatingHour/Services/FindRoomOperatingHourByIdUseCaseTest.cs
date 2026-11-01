using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomOperatingHour.Services;

public sealed class FindRoomOperatingHourByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRoomOperatingHourRepository> _repositoryMock = new();

    private readonly FindRoomOperatingHourByIdUseCase _sut;

    public FindRoomOperatingHourByIdUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.RoomOperatingHourRepository)
            .Returns(_repositoryMock.Object);

        _sut = new FindRoomOperatingHourByIdUseCase(
            _unitOfWorkMock.Object);
    }

    // ---------------------------------------------------------------
    // ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenIdIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room operating hour ID is required.");

        VerifyNoRepositoryCalls();
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
            "Room operating hour ID is required.");

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnModel_WhenRecordExists()
    {
        // Arrange
        var model = CreateModel();

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                model.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        // Act
        var result = await _sut.ExecuteAsync(model.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(model);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                model.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRecordDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomOperatingHourModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Room operating hour not found.");

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // ID normalization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldTrimId_BeforeCallingRepository()
    {
        // Arrange
        var model = CreateModel();

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                model.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        // Act
        var result = await _sut.ExecuteAsync(
            $"  {model.Id}  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(model);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                model.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepository()
    {
        // Arrange
        var model = CreateModel();

        using var cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = cancellationTokenSource.Token;

        _repositoryMock
            .Setup(x => x.GetByIdAsync(
                model.Id,
                cancellationToken))
            .ReturnsAsync(model);

        // Act
        var result = await _sut.ExecuteAsync(
            model.Id,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(model);

        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                model.Id,
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

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static RoomOperatingHourModel CreateModel()
    {
        return new RoomOperatingHourModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = Guid.NewGuid().ToString(),
            DayOfWeek = DayOfWeek.Monday,
            OpensAt = new TimeOnly(8, 0),
            ClosesAt = new TimeOnly(12, 0),
            IsClosed = false,
            IsActive = true,
            Notes = "Morning operating hours.",
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private void VerifyNoRepositoryCalls()
    {
        _repositoryMock.Verify(
            x => x.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}