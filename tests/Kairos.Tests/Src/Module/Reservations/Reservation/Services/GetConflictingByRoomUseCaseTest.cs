using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Reservations.Reservation.Services;

public class GetConflictingByRoomUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReservationRepository> _reservationRepositoryMock;
    private readonly GetConflictingByRoomUseCase _sut;

    public GetConflictingByRoomUseCaseTest()
    {
        _reservationRepositoryMock = new Mock<IReservationRepository>();

        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock
            .Setup(x => x.ReservationRepository)
            .Returns(_reservationRepositoryMock.Object);

        _sut = new GetConflictingByRoomUseCase(
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConflictsExist_ReturnsReservations()
    {
        // Arrange
        const string roomId = "room-123";

        DateTimeOffset startsAt =
            new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset endsAt = startsAt.AddHours(1);

        IReadOnlyList<ReservationModel> reservations =
        [
            new()
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = roomId,
                UserId = "user-123",
                CategoryId = "category-123",
                Title = "Team Meeting",
                StartsAt = startsAt,
                EndsAt = endsAt,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        ];

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                roomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservations);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.False(result.IsFailure);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
        Assert.Same(reservations[0], result.Value[0]);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                roomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoConflictsExist_ReturnsEmptyList()
    {
        // Arrange
        const string roomId = "room-123";

        DateTimeOffset startsAt =
            new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset endsAt = startsAt.AddHours(1);

        IReadOnlyList<ReservationModel> reservations =
            Array.Empty<ReservationModel>();

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                roomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservations);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.False(result.IsFailure);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
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
        DateTimeOffset startsAt =
            new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset endsAt = startsAt.AddHours(1);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.True(result.IsFailure);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
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

        DateTimeOffset startsAt =
            new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset endsAt = startsAt.AddMinutes(endOffsetMinutes);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.True(result.IsFailure);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
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

        DateTimeOffset startsAt =
            new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset endsAt = startsAt.AddHours(1);

        IReadOnlyList<ReservationModel> reservations =
            Array.Empty<ReservationModel>();

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                expectedRoomId,
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservations);

        // Act
        var result = await _sut.ExecuteAsync(
            roomId,
            startsAt,
            endsAt);

        // Assert
        Assert.False(result.IsFailure);
        Assert.Empty(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
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
        cts.Cancel();

        DateTimeOffset startsAt =
            new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

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
            x => x.GetConflictingByRoomAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}