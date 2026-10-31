using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Reservations.Reservation.Services.Providers;

public sealed class GetConflictingByRoomUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();

    private readonly GetConflictingByRoomUseCase _sut;

    public GetConflictingByRoomUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(x => x.ReservationRepository)
            .Returns(_reservationRepositoryMock.Object);

        _sut = new GetConflictingByRoomUseCase(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Reservation room conflict command is required.");

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsInvalid(
        string? roomId)
    {
        // Arrange
        var command = new ReservationRoomConflictCommand(
            roomId!,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Room ID is required.");

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenStartDateIsEqualToEndDate()
    {
        // Arrange
        var date = DateTimeOffset.UtcNow;

        var command = new ReservationRoomConflictCommand(
            "room-123",
            date,
            date);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Reservation start date must be earlier than end date.");

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenStartDateIsAfterEndDate()
    {
        // Arrange
        var command = new ReservationRoomConflictCommand(
            "room-123",
            DateTimeOffset.UtcNow.AddHours(2),
            DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Reservation start date must be earlier than end date.");

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnReservations_WhenConflictsExist()
    {
        // Arrange
        var startsAt = DateTimeOffset.UtcNow.AddHours(1);
        var endsAt = startsAt.AddHours(2);

        var command = new ReservationRoomConflictCommand(
            "room-123",
            startsAt,
            endsAt);

        IReadOnlyList<ReservationModel> expected =
            Array.Empty<ReservationModel>();

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                "room-123",
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expected);

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                "room-123",
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenNoConflictsExist()
    {
        // Arrange
        var startsAt = DateTimeOffset.UtcNow.AddHours(1);
        var endsAt = startsAt.AddHours(2);

        var command = new ReservationRoomConflictCommand(
            "room-123",
            startsAt,
            endsAt);

        IReadOnlyList<ReservationModel> expected =
            Array.Empty<ReservationModel>();

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                "room-123",
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTrimRoomId_BeforeCallingRepository()
    {
        // Arrange
        var startsAt = DateTimeOffset.UtcNow.AddHours(1);
        var endsAt = startsAt.AddHours(2);

        var command = new ReservationRoomConflictCommand(
            "  room-123  ",
            startsAt,
            endsAt);

        IReadOnlyList<ReservationModel> expected =
            Array.Empty<ReservationModel>();

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                "room-123",
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                "room-123",
                startsAt,
                endsAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_ToRepository()
    {
        // Arrange
        var startsAt = DateTimeOffset.UtcNow.AddHours(1);
        var endsAt = startsAt.AddHours(2);

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var command = new ReservationRoomConflictCommand(
            "room-123",
            startsAt,
            endsAt);

        _reservationRepositoryMock
            .Setup(x => x.GetConflictingByRoomAsync(
                "room-123",
                startsAt,
                endsAt,
                cancellationToken))
            .ReturnsAsync(Array.Empty<ReservationModel>());

        // Act
        var result = await _sut.ExecuteAsync(command, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                "room-123",
                startsAt,
                endsAt,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCancelled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var command = new ReservationRoomConflictCommand(
            "room-123",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var act = () => _sut.ExecuteAsync(
            command,
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _reservationRepositoryMock.Verify(
            x => x.GetConflictingByRoomAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}