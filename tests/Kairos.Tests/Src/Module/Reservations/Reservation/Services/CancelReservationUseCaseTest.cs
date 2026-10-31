using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reservations.Reservation.Services;

public sealed class CancelReservationUseCaseTest
{
    private static readonly string ReservationId =
        Guid.NewGuid().ToString();

    private static readonly string UserId =
        Guid.NewGuid().ToString();

    private static readonly DateTimeOffset StartsAt =
        new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset EndsAt =
        new(2026, 11, 10, 11, 0, 0, TimeSpan.Zero);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IReservationRepository _reservationRepository;

    private readonly CancelReservationUseCase _sut;

    public CancelReservationUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _reservationRepository =
            Substitute.For<IReservationRepository>();

        _unitOfWork.ReservationRepository
            .Returns(_reservationRepository);

        _sut = new CancelReservationUseCase(_unitOfWork);
    }

    // ---------------------------------------------------------------
    // 1. Command validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ReturnsValidationFailure()
    {
        var result = await _sut.ExecuteAsync(null!);

        result.IsFailure.Should().BeTrue();

        await _reservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        var command = NewCommand(dto: null!);

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationIdIsInvalid_ReturnsValidationFailure()
    {
        var command = NewCommand(
            reservationId: "invalid-reservation-id");

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _reservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsEmpty_ReturnsValidationFailure()
    {
        var command = NewCommand(userId: string.Empty);

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _reservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsInvalid_ReturnsValidationFailure()
    {
        var command = NewCommand(userId: "invalid-user-id");

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _reservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 2. Reservation lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        _reservationRepository
            .GetByIdAsync(
                ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(null));

        var result = await _sut.ExecuteAsync(NewCommand());

        result.IsFailure.Should().BeTrue();

        await _reservationRepository
            .Received(1)
            .GetByIdAsync(
                ReservationId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationExists_FetchesReservationById()
    {
        ReservationModel reservation = ArrangeReservation();

        var result = await _sut.ExecuteAsync(NewCommand());

        result.IsSuccess.Should().BeTrue();

        await _reservationRepository
            .Received(1)
            .GetByIdAsync(
                reservation.Id,
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 3. Reservation status validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationIsAlreadyCancelled_ReturnsConflict()
    {
        ReservationModel reservation = ArrangeReservation(
            status: ReservationStatusEnum.Cancelled);

        DateTimeOffset? originalCancelledAt = reservation.CancelledAt;
        string? originalCancelledBy = reservation.CancelledBy;
        string? originalReason = reservation.CancellationReason;

        var result = await _sut.ExecuteAsync(
            NewCommand(reason: "Another cancellation"));

        result.IsFailure.Should().BeTrue();

        reservation.Status.Should().Be(ReservationStatusEnum.Cancelled);
        reservation.CancelledAt.Should().Be(originalCancelledAt);
        reservation.CancelledBy.Should().Be(originalCancelledBy);
        reservation.CancellationReason.Should().Be(originalReason);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationIsConfirmed_AllowsCancellation()
    {
        ReservationModel reservation = ArrangeReservation(
            status: ReservationStatusEnum.Confirmed);

        var result = await _sut.ExecuteAsync(NewCommand());

        result.IsSuccess.Should().BeTrue();

        reservation.Status.Should().Be(ReservationStatusEnum.Cancelled);
    }

    // ---------------------------------------------------------------
    // 4. Cancellation reason
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReasonIsProvided_TrimsCancellationReason()
    {
        ReservationModel reservation = ArrangeReservation();

        var command = NewCommand(
            reason: "  Meeting postponed  ");

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        result.Value!.CancellationReason
            .Should()
            .Be("Meeting postponed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenReasonIsNull_SetsCancellationReasonToNull()
    {
        ReservationModel reservation = ArrangeReservation();

        reservation.CancellationReason = "Previous reason";

        var result = await _sut.ExecuteAsync(
            NewCommand(reason: null));

        result.IsSuccess.Should().BeTrue();

        result.Value!.CancellationReason.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReasonIsWhitespace_SetsCancellationReasonToNull()
    {
        ReservationModel reservation = ArrangeReservation();

        var result = await _sut.ExecuteAsync(
            NewCommand(reason: "    "));

        result.IsSuccess.Should().BeTrue();

        result.Value!.CancellationReason.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // 5. Cancellation fields
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationSucceeds_UpdatesCancellationFields()
    {
        ReservationModel reservation = ArrangeReservation();

        var command = NewCommand(
            reason: "Room unavailable");

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        reservation.Status.Should().Be(ReservationStatusEnum.Cancelled);
        reservation.CancelledAt.Should().NotBeNull();
        reservation.CancelledBy.Should().Be(UserId);
        reservation.CancellationReason.Should().Be("Room unavailable");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationSucceeds_UsesUtcTimestamp()
    {
        ReservationModel reservation = ArrangeReservation();

        DateTimeOffset beforeExecution = DateTimeOffset.UtcNow;

        var result = await _sut.ExecuteAsync(NewCommand());

        DateTimeOffset afterExecution = DateTimeOffset.UtcNow;

        result.IsSuccess.Should().BeTrue();

        reservation.CancelledAt.Should().NotBeNull();

        reservation.CancelledAt!.Value
            .Should()
            .BeOnOrAfter(beforeExecution);

        reservation.CancelledAt!.Value
            .Should()
            .BeOnOrBefore(afterExecution);

        reservation.CancelledAt!.Value.Offset
            .Should()
            .Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsValidButNotNormalized_NormalizesCancelledBy()
    {
        string userId = Guid.NewGuid().ToString("D").ToUpperInvariant();

        ReservationModel reservation = ArrangeReservation();

        var command = NewCommand(userId: userId);

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        reservation.CancelledBy.Should().Be(
            Guid.Parse(userId).ToString());
    }

    // ---------------------------------------------------------------
    // 6. Audit fields
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationSucceeds_IncrementsVersion()
    {
        ReservationModel reservation = ArrangeReservation();

        long previousVersion = reservation.Version;

        var result = await _sut.ExecuteAsync(NewCommand());

        result.IsSuccess.Should().BeTrue();

        reservation.Version.Should().Be(previousVersion + 1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationSucceeds_UpdatesUpdatedAt()
    {
        ReservationModel reservation = ArrangeReservation();

        DateTimeOffset previousUpdatedAt =
            new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

        reservation.UpdatedAt = previousUpdatedAt;

        var result = await _sut.ExecuteAsync(NewCommand());

        result.IsSuccess.Should().BeTrue();

        reservation.UpdatedAt.Should().NotBeNull();

    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationSucceeds_UsesSameTimestampForAuditAndCancellation()
    {
        ReservationModel reservation = ArrangeReservation();

        var result = await _sut.ExecuteAsync(NewCommand());

        result.IsSuccess.Should().BeTrue();

        reservation.CancelledAt.Should().Be(reservation.UpdatedAt);
    }

    // ---------------------------------------------------------------
    // 7. Entity preservation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationSucceeds_PreservesReservationData()
    {
        ReservationModel reservation = ArrangeReservation();

        string originalRoomId = reservation.RoomId;
        string originalUserId = reservation.UserId;
        string originalCategoryId = reservation.CategoryId;
        string originalTitle = reservation.Title;
        string? originalDescription = reservation.Description;
        DateTimeOffset originalStartsAt = reservation.StartsAt;
        DateTimeOffset originalEndsAt = reservation.EndsAt;

        var result = await _sut.ExecuteAsync(
            NewCommand(reason: "Cancelled by user"));

        result.IsSuccess.Should().BeTrue();

        reservation.RoomId.Should().Be(originalRoomId);
        reservation.UserId.Should().Be(originalUserId);
        reservation.CategoryId.Should().Be(originalCategoryId);
        reservation.Title.Should().Be(originalTitle);
        reservation.Description.Should().Be(originalDescription);
        reservation.StartsAt.Should().Be(originalStartsAt);
        reservation.EndsAt.Should().Be(originalEndsAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationSucceeds_ReturnsSameReservationInstance()
    {
        ReservationModel reservation = ArrangeReservation();

        var result = await _sut.ExecuteAsync(NewCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(reservation);
    }

    // ---------------------------------------------------------------
    // 8. Cancellation token
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationTokenIsCancelled_ThrowsOperationCanceledException()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        var act = async () => await _sut.ExecuteAsync(
            NewCommand(),
            cancellationTokenSource.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _reservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private ReservationModel ArrangeReservation(
        ReservationStatusEnum status = ReservationStatusEnum.Confirmed)
    {
        ReservationModel reservation = NewReservation(status);

        _reservationRepository
            .GetByIdAsync(
                reservation.Id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(reservation));

        return reservation;
    }

    private static CancelReservationCommand NewCommand(
        string? reservationId = null,
        string? userId = null,
        string? reason = null,
        CancelReservationDto? dto = null)
    {
        return new CancelReservationCommand(
            reservationId ?? ReservationId,
            userId ?? UserId,
            dto ?? new CancelReservationDto
            {
                CancellationReason = reason
            });
    }

    private static ReservationModel NewReservation(
        ReservationStatusEnum status)
    {
        return new ReservationModel
        {
            Id = ReservationId,
            RoomId = Guid.NewGuid().ToString(),
            UserId = UserId,
            CategoryId = Guid.NewGuid().ToString(),
            Title = "Architecture meeting",
            Description = "Meeting about system architecture",
            StartsAt = StartsAt,
            EndsAt = EndsAt,
            Status = status,
            Version = 3,
            CreatedAt = StartsAt.AddDays(-5),
            UpdatedAt = null,
            CancelledAt = null,
            CancelledBy = null,
            CancellationReason = null
        };
    }
}