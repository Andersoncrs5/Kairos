using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Rooms.RoomOperatingHour.Services;

public sealed class FindRoomOperatingHoursByRoomIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRoomRepository> _roomRepositoryMock = new();
    private readonly Mock<IRoomOperatingHourRepository> _repositoryMock = new();

    private readonly FindRoomOperatingHoursByRoomIdUseCase _sut;

    public FindRoomOperatingHoursByRoomIdUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.RoomRepository)
            .Returns(_roomRepositoryMock.Object);

        _unitOfWorkMock
            .SetupGet(x => x.RoomOperatingHourRepository)
            .Returns(_repositoryMock.Object);

        _sut = new FindRoomOperatingHoursByRoomIdUseCase(
            _unitOfWorkMock.Object);
    }

    // ---------------------------------------------------------------
    // Room ID validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Room ID is required.");

        VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsEmpty(
        string roomId)
    {
        // Act
        var result = await _sut.ExecuteAsync(roomId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Room ID is required.");

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Room existence
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        _roomRepositoryMock
            .Setup(x => x.ExistsAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(roomId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain("Room doesn't exist.");

        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            x => x.GetByRoomIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Find operating hours
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOperatingHours_WhenRoomExists()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        var expected = new List<RoomOperatingHourModel>
        {
            CreateModel(
                roomId,
                DayOfWeek.Monday,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0)),

            CreateModel(
                roomId,
                DayOfWeek.Monday,
                new TimeOnly(13, 0),
                new TimeOnly(18, 0)),

            CreateModel(
                roomId,
                DayOfWeek.Tuesday,
                new TimeOnly(8, 0),
                new TimeOnly(17, 0))
        };

        SetupRoomExists(roomId);

        _repositoryMock
            .Setup(x => x.GetByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoomOperatingHourModel>)expected);

        // Act
        var result = await _sut.ExecuteAsync(roomId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(expected);
        result.Value.Should().HaveCount(3);

        _repositoryMock.Verify(
            x => x.GetByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenRoomHasNoOperatingHours()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId);

        IReadOnlyList<RoomOperatingHourModel> expected =
            Array.Empty<RoomOperatingHourModel>();

        _repositoryMock
            .Setup(x => x.GetByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(roomId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveInactiveAndClosedHours_WhenReturningResults()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        var expected = new List<RoomOperatingHourModel>
        {
            CreateModel(
                roomId,
                DayOfWeek.Sunday,
                TimeOnly.MinValue,
                TimeOnly.MinValue,
                isClosed: true),

            CreateModel(
                roomId,
                DayOfWeek.Monday,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0),
                isActive: false)
        };

        SetupRoomExists(roomId);

        _repositoryMock
            .Setup(x => x.GetByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoomOperatingHourModel>)expected);

        // Act
        var result = await _sut.ExecuteAsync(roomId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(x => x.IsClosed);
        result.Value.Should().Contain(x => !x.IsActive);
    }

    // ---------------------------------------------------------------
    // Room ID normalization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldTrimRoomId_BeforeCallingRepositories()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        SetupRoomExists(roomId);

        _repositoryMock
            .Setup(x => x.GetByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (IReadOnlyList<RoomOperatingHourModel>)
                Array.Empty<RoomOperatingHourModel>());

        // Act
        var result = await _sut.ExecuteAsync($"  {roomId}  ");

        // Assert
        result.IsSuccess.Should().BeTrue();

        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            x => x.GetByRoomIdAsync(
                roomId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepositories()
    {
        // Arrange
        var roomId = Guid.NewGuid().ToString();

        using var cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = cancellationTokenSource.Token;

        _roomRepositoryMock
            .Setup(x => x.ExistsAsync(roomId, cancellationToken))
            .ReturnsAsync(true);

        _repositoryMock
            .Setup(x => x.GetByRoomIdAsync(roomId, cancellationToken))
            .ReturnsAsync(
                (IReadOnlyList<RoomOperatingHourModel>)
                Array.Empty<RoomOperatingHourModel>());

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(roomId, cancellationToken),
            Times.Once);

        _repositoryMock.Verify(
            x => x.GetByRoomIdAsync(roomId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCancelled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        var roomId = Guid.NewGuid().ToString();

        // Act
        var act = () => _sut.ExecuteAsync(
            roomId,
            cancellationTokenSource.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        VerifyNoRepositoryCalls();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static RoomOperatingHourModel CreateModel(
        string roomId,
        DayOfWeek dayOfWeek,
        TimeOnly opensAt,
        TimeOnly closesAt,
        bool isClosed = false,
        bool isActive = true)
    {
        return new RoomOperatingHourModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            DayOfWeek = dayOfWeek,
            OpensAt = opensAt,
            ClosesAt = closesAt,
            IsClosed = isClosed,
            IsActive = isActive,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private void SetupRoomExists(string roomId)
    {
        _roomRepositoryMock
            .Setup(x => x.ExistsAsync(
                roomId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(true));
    }

    private void VerifyNoRepositoryCalls()
    {
        _roomRepositoryMock.Verify(
            x => x.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _repositoryMock.Verify(
            x => x.GetByRoomIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}