using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reservations.ReservationAttendees.Services;

public sealed class FindReservationAttendeeByIdUseCaseTest
{
    private const string ViewAllPermission = "reservations.read";

    private readonly IUnitOfWork _unitOfWork;
    private readonly FindReservationAttendeeByIdUseCase _sut;

    public FindReservationAttendeeByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new FindReservationAttendeeByIdUseCase(_unitOfWork);
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
            "Attendee search command is required.");

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

        SetupFind(command, attendee: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Attendee not found.");

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Find(
                Arg.Is<FilterReservationAttendeeDto>(filter =>
                    filter.Id == command.AttendeeId &&
                    filter.ReservationId == command.ReservationId &&
                    filter.Page == 1 &&
                    filter.PageSize == 1),
                Arg.Any<CancellationToken>());

        await VerifyVisibilityChecksWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeBelongsToAnotherReservation_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        // The repository must filter by both attendee and reservation.
        SetupFind(command, attendee: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Attendee not found.");

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Find(
                Arg.Is<FilterReservationAttendeeDto>(filter =>
                    filter.Id == command.AttendeeId &&
                    filter.ReservationId == command.ReservationId),
                Arg.Any<CancellationToken>());

        await VerifyVisibilityChecksWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_UsesCorrectFilter()
    {
        // Arrange
        var command = NewCommand();

        var attendee = NewAttendee(command);

        SetupFind(command, attendee);

        SetupMemberCheck(command, isMember: true);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Find(
                Arg.Is<FilterReservationAttendeeDto>(filter =>
                    filter.Id == command.AttendeeId &&
                    filter.ReservationId == command.ReservationId &&
                    filter.Page == 1 &&
                    filter.PageSize == 1),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 4. Visibility: attendee
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsAttendee_ReturnsAttendee()
    {
        // Arrange
        var requesterUserId = Guid.NewGuid().ToString();

        var command = NewCommand(
            requesterUserId: requesterUserId);

        var reservation = NewReservation(
            userId: Guid.NewGuid().ToString());

        var attendee = NewAttendee(
            command,
            reservation: reservation,
            userId: requesterUserId);

        SetupFind(command, attendee);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        await VerifyVisibilityChecksWereNotCalled();
    }

    // ---------------------------------------------------------------
    // 5. Visibility: reservation organizer
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsReservationOrganizer_ReturnsAttendee()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: command.RequesterUserId);

        var attendee = NewAttendee(
            command,
            reservation: reservation);

        SetupFind(command, attendee);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        await VerifyVisibilityChecksWereNotCalled();
    }

    // ---------------------------------------------------------------
    // 6. Visibility: another attendee of the same reservation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsAnotherAttendee_ReturnsAttendee()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: Guid.NewGuid().ToString());

        var attendee = NewAttendee(
            command,
            reservation: reservation);

        SetupFind(command, attendee);
        SetupMemberCheck(command, isMember: true);

        _unitOfWork.UserRepository
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .ExistsByReservationAndUserAsync(
                command.ReservationId,
                command.RequesterUserId,
                Arg.Any<CancellationToken>());

        // Membership is enough; the global permission check is skipped.
        await _unitOfWork.UserRepository
            .DidNotReceive()
            .ExistsPermission(
                Arg.Any<string>(),
                Arg.Any<string>());
    }

    // ---------------------------------------------------------------
    // 7. Visibility: global permission
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterHasViewAllPermission_ReturnsAttendee()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: Guid.NewGuid().ToString());

        var attendee = NewAttendee(
            command,
            reservation: reservation);

        SetupFind(command, attendee);
        SetupMemberCheck(command, isMember: false);

        _unitOfWork.UserRepository
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(attendee);

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .ExistsByReservationAndUserAsync(
                command.ReservationId,
                command.RequesterUserId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.UserRepository
            .Received(1)
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId);
    }

    // ---------------------------------------------------------------
    // 8. Privacy protection
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterHasNoVisibility_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: Guid.NewGuid().ToString());

        var attendee = NewAttendee(
            command,
            reservation: reservation);

        SetupFind(command, attendee);
        SetupMemberCheck(command, isMember: false);

        _unitOfWork.UserRepository
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Attendee not found.");

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .ExistsByReservationAndUserAsync(
                command.ReservationId,
                command.RequesterUserId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.UserRepository
            .Received(1)
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId);
    }

    // ---------------------------------------------------------------
    // 9. Exception handling
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenFindFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        _unitOfWork.ReservationAttendeeRepository
            .Find(
                Arg.Any<FilterReservationAttendeeDto>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Page<ReservationAttendeeModel>>(
                new InvalidOperationException(
                    "Attendee search failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Attendee search failed.");

        await VerifyVisibilityChecksWereNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipCheckFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: Guid.NewGuid().ToString());

        var attendee = NewAttendee(
            command,
            reservation: reservation);

        SetupFind(command, attendee);

        _unitOfWork.ReservationAttendeeRepository
            .ExistsByReservationAndUserAsync(
                command.ReservationId,
                command.RequesterUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(
                new InvalidOperationException(
                    "Membership lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Membership lookup failed.");

        await _unitOfWork.UserRepository
            .DidNotReceive()
            .ExistsPermission(
                Arg.Any<string>(),
                Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionCheckFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: Guid.NewGuid().ToString());

        var attendee = NewAttendee(
            command,
            reservation: reservation);

        SetupFind(command, attendee);
        SetupMemberCheck(command, isMember: false);

        _unitOfWork.UserRepository
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId)
            .Returns(Task.FromException<bool>(
                new InvalidOperationException(
                    "Permission lookup failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission lookup failed.");
    }

    // ---------------------------------------------------------------
    // 10. CancellationToken
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

    private void SetupFind(
        FindReservationAttendeeByIdCommand command,
        ReservationAttendeeModel? attendee)
    {
        _unitOfWork.ReservationAttendeeRepository
            .Find(
                Arg.Any<FilterReservationAttendeeDto>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                NewPage(attendee)));
    }

    private void SetupMemberCheck(
        FindReservationAttendeeByIdCommand command,
        bool isMember)
    {
        _unitOfWork.ReservationAttendeeRepository
            .ExistsByReservationAndUserAsync(
                command.ReservationId,
                command.RequesterUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(isMember));
    }

    // ---------------------------------------------------------------
    // Helpers: commands and entities
    // ---------------------------------------------------------------

    private static FindReservationAttendeeByIdCommand NewCommand(
        string? reservationId = null,
        string? attendeeId = null,
        string? requesterUserId = null)
    {
        return new FindReservationAttendeeByIdCommand(
            ReservationId: reservationId ?? Guid.NewGuid().ToString(),
            AttendeeId: attendeeId ?? Guid.NewGuid().ToString(),
            RequesterUserId: requesterUserId ?? Guid.NewGuid().ToString());
    }

    private static ReservationModel NewReservation(
        string userId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new ReservationModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            RoomId = Guid.NewGuid().ToString(),
            CategoryId = Guid.NewGuid().ToString(),
            Title = "Test reservation",
            StartsAt = now.AddDays(1),
            EndsAt = now.AddDays(1).AddHours(1),
            Status = ReservationStatusEnum.Confirmed,
            Version = 1,
            CreatedAt = now
        };
    }

    private static ReservationAttendeeModel NewAttendee(
        FindReservationAttendeeByIdCommand command,
        ReservationModel? reservation = null,
        string? userId = null)
    {
        return new ReservationAttendeeModel
        {
            Id = command.AttendeeId,
            ReservationId = command.ReservationId,
            Reservation = reservation,
            UserId = userId ?? Guid.NewGuid().ToString(),
            Role = AttendeeRoleEnum.Required,
            Status = AttendeeStatusEnum.Accepted,
            InvitedBy = Guid.NewGuid().ToString(),
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    private static Page<T> NewPage<T>(T? item)
    {
        IEnumerable<T> data = item is null
            ? Array.Empty<T>()
            : [item];

        long totalCount = item is null ? 0 : 1;

        return Page<T>.Create(
            data,
            totalCount,
            pageNumber: 1,
            pageSize: 1);
    }

    // ---------------------------------------------------------------
    // Helpers: verification
    // ---------------------------------------------------------------

    private async Task VerifyNoRepositoryInteractions()
    {
        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .Find(
                Arg.Any<FilterReservationAttendeeDto>(),
                Arg.Any<CancellationToken>());

        await VerifyVisibilityChecksWereNotCalled();
    }

    private async Task VerifyVisibilityChecksWereNotCalled()
    {
        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .ExistsByReservationAndUserAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork.UserRepository
            .DidNotReceive()
            .ExistsPermission(
                Arg.Any<string>(),
                Arg.Any<string>());
    }
}