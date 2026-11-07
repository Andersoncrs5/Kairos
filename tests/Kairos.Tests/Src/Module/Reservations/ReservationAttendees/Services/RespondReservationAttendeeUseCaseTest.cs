
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;
using Kairos.Api.Modules.Rooms.Room.Models;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reservations.ReservationAttendees.Services;

public sealed class RespondReservationAttendeeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly RespondReservationAttendeeUseCase _sut;

    public RespondReservationAttendeeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new RespondReservationAttendeeUseCase(_unitOfWork);
    }

    // ---------------------------------------------------------------
    // 1. Command validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ReturnsValidationFailure()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Response command is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = new RespondReservationAttendeeCommand(
            ReservationId: Guid.NewGuid().ToString(),
            UserId: Guid.NewGuid().ToString(),
            Dto: null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().NotBeEmpty();

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            reservationId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Reservation ID is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            userId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Attendee user ID is required.");

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // 2. DTO validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenStatusIsPending_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Pending);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Attendee status must be Accepted, Declined or Tentative.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStatusIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            status: (AttendeeStatusEnum)999);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Attendee status is invalid.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseNoteExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            responseNote: new string('A', 256));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Response note cannot exceed 255 characters.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseNoteHasMaximumLength_AcceptsValidation()
    {
        // Arrange
        var command = NewCommand(
            responseNote: new string('A', 255));

        SetupAttendee(
            command,
            NewAttendee(command));

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // 3. Identifier validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            reservationId: "invalid-reservation-id");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Reservation ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            userId: "invalid-user-id");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Attendee user ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // 4. Invitation lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenInvitationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        SetupAttendee(command, attendee: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Invitation not found.");

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .GetByReservationAndUserAsync(
                command.ReservationId,
                command.UserId,
                Arg.Any<CancellationToken>());

        await VerifyReservationLookupsWereNotCalled();
        await VerifyRoomLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 5. Reservation lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted);

        SetupAttendee(command, attendee);
        SetupNormalReservation(command, reservation: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Reservation not found.");

        await _unitOfWork.ReservationRepository
            .Received(1)
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .FindByIdWithForUpdateAsync(
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>());

        await VerifyRoomLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeclinedAttendeeReservationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Declined);

        SetupAttendee(command, attendee);
        SetupLockedReservation(command, reservation: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Reservation not found.");

        await _unitOfWork.ReservationRepository
            .Received(1)
            .FindByIdWithForUpdateAsync(
                command.ReservationId,
                cancellationToken: Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await VerifyRoomLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 6. Reservation status validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationIsCancelled_ReturnsFailure()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Pending);

        var reservation = NewReservation(
            command.ReservationId,
            status: ReservationStatusEnum.Cancelled);

        SetupAttendee(command, attendee);
        SetupNormalReservation(command, reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().NotBeEmpty();

        await VerifyRoomLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationHasEnded_ReturnsFailure()
    {
        // Arrange
        var command = NewCommand();

        var now = DateTimeOffset.UtcNow;

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Pending);

        var reservation = NewReservation(
            command.ReservationId,
            startsAt: now.AddHours(-3),
            endsAt: now.AddHours(-1));

        SetupAttendee(command, attendee);
        SetupNormalReservation(command, reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().NotBeEmpty();

        await VerifyRoomLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 7. Idempotency
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenStatusAndResponseNoteAreUnchanged_ReturnsExistingAttendee()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted,
            responseNote: "  Confirmed  ");

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted,
            responseNote: "Confirmed");

        SetupAttendee(command, attendee);

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        attendee.ResponseNote.Should().Be("Confirmed");
        attendee.RespondedAt.Should().BeNull();
        attendee.UpdatedAt.Should().BeNull();

        await VerifyRoomLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStatusIsUnchangedButResponseNoteChanges_UpdatesAttendee()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted,
            responseNote: "Updated note");

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted,
            responseNote: "Old note");

        SetupAttendee(command, attendee);

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(attendee);

        attendee.ResponseNote.Should().Be("Updated note");
        attendee.RespondedAt.Should().NotBeNull();
        attendee.UpdatedAt.Should().NotBeNull();

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseNoteIsWhitespace_NormalizesNoteToNull()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted,
            responseNote: "   ");

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted,
            responseNote: "Previous note");

        SetupAttendee(command, attendee);

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        attendee.ResponseNote.Should().BeNull();

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);
    }

    // ---------------------------------------------------------------
    // 8. Reoccupying a seat
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(AttendeeStatusEnum.Accepted)]
    [InlineData(AttendeeStatusEnum.Tentative)]
    public async Task ExecuteAsync_WhenDeclinedAttendeeChangesToOccupyingStatus_LocksReservation(
        AttendeeStatusEnum newStatus)
    {
        // Arrange
        var command = NewCommand(status: newStatus);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Declined);

        var reservation = NewReservation(
            command.ReservationId);

        var room = NewRoom(reservation.RoomId);

        SetupAttendee(command, attendee);
        SetupLockedReservation(command, reservation);
        SetupRoom(reservation.RoomId, room);
        SetupOccupyingCount(command, occupying: 1);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        attendee.Status.Should().Be(newStatus);

        await _unitOfWork.ReservationRepository
            .Received(1)
            .FindByIdWithForUpdateAsync(
                command.ReservationId,
                cancellationToken: Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork.RoomRepository
            .Received(1)
            .GetByIdAsync(
                reservation.RoomId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .CountOccupyingByReservationAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>());

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeclinedAttendeeRemainsDeclined_DoesNotLockReservation()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Declined,
            responseNote: "Still unavailable");

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Declined,
            responseNote: "Previous note");

        SetupAttendee(command, attendee);

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        attendee.Status.Should().Be(AttendeeStatusEnum.Declined);
        attendee.ResponseNote.Should().Be("Still unavailable");

        await _unitOfWork.ReservationRepository
            .Received(1)
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .FindByIdWithForUpdateAsync(
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>());

        await VerifyRoomLookupWasNotCalled();
        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeclinedAttendeeChangesToDeclinedWithSameNote_IsIdempotent()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Declined,
            responseNote: "  Cannot attend  ");

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Declined,
            responseNote: "Cannot attend");

        SetupAttendee(command, attendee);

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(attendee);

        await VerifyRoomLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 9. Room capacity validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Declined);

        var reservation = NewReservation(
            command.ReservationId);

        SetupAttendee(command, attendee);
        SetupLockedReservation(command, reservation);
        SetupRoom(reservation.RoomId, room: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Room doesn't exist.");

        await _unitOfWork.RoomRepository
            .Received(1)
            .GetByIdAsync(
                reservation.RoomId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .CountOccupyingByReservationAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomCapacityIsReached_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Declined);

        var reservation = NewReservation(
            command.ReservationId);

        var room = NewRoom(
            reservation.RoomId,
            capacity: 2);

        SetupAttendee(command, attendee);
        SetupLockedReservation(command, reservation);
        SetupRoom(reservation.RoomId, room);
        SetupOccupyingCount(command, occupying: 2);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().NotBeEmpty();

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .CountOccupyingByReservationAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 10. No capacity validation when seat is not reoccupied
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAcceptedAttendeeDeclines_DoesNotCheckCapacity()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Declined);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted);

        SetupAttendee(command, attendee);

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        attendee.Status.Should().Be(AttendeeStatusEnum.Declined);

        await _unitOfWork.ReservationRepository
            .Received(1)
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .FindByIdWithForUpdateAsync(
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>());

        await VerifyRoomLookupWasNotCalled();

        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .CountOccupyingByReservationAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);
    }

    // ---------------------------------------------------------------
    // 11. Successful response
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_UpdatesAttendeeResponse()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted,
            responseNote: "  I will attend  ");

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Pending);

        SetupAttendee(command, attendee);

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(command);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        attendee.Status.Should().Be(AttendeeStatusEnum.Accepted);
        attendee.ResponseNote.Should().Be("I will attend");

        attendee.RespondedAt.Should().NotBeNull();
        attendee.UpdatedAt.Should().NotBeNull();

        attendee.RespondedAt.Should().BeOnOrAfter(beforeExecution);
        attendee.RespondedAt.Should().BeOnOrBefore(afterExecution);

        attendee.UpdatedAt.Should().BeOnOrAfter(beforeExecution);
        attendee.UpdatedAt.Should().BeOnOrBefore(afterExecution);

        // This UseCase does not increment Version.
        attendee.Version.Should().Be(1);

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(
                Arg.Is<ReservationAttendeeModel>(entity =>
                    entity.Id == attendee.Id &&
                    entity.Status == AttendeeStatusEnum.Accepted &&
                    entity.ResponseNote == "I will attend" &&
                    entity.RespondedAt != null &&
                    entity.UpdatedAt != null));
    }

    [Theory]
    [InlineData(AttendeeStatusEnum.Accepted)]
    [InlineData(AttendeeStatusEnum.Declined)]
    [InlineData(AttendeeStatusEnum.Tentative)]
    public async Task ExecuteAsync_WhenResponseStatusIsValid_UpdatesAttendeeStatus(
        AttendeeStatusEnum status)
    {
        // Arrange
        var command = NewCommand(status: status);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Pending);

        SetupAttendee(command, attendee);

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        attendee.Status.Should().Be(status);

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);
    }

    // ---------------------------------------------------------------
    // 12. Exception handling
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        _unitOfWork.ReservationAttendeeRepository
            .GetByReservationAndUserAsync(
                command.ReservationId,
                command.UserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReservationAttendeeModel?>(
                new InvalidOperationException(
                    "Invitation lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Invitation lookup failed.");

        await VerifyReservationLookupsWereNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted);

        SetupAttendee(command, attendee);

        _unitOfWork.ReservationRepository
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReservationModel?>(
                new InvalidOperationException(
                    "Reservation lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Reservation lookup failed.");

        await VerifyRoomLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLockedReservationLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Declined);

        SetupAttendee(command, attendee);

        _unitOfWork.ReservationRepository
            .FindByIdWithForUpdateAsync(
                command.ReservationId,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReservationModel?>(
                new InvalidOperationException(
                    "Locked reservation lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Locked reservation lookup failed.");

        await VerifyRoomLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Declined);

        var reservation = NewReservation(command.ReservationId);

        SetupAttendee(command, attendee);
        SetupLockedReservation(command, reservation);

        _unitOfWork.RoomRepository
            .GetByIdAsync(
                reservation.RoomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<RoomModel?>(
                new InvalidOperationException(
                    "Room lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Room lookup failed.");

        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCapacityLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Declined);

        var reservation = NewReservation(command.ReservationId);
        var room = NewRoom(reservation.RoomId);

        SetupAttendee(command, attendee);
        SetupLockedReservation(command, reservation);
        SetupRoom(reservation.RoomId, room);

        _unitOfWork.ReservationAttendeeRepository
            .CountOccupyingByReservationAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(
                new InvalidOperationException(
                    "Capacity lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Capacity lookup failed.");

        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUpdateFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand(
            status: AttendeeStatusEnum.Accepted);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Pending);

        SetupAttendee(command, attendee);

        SetupNormalReservation(
            command,
            NewReservation(command.ReservationId));

        var exception = new InvalidOperationException(
            "Attendee update failed.");

        _unitOfWork.ReservationAttendeeRepository
            .When(repository => repository.Update(attendee))
            .Do(_ => throw exception);

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Attendee update failed.");

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);
    }

    // ---------------------------------------------------------------
    // 13. CancellationToken
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        cts.Cancel();

        var command = NewCommand();

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // Helpers: repository setup
    // ---------------------------------------------------------------

    private void SetupAttendee(
        RespondReservationAttendeeCommand command,
        ReservationAttendeeModel? attendee)
    {
        _unitOfWork.ReservationAttendeeRepository
            .GetByReservationAndUserAsync(
                command.ReservationId,
                command.UserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationAttendeeModel?>(attendee));
    }

    private void SetupNormalReservation(
        RespondReservationAttendeeCommand command,
        ReservationModel? reservation)
    {
        _unitOfWork.ReservationRepository
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(reservation));
    }

    private void SetupLockedReservation(
        RespondReservationAttendeeCommand command,
        ReservationModel? reservation)
    {
        _unitOfWork.ReservationRepository
            .FindByIdWithForUpdateAsync(
                command.ReservationId,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(reservation));
    }

    private void SetupRoom(
        string roomId,
        RoomModel? room)
    {
        _unitOfWork.RoomRepository
            .GetByIdAsync(
                roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));
    }

    private void SetupOccupyingCount(
        RespondReservationAttendeeCommand command,
        int occupying)
    {
        _unitOfWork.ReservationAttendeeRepository
            .CountOccupyingByReservationAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(occupying));
    }

    // ---------------------------------------------------------------
    // Helpers: commands and entities
    // ---------------------------------------------------------------

    private static RespondReservationAttendeeCommand NewCommand(
        string? reservationId = null,
        string? userId = null,
        AttendeeStatusEnum status = AttendeeStatusEnum.Accepted,
        string? responseNote = null)
    {
        return new RespondReservationAttendeeCommand(
            ReservationId: reservationId ?? Guid.NewGuid().ToString(),
            UserId: userId ?? Guid.NewGuid().ToString(),
            Dto: new RespondReservationAttendeeDto
            {
                Status = status,
                ResponseNote = responseNote
            });
    }

    private static ReservationAttendeeModel NewAttendee(
        RespondReservationAttendeeCommand command,
        AttendeeStatusEnum status = AttendeeStatusEnum.Pending,
        string? responseNote = null)
    {
        return new ReservationAttendeeModel
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = command.ReservationId,
            UserId = command.UserId,
            Role = AttendeeRoleEnum.Required,
            Status = status,
            InvitedBy = Guid.NewGuid().ToString(),
            RespondedAt = null,
            ResponseNote = responseNote,
            CheckedInAt = null,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    private static ReservationModel NewReservation(
        string reservationId,
        ReservationStatusEnum status = ReservationStatusEnum.Confirmed,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new ReservationModel
        {
            Id = reservationId,
            UserId = Guid.NewGuid().ToString(),
            RoomId = Guid.NewGuid().ToString(),
            CategoryId = Guid.NewGuid().ToString(),
            Title = "Test reservation",
            StartsAt = startsAt ?? now.AddHours(-1),
            EndsAt = endsAt ?? now.AddHours(1),
            Status = status,
            Version = 1,
            CreatedAt = now.AddDays(-1)
        };
    }

    private static RoomModel NewRoom(
        string roomId,
        int capacity = 10)
    {
        return new RoomModel
        {
            Id = roomId,
            RoomTypeId = Guid.NewGuid().ToString(),
            Code = $"R-{Guid.NewGuid():N}"[..10],
            Name = "Test room",
            Description = "Room created for unit tests.",
            Location = "Building A",
            Capacity = capacity,
            IsAccessible = false,
            IsReservable = true,
            IsActive = true,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    // ---------------------------------------------------------------
    // Helpers: verification
    // ---------------------------------------------------------------

    private async Task VerifyNoRepositoryInteractions()
    {
        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .GetByReservationAndUserAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await VerifyReservationLookupsWereNotCalled();
        await VerifyRoomLookupWasNotCalled();
        await VerifyCapacityLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    private async Task VerifyReservationLookupsWereNotCalled()
    {
        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .FindByIdWithForUpdateAsync(
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>());
    }

    private async Task VerifyRoomLookupWasNotCalled()
    {
        await _unitOfWork.RoomRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyCapacityLookupWasNotCalled()
    {
        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .CountOccupyingByReservationAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyUpdateWasNotCalled()
    {
         _unitOfWork.ReservationAttendeeRepository.DidNotReceive()
            .Update(
                Arg.Any<ReservationAttendeeModel>());
    }
}
