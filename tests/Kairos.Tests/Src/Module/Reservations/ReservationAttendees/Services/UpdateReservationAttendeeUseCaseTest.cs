
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reservations.ReservationAttendees.Services;

public sealed class UpdateReservationAttendeeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateReservationAttendeeUseCase _sut;

    public UpdateReservationAttendeeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new UpdateReservationAttendeeUseCase(_unitOfWork);
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
            "Attendee update command is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = new UpdateReservationAttendeeCommand(
            ReservationId: Guid.NewGuid().ToString(),
            AttendeeId: Guid.NewGuid().ToString(),
            RequesterUserId: Guid.NewGuid().ToString(),
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
    public async Task ExecuteAsync_WhenAttendeeIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            attendeeId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Attendee ID is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterUserIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            requesterUserId: string.Empty);

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
    // 2. Identifier validation
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
    public async Task ExecuteAsync_WhenAttendeeIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            attendeeId: "invalid-attendee-id");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Attendee ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterUserIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            requesterUserId: "invalid-user-id");

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
    // 3. DTO validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoleIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            role: (AttendeeRoleEnum)999);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // 4. Reservation lookup
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

    // ---------------------------------------------------------------
    // 5. Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsNotOrganizer_ReturnsForbidden()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: Guid.NewGuid().ToString());

        SetupReservation(command, reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.Errors.Should().Contain(
            "Only the reservation organizer can update attendees.");

        // Authorization happens before attendee lookup.
        await VerifyAttendeeLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsOrganizer_ContinuesToAttendeeLookup()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupReservation(command, reservation);
        SetupAttendee(command, attendee: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .GetByIdAndReservationAsync(
                command.AttendeeId,
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 6. Attendee lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupReservation(command, reservation);
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
            .GetByIdAndReservationAsync(
                command.AttendeeId,
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeBelongsToAnotherReservation_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupReservation(command, reservation);
        SetupAttendee(command, attendee: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Attendee not found.");

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 7. Reservation status validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationIsCancelled_ReturnsFailure()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId,
            status: ReservationStatusEnum.Cancelled);

        var attendee = NewAttendee(command);

        SetupValidDependencies(command, reservation, attendee);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();

        result.Errors.Should().NotBeEmpty();

        await VerifyUpdateWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationHasEnded_ReturnsFailure()
    {
        // Arrange
        var command = NewCommand();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId,
            startsAt: now.AddHours(-3),
            endsAt: now.AddHours(-1));

        var attendee = NewAttendee(command);

        SetupValidDependencies(command, reservation, attendee);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();

        result.Errors.Should().NotBeEmpty();

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 8. Idempotency
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoleIsUnchanged_ReturnsExistingAttendee()
    {
        // Arrange
        var command = NewCommand(
            role: AttendeeRoleEnum.Required);

        var attendee = NewAttendee(
            command,
            role: AttendeeRoleEnum.Required);

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupValidDependencies(command, reservation, attendee);

        DateTimeOffset? previousUpdatedAt = attendee.UpdatedAt;

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        attendee.Role.Should().Be(AttendeeRoleEnum.Required);
        attendee.UpdatedAt.Should().Be(previousUpdatedAt);

        await VerifyUpdateWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 9. Role update
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(AttendeeRoleEnum.Required, AttendeeRoleEnum.Optional)]
    [InlineData(AttendeeRoleEnum.Optional, AttendeeRoleEnum.Required)]
    public async Task ExecuteAsync_WhenRoleChanges_UpdatesAttendee(
        AttendeeRoleEnum currentRole,
        AttendeeRoleEnum newRole)
    {
        // Arrange
        var command = NewCommand(role: newRole);

        var attendee = NewAttendee(
            command,
            role: currentRole);

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupValidDependencies(command, reservation, attendee);

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(command);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        attendee.Role.Should().Be(newRole);

        attendee.UpdatedAt.Should().NotBeNull();

        attendee.UpdatedAt.Should().BeOnOrAfter(beforeExecution);
        attendee.UpdatedAt.Should().BeOnOrBefore(afterExecution);

        attendee.Version.Should().Be(1);

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(
                Arg.Is<ReservationAttendeeModel>(entity =>
                    entity.Id == attendee.Id &&
                    entity.ReservationId == command.ReservationId &&
                    entity.Role == newRole &&
                    entity.UpdatedAt != null));
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleChanges_OnlyUpdatesTargetAttendee()
    {
        // Arrange
        var command = NewCommand(
            role: AttendeeRoleEnum.Optional);

        var attendee = NewAttendee(
            command,
            role: AttendeeRoleEnum.Required);

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupValidDependencies(command, reservation, attendee);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Update(attendee);

        _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .Update(
                Arg.Is<ReservationAttendeeModel>(entity =>
                    entity.Id != attendee.Id));
    }

    // ---------------------------------------------------------------
    // 10. Exception handling
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

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupReservation(command, reservation);

        _unitOfWork.ReservationAttendeeRepository
            .GetByIdAndReservationAsync(
                command.AttendeeId,
                command.ReservationId,
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

    [Fact]
    public async Task ExecuteAsync_WhenUpdateFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand(
            role: AttendeeRoleEnum.Optional);

        var attendee = NewAttendee(
            command,
            role: AttendeeRoleEnum.Required);

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupValidDependencies(command, reservation, attendee);

        _unitOfWork.ReservationAttendeeRepository
            .When(repository => repository.Update(attendee))
            .Do(_ => throw new InvalidOperationException(
                "Attendee update failed."));

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
    // 11. CancellationToken
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

    private void SetupValidDependencies(
        UpdateReservationAttendeeCommand command,
        ReservationModel reservation,
        ReservationAttendeeModel attendee)
    {
        SetupReservation(command, reservation);
        SetupAttendee(command, attendee);
    }

    private void SetupReservation(
        UpdateReservationAttendeeCommand command,
        ReservationModel? reservation)
    {
        _unitOfWork.ReservationRepository
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(reservation));
    }

    private void SetupAttendee(
        UpdateReservationAttendeeCommand command,
        ReservationAttendeeModel? attendee)
    {
        _unitOfWork.ReservationAttendeeRepository
            .GetByIdAndReservationAsync(
                command.AttendeeId,
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationAttendeeModel?>(attendee));
    }

    // ---------------------------------------------------------------
    // Helpers: commands and entities
    // ---------------------------------------------------------------

    private static UpdateReservationAttendeeCommand NewCommand(
        string? reservationId = null,
        string? attendeeId = null,
        string? requesterUserId = null,
        AttendeeRoleEnum role = AttendeeRoleEnum.Required)
    {
        return new UpdateReservationAttendeeCommand(
            ReservationId: reservationId ?? Guid.NewGuid().ToString(),
            AttendeeId: attendeeId ?? Guid.NewGuid().ToString(),
            RequesterUserId: requesterUserId ?? Guid.NewGuid().ToString(),
            Dto: new UpdateReservationAttendeeDto
            {
                Role = role
            });
    }

    private static ReservationModel NewReservation(
        string reservationId,
        string organizerUserId,
        ReservationStatusEnum status = ReservationStatusEnum.Confirmed,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new ReservationModel
        {
            Id = reservationId,
            UserId = organizerUserId,
            RoomId = Guid.NewGuid().ToString(),
            CategoryId = Guid.NewGuid().ToString(),
            Title = "Test reservation",
            StartsAt = startsAt ?? now.AddDays(1),
            EndsAt = endsAt ?? now.AddDays(1).AddHours(1),
            Status = status,
            Version = 1,
            CreatedAt = now
        };
    }

    private static ReservationAttendeeModel NewAttendee(
        UpdateReservationAttendeeCommand command,
        AttendeeRoleEnum role = AttendeeRoleEnum.Required)
    {
        return new ReservationAttendeeModel
        {
            Id = command.AttendeeId,
            ReservationId = command.ReservationId,
            UserId = Guid.NewGuid().ToString(),
            Role = role,
            Status = AttendeeStatusEnum.Pending,
            InvitedBy = Guid.NewGuid().ToString(),
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    // ---------------------------------------------------------------
    // Helpers: verification
    // ---------------------------------------------------------------

    private async Task VerifyNoRepositoryInteractions()
    {
        await VerifyReservationLookupWasNotCalled();
        await VerifyAttendeeLookupWasNotCalled();
        await VerifyUpdateWasNotCalled();
    }

    private async Task VerifyReservationLookupWasNotCalled()
    {
        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyAttendeeLookupWasNotCalled()
    {
        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .GetByIdAndReservationAsync(
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
