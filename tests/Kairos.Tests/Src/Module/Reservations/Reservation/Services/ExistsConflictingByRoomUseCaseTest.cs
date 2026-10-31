using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Reservations.Reservation.Services;

public sealed class ExistsConflictingByRoomUseCaseTest
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

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenConflictExists_ReturnsTrue()
    {
        // Arrange
        ReservationRoomConflictCommand command = NewCommand();

        _reservationRepositoryMock
            .Setup(x => x.ExistsConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsFailure);
        Assert.True(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoConflictExists_ReturnsFalse()
    {
        // Arrange
        ReservationRoomConflictCommand command = NewCommand();

        _reservationRepositoryMock
            .Setup(x => x.ExistsConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsFailure);
        Assert.False(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdContainsWhitespace_TrimsRoomId()
    {
        // Arrange
        const string roomId = " room-123 ";
        const string expectedRoomId = "room-123";

        ReservationRoomConflictCommand command = NewCommand(roomId);

        _reservationRepositoryMock
            .Setup(x => x.ExistsConflictingByRoomAsync(
                expectedRoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsFailure);
        Assert.False(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                expectedRoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationTokenIsProvided_PassesItToRepository()
    {
        // Arrange
        using CancellationTokenSource cts = new();

        ReservationRoomConflictCommand command = NewCommand();

        _reservationRepositoryMock
            .Setup(x => x.ExistsConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                cts.Token))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(command, cts.Token);

        // Assert
        Assert.False(result.IsFailure);

        _reservationRepositoryMock.Verify(
            x => x.ExistsConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                cts.Token),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ReturnsValidationFailure()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

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
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenRoomIdIsEmpty_ReturnsValidationFailure(
        string roomId)
    {
        // Arrange
        ReservationRoomConflictCommand command = NewCommand(roomId);

        // Act
        var result = await _sut.ExecuteAsync(command);

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
        DateTimeOffset startsAt = new(
            2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset endsAt = startsAt.AddMinutes(endOffsetMinutes);

        ReservationRoomConflictCommand command = NewCommand(
            startsAt: startsAt,
            endsAt: endsAt);

        // Act
        var result = await _sut.ExecuteAsync(command);

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

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static ReservationRoomConflictCommand NewCommand(
        string roomId = "room-123",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        DateTimeOffset start = startsAt ?? new DateTimeOffset(
            2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

        return new ReservationRoomConflictCommand(
            roomId,
            start,
            endsAt ?? start.AddHours(1));
    }
}