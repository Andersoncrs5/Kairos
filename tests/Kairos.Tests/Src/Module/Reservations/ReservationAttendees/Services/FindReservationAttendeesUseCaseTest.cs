
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

public sealed class FindReservationAttendeesUseCaseTest
{
    private const string ViewAllPermission = "reservations.list";

    private readonly IUnitOfWork _unitOfWork;
    private readonly FindReservationAttendeesUseCase _sut;

    public FindReservationAttendeesUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new FindReservationAttendeesUseCase(_unitOfWork);
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

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = new FindReservationAttendeesCommand(
            RequesterUserId: Guid.NewGuid().ToString(),
            Dto: null!);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // 2. Requester validation
    // ---------------------------------------------------------------

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
    // 3. Visibility: own user filter
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenFilterUsesRequesterUserId_ReturnsAttendees()
    {
        // Arrange
        var requesterUserId = Guid.NewGuid().ToString();

        var command = NewCommand(
            requesterUserId: requesterUserId,
            filterUserId: requesterUserId);

        var attendees = NewAttendees(command, count: 2);
        var page = NewPage(attendees);

        SetupFind(page);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(page);
        result.Value!.Data.Should().HaveCount(2);

        await VerifyOwnUserFilterAuthorization(command);
        await VerifyFindReceived(command);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFilterUsesDifferentUserId_DoesNotAllowOwnUserAccess()
    {
        // Arrange
        var command = NewCommand();

        var page = NewPage<ReservationAttendeeModel>([]);

        SetupFind(page);
        SetupNoReservation(command);
        SetupViewAllPermission(command, hasPermission: false);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.Errors.Should().Contain(
            "Filter by your own user ID or by a reservation you take part in.");

        await _unitOfWork.UserRepository
            .Received(1)
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId);

        await VerifyFindWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 4. Visibility: reservation organizer
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsReservationOrganizer_ReturnsAttendees()
    {
        // Arrange
        var command = NewCommand(
            reservationId: Guid.NewGuid().ToString());

        var reservation = NewReservation(
            command.Dto.ReservationId!,
            command.RequesterUserId);

        var attendees = NewAttendees(command, count: 2);
        var page = NewPage(attendees);

        SetupReservation(command, reservation);
        SetupFind(page);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(page);
        result.Value!.Data.Should().HaveCount(2);

        await _unitOfWork.ReservationRepository
            .Received(1)
            .GetByIdAsync(
                command.Dto.ReservationId!,
                Arg.Any<CancellationToken>());

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

        await VerifyFindReceived(command);
    }

    // ---------------------------------------------------------------
    // 5. Visibility: another attendee of the reservation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsReservationAttendee_ReturnsAttendees()
    {
        // Arrange
        var command = NewCommand(
            reservationId: Guid.NewGuid().ToString());

        var reservation = NewReservation(
            command.Dto.ReservationId!,
            organizerUserId: Guid.NewGuid().ToString());

        var attendees = NewAttendees(command, count: 3);
        var page = NewPage(attendees);

        SetupReservation(command, reservation);
        SetupMembership(command, isMember: true);
        SetupFind(page);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(page);
        result.Value!.Data.Should().HaveCount(3);

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .ExistsByReservationAndUserAsync(
                command.Dto.ReservationId!,
                command.RequesterUserId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.UserRepository
            .DidNotReceive()
            .ExistsPermission(
                Arg.Any<string>(),
                Arg.Any<string>());

        await VerifyFindReceived(command);
    }

    // ---------------------------------------------------------------
    // 6. Visibility: reservation does not exist
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExistAndRequesterHasNoPermission_ReturnsForbidden()
    {
        // Arrange
        var command = NewCommand(
            reservationId: Guid.NewGuid().ToString());

        SetupNoReservation(command);
        SetupViewAllPermission(command, hasPermission: false);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.Errors.Should().Contain(
            "Filter by your own user ID or by a reservation you take part in.");

        await _unitOfWork.ReservationRepository
            .Received(1)
            .GetByIdAsync(
                command.Dto.ReservationId!,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .ExistsByReservationAndUserAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await VerifyFindWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExistButRequesterHasPermission_ReturnsAttendees()
    {
        // Arrange
        var command = NewCommand(
            reservationId: Guid.NewGuid().ToString());

        var page = NewPage(
            NewAttendees(command, count: 1));

        SetupNoReservation(command);
        SetupViewAllPermission(command, hasPermission: true);
        SetupFind(page);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(page);

        await _unitOfWork.UserRepository
            .Received(1)
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId);

        await VerifyFindReceived(command);
    }

    // ---------------------------------------------------------------
    // 7. Visibility: global permission
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRequesterHasViewAllPermission_ReturnsAttendees()
    {
        // Arrange
        var command = NewCommand();

        var attendees = NewAttendees(command, count: 3);
        var page = NewPage(attendees);

        SetupViewAllPermission(command, hasPermission: true);
        SetupFind(page);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(page);
        result.Value!.Data.Should().HaveCount(3);

        await _unitOfWork.UserRepository
            .Received(1)
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId);

        await VerifyFindReceived(command);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterHasNoPermissionAndNoValidScope_ReturnsForbidden()
    {
        // Arrange
        var command = NewCommand(
            reservationId: "invalid-reservation-id");

        SetupViewAllPermission(command, hasPermission: false);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.Errors.Should().Contain(
            "Filter by your own user ID or by a reservation you take part in.");

        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork.UserRepository
            .Received(1)
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId);

        await VerifyFindWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 8. Filter validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenFilterPageIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(page: 0);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenFilterPageSizeIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(pageSize: 0);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // 9. Repository filtering
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_PassesOriginalFilterToRepository()
    {
        // Arrange
        var command = NewCommand(
            filterUserId: Guid.NewGuid().ToString(),
            reservationId: Guid.NewGuid().ToString(),
            page: 2,
            pageSize: 20);

        var reservation = NewReservation(
            command.Dto.ReservationId!,
            command.RequesterUserId);

        var page = NewPage<ReservationAttendeeModel>([]);

        SetupReservation(command, reservation);
        SetupFind(page);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Find(
                Arg.Is<FilterReservationAttendeeDto>(filter =>
                    filter.UserId == command.Dto.UserId &&
                    filter.ReservationId == command.Dto.ReservationId &&
                    filter.Page == command.Dto.Page &&
                    filter.PageSize == command.Dto.PageSize),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnUserFilterIsUsed_DoesNotLookupReservation()
    {
        // Arrange
        var requesterUserId = Guid.NewGuid().ToString();

        var command = NewCommand(
            requesterUserId: requesterUserId,
            filterUserId: requesterUserId);

        SetupFind(NewPage<ReservationAttendeeModel>([]));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

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

    // ---------------------------------------------------------------
    // 10. Exception handling
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationLookupFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand(
            reservationId: Guid.NewGuid().ToString());

        _unitOfWork.ReservationRepository
            .GetByIdAsync(
                command.Dto.ReservationId!,
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

        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .Find(
                Arg.Any<FilterReservationAttendeeDto>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipCheckFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand(
            reservationId: Guid.NewGuid().ToString());

        SetupReservation(
            command,
            NewReservation(
                command.Dto.ReservationId!,
                organizerUserId: Guid.NewGuid().ToString()));

        _unitOfWork.ReservationAttendeeRepository
            .ExistsByReservationAndUserAsync(
                command.Dto.ReservationId!,
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

        await VerifyFindWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionCheckFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

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

        await VerifyFindWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenFindFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        SetupViewAllPermission(command, hasPermission: true);

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

    private void SetupFind(Page<ReservationAttendeeModel> page)
    {
        _unitOfWork.ReservationAttendeeRepository
            .Find(
                Arg.Any<FilterReservationAttendeeDto>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(page));
    }

    private void SetupReservation(
        FindReservationAttendeesCommand command,
        ReservationModel? reservation)
    {
        _unitOfWork.ReservationRepository
            .GetByIdAsync(
                command.Dto.ReservationId!,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(reservation));
    }

    private void SetupNoReservation(
        FindReservationAttendeesCommand command)
    {
        SetupReservation(command, reservation: null);
    }

    private void SetupMembership(
        FindReservationAttendeesCommand command,
        bool isMember)
    {
        _unitOfWork.ReservationAttendeeRepository
            .ExistsByReservationAndUserAsync(
                command.Dto.ReservationId!,
                command.RequesterUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(isMember));
    }

    private void SetupViewAllPermission(
        FindReservationAttendeesCommand command,
        bool hasPermission)
    {
        _unitOfWork.UserRepository
            .ExistsPermission(
                ViewAllPermission,
                command.RequesterUserId)
            .Returns(Task.FromResult(hasPermission));
    }

    // ---------------------------------------------------------------
    // Helpers: authorization verification
    // ---------------------------------------------------------------

    private async Task VerifyOwnUserFilterAuthorization(
        FindReservationAttendeesCommand command)
    {
        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

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

    // ---------------------------------------------------------------
    // Helpers: repository verification
    // ---------------------------------------------------------------

    private async Task VerifyFindReceived(
        FindReservationAttendeesCommand command)
    {
        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .Find(
                Arg.Is<FilterReservationAttendeeDto>(filter =>
                    filter.UserId == command.Dto.UserId &&
                    filter.ReservationId == command.Dto.ReservationId &&
                    filter.Page == command.Dto.Page &&
                    filter.PageSize == command.Dto.PageSize),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyFindWasNotCalled()
    {
        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .Find(
                Arg.Any<FilterReservationAttendeeDto>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyNoRepositoryInteractions()
    {
        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

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

        await VerifyFindWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Helpers: commands and entities
    // ---------------------------------------------------------------

    private static FindReservationAttendeesCommand NewCommand(
        string? requesterUserId = null,
        string? filterUserId = null,
        string? reservationId = null,
        int page = 1,
        int pageSize = 10)
    {
        return new FindReservationAttendeesCommand(
            RequesterUserId: requesterUserId ?? Guid.NewGuid().ToString(),
            Dto: new FilterReservationAttendeeDto
            {
                UserId = filterUserId ?? Guid.NewGuid().ToString(),
                ReservationId = reservationId,
                Page = page,
                PageSize = pageSize
            });
    }

    private static ReservationModel NewReservation(
        string reservationId,
        string organizerUserId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new ReservationModel
        {
            Id = reservationId,
            UserId = organizerUserId,
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
        FindReservationAttendeesCommand command)
    {
        return new ReservationAttendeeModel
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = command.Dto.ReservationId
                ?? Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid().ToString(),
            Role = AttendeeRoleEnum.Required,
            Status = AttendeeStatusEnum.Pending,
            InvitedBy = command.RequesterUserId,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    private static List<ReservationAttendeeModel> NewAttendees(
        FindReservationAttendeesCommand command,
        int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => NewAttendee(command))
            .ToList();
    }

    private static Page<T> NewPage<T>(
        IEnumerable<T> items)
    {
        T[] data = items.ToArray();

        return Page<T>.Create(
            data,
            totalCount: data.Length,
            pageNumber: 1,
            pageSize: 10);
    }
}
