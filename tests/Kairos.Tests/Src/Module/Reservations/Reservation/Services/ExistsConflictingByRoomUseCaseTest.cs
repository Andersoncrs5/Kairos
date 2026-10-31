using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Reservations.Reservation.Services;

public class ExistsConflictingByRoomUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReservationRepository> _reservationRepositoryMock;
    private readonly ExistsConflictingByRoomUseCase _sut;

    public ExistsConflictingByRoomUseCaseTest()
    {
        _reservationRepositoryMock = new Mock<IReservationRepository>();

        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock
            .Setup(x => x.ReservationRepository)
            .Returns(_reservationRepositoryMock.Object);

        _sut = new ExistsConflictingByRoomUseCase(
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConflictExists_ReturnsTrue()
    {
        // Arrange
        const string roomId = "room-123";

        DateTimeOffset startsAt = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset endsAt = startsAt.AddHours(1);

        _reservationRepositoryMock
            .Setup(x => x.ExistsConflictingByRoomAsync(
                roomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.False(result.IsFailure);
        Assert.True(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                roomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoConflictExists_ReturnsFalse()
    {
        // Arrange
        const string roomId = "room-123";

        DateTimeOffset startsAt = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset endsAt = startsAt.AddHours(1);

        _reservationRepositoryMock
            .Setup(x => x.ExistsConflictingByRoomAsync(
                roomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.False(result.IsFailure);
        Assert.False(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                roomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenRoomIdIsEmpty_ReturnsValidationFailure(
        string roomId)
    {
        // Arrange
        DateTimeOffset startsAt = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset endsAt = startsAt.AddHours(1);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.True(result.IsFailure);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_WhenPeriodIsInvalid_ReturnsValidationFailure(
        int endOffsetMinutes)
    {
        // Arrange
        const string roomId = "room-123";

        DateTimeOffset startsAt = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset endsAt = startsAt.AddMinutes(endOffsetMinutes);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.True(result.IsFailure);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdContainsWhitespace_TrimsRoomId()
    {
        // Arrange
        const string roomId = " room-123 ";
        const string expectedRoomId = "room-123";

        DateTimeOffset startsAt = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset endsAt = startsAt.AddHours(1);

        _reservationRepositoryMock
            .Setup(x => x.ExistsConflictingByRoomAsync(
                expectedRoomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.False(result.IsFailure);
        Assert.False(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                expectedRoomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DateTimeOffset startsAt = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset endsAt = startsAt.AddHours(1);

        // Act
        Task act() => _sut.ExecuteAsync(
            "room-123",
            startsAt,
            endsAt,
            cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}