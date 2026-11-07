
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reservations.ReservationAttendees.Services;

public sealed class CheckInReservationAttendeeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly CheckInReservationAttendeeUseCase _sut;

    public CheckInReservationAttendeeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new CheckInReservationAttendeeUseCase(_unitOfWork);
    }

    // ---------------------------------------------------------------
    // 1. Validação do command
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
            "Check-in command is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(reservationId: string.Empty);

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
    public async Task ExecuteAsync_WhenAttendeeUserIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(attendeeUserId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Attendee user ID is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterUserIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(requesterUserId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Requester user ID is required.");

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // 2. Validação dos identificadores
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
    public async Task ExecuteAsync_WhenAttendeeUserIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            attendeeUserId: "invalid-attendee-id");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Attendee user ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterUserIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            requesterUserId: "invalid-requester-id");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Requester user ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // 3. Validação da reserva
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        SetupReservation(command, reservation: null);

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

        await VerifyAttendeeLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsNotAttendeeOrOrganizer_ReturnsForbidden()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: Guid.NewGuid().ToString());

        SetupReservation(command, reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.Errors.Should().Contain(
            "Only the attendee or the reservation organizer can check in.");

        await VerifyAttendeeLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationIsCancelled_ReturnsFailure()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: command.RequesterUserId,
            status: ReservationStatusEnum.Cancelled);

        SetupReservation(command, reservation);

        SetupAttendee(
            command,
            NewAttendee(
                command,
                status: AttendeeStatusEnum.Accepted));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .GetByReservationAndUserAsync(
                command.ReservationId,
                command.AttendeeUserId,
                Arg.Any<CancellationToken>());

        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationHasEnded_ReturnsFailure()
    {
        // Arrange
        var command = NewCommand();

        DateTimeOffset endedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var reservation = NewReservation(
            userId: command.RequesterUserId,
            startsAt: endedAt.AddHours(-2),
            endsAt: endedAt);

        SetupReservation(command, reservation);

        SetupAttendee(
            command,
            NewAttendee(
                command,
                status: AttendeeStatusEnum.Accepted));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .GetByReservationAndUserAsync(
                command.ReservationId,
                command.AttendeeUserId,
                Arg.Any<CancellationToken>());

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 4. Validação do participante
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        SetupReservation(
            command,
            NewReservation(
                userId: command.RequesterUserId));

        SetupAttendee(command, attendee: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Attendee not found.");

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .GetByReservationAndUserAsync(
                command.ReservationId,
                command.AttendeeUserId,
                Arg.Any<CancellationToken>());

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 5. Janela de check-in
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationHasNotStarted_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: command.RequesterUserId,
            startsAt: DateTimeOffset.UtcNow.AddHours(1),
            endsAt: DateTimeOffset.UtcNow.AddHours(2));

        SetupReservation(command, reservation);

        SetupAttendee(
            command,
            NewAttendee(
                command,
                status: AttendeeStatusEnum.Accepted));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Check-in is only available after the reservation starts.");

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 6. Validação do status do convite
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeHasPendingInvitation_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();

        SetupValidDependencies(
            command,
            status: AttendeeStatusEnum.Pending);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Only attendees who accepted the invitation can check in.");

        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeDeclinedInvitation_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();

        SetupValidDependencies(
            command,
            status: AttendeeStatusEnum.Declined);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Only attendees who accepted the invitation can check in.");

        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeTentativelyAcceptedInvitation_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();

        SetupValidDependencies(
            command,
            status: AttendeeStatusEnum.Tentative);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Only attendees who accepted the invitation can check in.");

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 7. Idempotência
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeIsAlreadyCheckedIn_ReturnsExistingAttendee()
    {
        // Arrange
        var command = NewCommand();

        var checkedInAt = DateTimeOffset.UtcNow.AddMinutes(-10);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted,
            checkedInAt: checkedInAt,
            version: 3);

        SetupValidDependencies(command, attendee);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        result.Value!.CheckedInAt.Should().Be(checkedInAt);
        result.Value.Version.Should().Be(3);

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 8. Check-in do próprio participante
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeChecksInItself_ReturnsSuccess()
    {
        // Arrange
        var attendeeUserId = Guid.NewGuid().ToString();

        var command = NewCommand(
            requesterUserId: attendeeUserId,
            attendeeUserId: attendeeUserId);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted,
            version: 1);

        SetupValidDependencies(command, attendee);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().NotBeNull();
        result.Value!.CheckedInAt.Should().NotBeNull();

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);
    }

    // ---------------------------------------------------------------
    // 9. Check-in realizado pelo organizador
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenOrganizerChecksInAttendee_ReturnsSuccess()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: command.RequesterUserId);

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted);

        SetupValidDependencies(
            command,
            reservation,
            attendee);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().NotBeNull();
        result.Value!.CheckedInAt.Should().NotBeNull();

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);
    }

    // ---------------------------------------------------------------
    // 10. Check-in válido
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_ChecksInAttendee()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(
            command,
            status: AttendeeStatusEnum.Accepted,
            version: 4);

        SetupValidDependencies(command, attendee);

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(command);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().NotBeNull();

        var checkedInAttendee = result.Value!;

        checkedInAttendee.Id.Should().Be(attendee.Id);
        checkedInAttendee.ReservationId.Should().Be(command.ReservationId);
        checkedInAttendee.UserId.Should().Be(command.AttendeeUserId);

        checkedInAttendee.Status.Should().Be(
            AttendeeStatusEnum.Accepted);

        checkedInAttendee.CheckedInAt.Should().NotBeNull();

        checkedInAttendee.CheckedInAt.Should().BeOnOrAfter(beforeExecution);
        checkedInAttendee.CheckedInAt.Should().BeOnOrBefore(afterExecution);

        checkedInAttendee.UpdatedAt.Should().NotBeNull();

        checkedInAttendee.UpdatedAt.Should().BeOnOrAfter(beforeExecution);
        checkedInAttendee.UpdatedAt.Should().BeOnOrBefore(afterExecution);

        checkedInAttendee.Version.Should().Be(5);

        checkedInAttendee.CreatedAt.Should().Be(attendee.CreatedAt);

        await _unitOfWork.ReservationRepository
            .Received(1)
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .GetByReservationAndUserAsync(
                command.ReservationId,
                command.AttendeeUserId,
                Arg.Any<CancellationToken>());

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(
                Arg.Is<ReservationAttendeeModel>(entity =>
                    entity.Id == attendee.Id &&
                    entity.CheckedInAt != null &&
                    entity.UpdatedAt != null &&
                    entity.Version == 5));
    }

    // ---------------------------------------------------------------
    // 11. Tratamento de exceções
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

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

        await VerifyAttendeeLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        SetupReservation(
            command,
            NewReservation(command.RequesterUserId));

        _unitOfWork.ReservationAttendeeRepository
            .GetByReservationAndUserAsync(
                command.ReservationId,
                command.AttendeeUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReservationAttendeeModel?>(
                new InvalidOperationException(
                    "Attendee lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Attendee lookup failed.");

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 12. CancellationToken
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
    // Helpers: configuração dos repositórios
    // ---------------------------------------------------------------

    private void SetupValidDependencies(
        CheckInReservationAttendeeCommand command,
        ReservationAttendeeModel? attendee = null,
        AttendeeStatusEnum status = AttendeeStatusEnum.Accepted)
    {
        var reservation = NewReservation(
            userId: command.RequesterUserId);

        attendee ??= NewAttendee(
            command,
            status: status);

        SetupValidDependencies(
            command,
            reservation,
            attendee);
    }

    private void SetupValidDependencies(
        CheckInReservationAttendeeCommand command,
        ReservationModel reservation,
        ReservationAttendeeModel attendee)
    {
        SetupReservation(command, reservation);
        SetupAttendee(command, attendee);
    }

    private void SetupReservation(
        CheckInReservationAttendeeCommand command,
        ReservationModel? reservation)
    {
        _unitOfWork.ReservationRepository
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(reservation));
    }

    private void SetupAttendee(
        CheckInReservationAttendeeCommand command,
        ReservationAttendeeModel? attendee)
    {
        _unitOfWork.ReservationAttendeeRepository
            .GetByReservationAndUserAsync(
                command.ReservationId,
                command.AttendeeUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationAttendeeModel?>(attendee));
    }

    // ---------------------------------------------------------------
    // Helpers: comandos e entidades
    // ---------------------------------------------------------------

    private static CheckInReservationAttendeeCommand NewCommand(
        string? reservationId = null,
        string? attendeeUserId = null,
        string? requesterUserId = null)
    {
        return new CheckInReservationAttendeeCommand(
            ReservationId: reservationId ?? Guid.NewGuid().ToString(),
            AttendeeUserId: attendeeUserId ?? Guid.NewGuid().ToString(),
            RequesterUserId: requesterUserId ?? Guid.NewGuid().ToString());
    }

    private static ReservationModel NewReservation(
        string userId,
        ReservationStatusEnum status = ReservationStatusEnum.Confirmed,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DateTimeOffset start =
            startsAt ?? now.AddHours(-1);

        return new ReservationModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            RoomId = Guid.NewGuid().ToString(),
            CategoryId = Guid.NewGuid().ToString(),
            Title = "Test reservation",
            StartsAt = start,
            EndsAt = endsAt ?? now.AddHours(1),
            Status = status,
            Version = 1,
            CreatedAt = now.AddDays(-1)
        };
    }

    private static ReservationAttendeeModel NewAttendee(
        CheckInReservationAttendeeCommand command,
        AttendeeStatusEnum status = AttendeeStatusEnum.Accepted,
        DateTimeOffset? checkedInAt = null,
        long version = 1)
    {
        return new ReservationAttendeeModel
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = command.ReservationId,
            UserId = command.AttendeeUserId,
            Role = AttendeeRoleEnum.Required,
            Status = status,
            InvitedBy = command.RequesterUserId,
            CheckedInAt = checkedInAt,
            Version = version,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    // ---------------------------------------------------------------
    // Helpers: verificações
    // ---------------------------------------------------------------

    private async Task VerifyNoRepositoryInteractions()
    {
        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await VerifyAttendeeLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    private async Task VerifyAttendeeLookupWasNotCalled()
    {
        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .GetByReservationAndUserAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyUpdateWasNotCalled()
    {
        _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .Update(
                Arg.Any<ReservationAttendeeModel>());
    }
}
