
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

public sealed class RemoveReservationAttendeeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly RemoveReservationAttendeeUseCase _sut;

    public RemoveReservationAttendeeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new RemoveReservationAttendeeUseCase(_unitOfWork);
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
            "Remove attendee command is required.");

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
    // 3. Attendee lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeDoesNotExist_ReturnsNotFound()
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
            "Attendee not found.");

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .GetByIdAndReservationAsync(
                command.AttendeeId,
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await VerifyReservationLookupWasNotCalled();
        await VerifyDeleteWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeBelongsToAnotherReservation_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        // The repository query must return null when the attendee
        // does not belong to the specified reservation.
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

        await VerifyReservationLookupWasNotCalled();
        await VerifyDeleteWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 4. Reservation lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(command);

        SetupAttendee(command, attendee);
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

        await VerifyDeleteWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 5. Authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsNotOrganizerOrAttendee_ReturnsForbidden()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(
            command,
            userId: Guid.NewGuid().ToString());

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: Guid.NewGuid().ToString());

        SetupAttendee(command, attendee);
        SetupReservation(command, reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.Errors.Should().Contain(
            "Only the reservation organizer or the attendee can remove this attendee.");

        await VerifyDeleteWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsOrganizer_RemovesAnotherAttendee()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(
            command,
            userId: Guid.NewGuid().ToString());

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupValidDependencies(
            command,
            attendee,
            reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeTrue();

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Delete(attendee);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsAttendee_RemovesItself()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(
            command,
            userId: command.RequesterUserId);

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: Guid.NewGuid().ToString());

        SetupValidDependencies(
            command,
            attendee,
            reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeTrue();

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Delete(attendee);
    }

    // ---------------------------------------------------------------
    // 6. Reservation status validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationIsCancelled_ReturnsFailure()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(command);

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId,
            status: ReservationStatusEnum.Cancelled);

        SetupValidDependencies(
            command,
            attendee,
            reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();

        result.Errors.Should().NotBeEmpty();

        await VerifyDeleteWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationHasEnded_ReturnsFailure()
    {
        // Arrange
        var command = NewCommand();

        var now = DateTimeOffset.UtcNow;

        var attendee = NewAttendee(command);

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId,
            startsAt: now.AddHours(-3),
            endsAt: now.AddHours(-1));

        SetupValidDependencies(
            command,
            attendee,
            reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();

        result.Errors.Should().NotBeEmpty();

        await VerifyDeleteWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 7. Successful removal
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_RemovesAttendee()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(command);

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupValidDependencies(
            command,
            attendee,
            reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeTrue();

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .GetByIdAndReservationAsync(
                command.AttendeeId,
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationRepository
            .Received(1)
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>());

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Delete(
                Arg.Is<ReservationAttendeeModel>(entity =>
                    entity.Id == attendee.Id &&
                    entity.ReservationId == command.ReservationId &&
                    entity.UserId == attendee.UserId));
    }

    // ---------------------------------------------------------------
    // 8. Exception handling
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

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

        await VerifyReservationLookupWasNotCalled();
        await VerifyDeleteWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(command);

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

        await VerifyDeleteWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeleteFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(command);

        var reservation = NewReservation(
            command.ReservationId,
            organizerUserId: command.RequesterUserId);

        SetupValidDependencies(
            command,
            attendee,
            reservation);

        _unitOfWork.ReservationAttendeeRepository
            .When(repository => repository.Delete(attendee))
            .Do(_ => throw new InvalidOperationException(
                "Attendee deletion failed."));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Attendee deletion failed.");

        _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Delete(attendee);
    }

    // ---------------------------------------------------------------
    // 9. CancellationToken
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
        RemoveReservationAttendeeCommand command,
        ReservationAttendeeModel attendee,
        ReservationModel reservation)
    {
        SetupAttendee(command, attendee);
        SetupReservation(command, reservation);
    }

    private void SetupAttendee(
        RemoveReservationAttendeeCommand command,
        ReservationAttendeeModel? attendee)
    {
        _unitOfWork.ReservationAttendeeRepository
            .GetByIdAndReservationAsync(
                command.AttendeeId,
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationAttendeeModel?>(attendee));
    }

    private void SetupReservation(
        RemoveReservationAttendeeCommand command,
        ReservationModel? reservation)
    {
        _unitOfWork.ReservationRepository
            .GetByIdAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(reservation));
    }

    // ---------------------------------------------------------------
    // Helpers: commands and entities
    // ---------------------------------------------------------------

    private static RemoveReservationAttendeeCommand NewCommand(
        string? reservationId = null,
        string? attendeeId = null,
        string? requesterUserId = null)
    {
        return new RemoveReservationAttendeeCommand(
            ReservationId: reservationId ?? Guid.NewGuid().ToString(),
            AttendeeId: attendeeId ?? Guid.NewGuid().ToString(),
            RequesterUserId: requesterUserId ?? Guid.NewGuid().ToString());
    }

    private static ReservationAttendeeModel NewAttendee(
        RemoveReservationAttendeeCommand command,
        string? userId = null)
    {
        return new ReservationAttendeeModel
        {
            Id = command.AttendeeId,
            ReservationId = command.ReservationId,
            UserId = userId ?? Guid.NewGuid().ToString(),
            Role = AttendeeRoleEnum.Required,
            Status = AttendeeStatusEnum.Accepted,
            InvitedBy = Guid.NewGuid().ToString(),
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
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

    // ---------------------------------------------------------------
    // Helpers: verification
    // ---------------------------------------------------------------

    private async Task VerifyNoRepositoryInteractions()
    {
        await VerifyAttendeeLookupWasNotCalled();
        await VerifyReservationLookupWasNotCalled();
        await VerifyDeleteWasNotCalled();
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

    private async Task VerifyReservationLookupWasNotCalled()
    {
        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyDeleteWasNotCalled()
    {
        _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .Delete(
                Arg.Any<ReservationAttendeeModel>());
    }
}
