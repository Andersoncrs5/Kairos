using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Reservations.Reservation.Services;

public sealed class GetConflictingByRoomUseCaseTest
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

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenConflictsExist_ReturnsReservations()
    {
        // Arrange
        ReservationRoomConflictCommand command = NewCommand();

        IReadOnlyList<ReservationModel> reservations =
        [
            NewReservation(
                command.RoomId,
                command.StartsAt,
                command.EndsAt)
        ];

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservations);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsFailure);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value!);
        Assert.Same(reservations[0], result.Value![0]);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoConflictsExist_ReturnsEmptyList()
    {
        // Arrange
        ReservationRoomConflictCommand command = NewCommand();

        IReadOnlyList<ReservationModel> reservations =
            Array.Empty<ReservationModel>();

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservations);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsFailure);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
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

        IReadOnlyList<ReservationModel> reservations =
            Array.Empty<ReservationModel>();

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                expectedRoomId,
                command.StartsAt,
                command.EndsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservations);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsFailure);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
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
            .Setup(x => x.GetConflictingByRoomAsync(
                command.RoomId,
                command.StartsAt,
                command.EndsAt,
                cts.Token))
            .ReturnsAsync(Array.Empty<ReservationModel>());

        // Act
        var result = await _sut.ExecuteAsync(
            command,
            cts.Token);

        // Assert
        Assert.False(result.IsFailure);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
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
            x => x.GetConflictingByRoomAsync(
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
            x => x.GetConflictingByRoomAsync(
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
        DateTimeOffset start = startsAt ?? new(
            2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

        return new ReservationRoomConflictCommand(
            roomId,
            start,
            endsAt ?? start.AddHours(1));
    }

    private static ReservationModel NewReservation(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        return new ReservationModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            UserId = "user-123",
            CategoryId = "category-123",
            Title = "Team Meeting",
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}