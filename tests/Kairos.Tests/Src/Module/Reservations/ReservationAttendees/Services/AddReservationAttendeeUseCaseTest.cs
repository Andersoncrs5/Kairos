using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;
using Kairos.Api.Modules.Rooms.Room.Models;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reservations.ReservationAttendees.Services;

public sealed class AddReservationAttendeeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AddReservationAttendeeUseCase _sut;

    public AddReservationAttendeeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new AddReservationAttendeeUseCase(_unitOfWork);
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
            "Attendee command is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = new AddReservationAttendeeCommand(
            ReservationId: Guid.NewGuid().ToString(),
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
    public async Task ExecuteAsync_WhenReservationIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(reservationId: "invalid-reservation-id");

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
    public async Task ExecuteAsync_WhenRequesterUserIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(requesterUserId: "invalid-user-id");

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
    // 2. Validação do DTO
    // ---------------------------------------------------------------

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
    public async Task ExecuteAsync_WhenAttendeeUserIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(attendeeUserId: "invalid-attendee-id");

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

        result.Errors.Should().Contain(
            "Attendee role is invalid.");

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
            .FindByIdWithForUpdateAsync(
                command.ReservationId, null,
                Arg.Any<CancellationToken>());

        await _unitOfWork.UserRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequesterIsNotOrganizer_ReturnsForbidden()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: Guid.NewGuid().ToString(),
            roomId: Guid.NewGuid().ToString());

        SetupReservation(command, reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.Errors.Should().Contain(
            "Only the reservation organizer can add attendees.");

        await _unitOfWork.UserRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<string>());

        await VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationIsCancelled_ReturnsBadRequest()
    {
        // Arrange
        var command = NewCommand();

        var reservation = NewReservation(
            userId: command.RequesterUserId,
            roomId: Guid.NewGuid().ToString(),
            status: ReservationStatusEnum.Cancelled);

        SetupReservation(command, reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();

        await _unitOfWork.UserRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<string>());

        await VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationHasEnded_ReturnsBadRequest()
    {
        // Arrange
        var command = NewCommand();

        DateTimeOffset endedAt = DateTimeOffset.UtcNow.AddHours(-2);

        var reservation = NewReservation(
            userId: command.RequesterUserId,
            roomId: Guid.NewGuid().ToString(),
            startsAt: endedAt.AddHours(-1),
            endsAt: endedAt);

        SetupReservation(command, reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();

        await _unitOfWork.UserRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<string>());

        await VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 4. Validação do usuário convidado
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeIsOrganizer_ReturnsBadRequest()
    {
        // Arrange
        var requesterUserId = Guid.NewGuid().ToString();

        var command = NewCommand(
            requesterUserId: requesterUserId,
            attendeeUserId: requesterUserId);

        var reservation = NewReservation(
            userId: requesterUserId,
            roomId: Guid.NewGuid().ToString());

        SetupReservation(command, reservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "The organizer is already part of the reservation.");

        await _unitOfWork.UserRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<string>());

        await VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        SetupReservation(
            command,
            NewReservation(command.RequesterUserId, Guid.NewGuid().ToString()));

        SetupAttendeeUser(command, user: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Attendee user not found.");

        await _unitOfWork.UserRepository
            .Received(1)
            .GetByIdAsync(command.Dto.UserId);

        await VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttendeeUserIsInactive_ReturnsBadRequest()
    {
        // Arrange
        var command = NewCommand();

        SetupReservation(
            command,
            NewReservation(command.RequesterUserId, Guid.NewGuid().ToString()));

        SetupAttendeeUser(
            command,
            NewUser(command.Dto.UserId, isActive: false));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Attendee user is not active.");

        await VerifyDuplicateCheckWasNotCalled();
        await VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 5. Validação de duplicidade
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenUserIsAlreadyAttendee_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();

        SetupValidDependencies(command);

        _unitOfWork.ReservationAttendeeRepository
            .ExistsByReservationAndUserAsync(
                command.ReservationId,
                command.Dto.UserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "User is already an attendee of this reservation.");

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .ExistsByReservationAndUserAsync(
                command.ReservationId,
                command.Dto.UserId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.RoomRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 6. Validação da sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        SetupReservation(
            command,
            NewReservation(command.RequesterUserId, Guid.NewGuid().ToString()));

        SetupAttendeeUser(command, NewUser(command.Dto.UserId));
        SetupNoDuplicate(command);

        SetupRoom(
            Guid.NewGuid().ToString(),
            room: null);

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
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomCapacityIsReached_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();

        var roomId = Guid.NewGuid().ToString();

        SetupReservation(
            command,
            NewReservation(command.RequesterUserId, roomId));

        SetupAttendeeUser(command, NewUser(command.Dto.UserId));
        SetupNoDuplicate(command);

        SetupRoom(
            roomId,
            NewRoom(roomId, capacity: 2));

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

        await VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 7. Criação do convidado
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_CreatesAttendee()
    {
        // Arrange
        var command = NewCommand();

        var roomId = Guid.NewGuid().ToString();

        var reservation = NewReservation(
            userId: command.RequesterUserId,
            roomId: roomId);

        var attendeeUser = NewUser(command.Dto.UserId);

        SetupValidDependencies(
            command,
            reservation,
            attendeeUser,
            NewRoom(roomId));

        _unitOfWork.ReservationAttendeeRepository
            .AddAsync(
                Arg.Any<ReservationAttendeeModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(command);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);

        result.Value.Should().NotBeNull();

        var attendee = result.Value!;

        Guid.TryParse(attendee.Id, out _).Should().BeTrue();

        attendee.ReservationId.Should().Be(command.ReservationId);
        attendee.UserId.Should().Be(command.Dto.UserId);
        attendee.User.Should().BeSameAs(attendeeUser);

        attendee.Role.Should().Be(command.Dto.Role);
        attendee.Status.Should().Be(AttendeeStatusEnum.Pending);

        attendee.InvitedBy.Should().Be(command.RequesterUserId);

        attendee.Version.Should().Be(1);

        attendee.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        attendee.CreatedAt.Should().BeOnOrBefore(afterExecution);

        attendee.UpdatedAt.Should().BeNull();
        attendee.RespondedAt.Should().BeNull();

        await _unitOfWork.ReservationRepository
            .Received(1)
            .FindByIdWithForUpdateAsync(
                command.ReservationId, null,
                Arg.Any<CancellationToken>());

        await _unitOfWork.UserRepository
            .Received(1)
            .GetByIdAsync(command.Dto.UserId);

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .ExistsByReservationAndUserAsync(
                command.ReservationId,
                command.Dto.UserId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.RoomRepository
            .Received(1)
            .GetByIdAsync(
                roomId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .CountOccupyingByReservationAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .AddAsync(
                Arg.Is<ReservationAttendeeModel>(entity =>
                    entity.Id == attendee.Id &&
                    entity.ReservationId == command.ReservationId &&
                    entity.UserId == command.Dto.UserId &&
                    entity.Role == command.Dto.Role &&
                    entity.Status == AttendeeStatusEnum.Pending &&
                    entity.InvitedBy == command.RequesterUserId &&
                    entity.Version == 1),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoleIsOptional_CreatesOptionalAttendee()
    {
        // Arrange
        var command = NewCommand(
            role: AttendeeRoleEnum.Optional);

        var roomId = Guid.NewGuid().ToString();

        SetupValidDependencies(
            command,
            NewReservation(command.RequesterUserId, roomId),
            NewUser(command.Dto.UserId),
            NewRoom(roomId));

        _unitOfWork.ReservationAttendeeRepository
            .AddAsync(
                Arg.Any<ReservationAttendeeModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        result.Value!.Role.Should().Be(AttendeeRoleEnum.Optional);
        result.Value.Status.Should().Be(AttendeeStatusEnum.Pending);

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .AddAsync(
                Arg.Is<ReservationAttendeeModel>(entity =>
                    entity.Role == AttendeeRoleEnum.Optional),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 8. Tratamento de exceções
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAddAsyncFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        var roomId = Guid.NewGuid().ToString();

        SetupValidDependencies(
            command,
            NewReservation(command.RequesterUserId, roomId),
            NewUser(command.Dto.UserId),
            NewRoom(roomId));

        _unitOfWork.ReservationAttendeeRepository
            .AddAsync(
                Arg.Any<ReservationAttendeeModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(
                new InvalidOperationException(
                    "Attendee persistence failed.")));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Attendee persistence failed.");

        await _unitOfWork.ReservationAttendeeRepository
            .Received(1)
            .AddAsync(
                Arg.Any<ReservationAttendeeModel>(),
                Arg.Any<CancellationToken>());
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
    // Helpers: configuração dos repositórios
    // ---------------------------------------------------------------

    private void SetupValidDependencies(
        AddReservationAttendeeCommand command,
        ReservationModel? reservation = null,
        UserModel? user = null,
        RoomModel? room = null)
    {
        string roomId = reservation?.RoomId
            ?? Guid.NewGuid().ToString();

        reservation ??= NewReservation(
            command.RequesterUserId,
            roomId);

        user ??= NewUser(command.Dto.UserId);
        room ??= NewRoom(roomId);

        SetupReservation(command, reservation);
        SetupAttendeeUser(command, user);
        SetupNoDuplicate(command);
        SetupRoom(roomId, room);
        SetupOccupyingCount(command, occupying: 0);
    }

    private void SetupReservation(
        AddReservationAttendeeCommand command,
        ReservationModel? reservation)
    {
        _unitOfWork.ReservationRepository
            .FindByIdWithForUpdateAsync(
                command.ReservationId, null,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(reservation));
    }

    private void SetupAttendeeUser(
        AddReservationAttendeeCommand command,
        UserModel? user)
    {
        _unitOfWork.UserRepository
            .GetByIdAsync(command.Dto.UserId)
            .Returns(Task.FromResult(user));
    }

    private void SetupNoDuplicate(
        AddReservationAttendeeCommand command)
    {
        _unitOfWork.ReservationAttendeeRepository
            .ExistsByReservationAndUserAsync(
                command.ReservationId,
                command.Dto.UserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
    }

    private void SetupRoom(
        string roomId,
        RoomModel? room)
    {
        _unitOfWork.RoomRepository
            .GetByIdAsync(
                roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(room));
    }

    private void SetupOccupyingCount(
        AddReservationAttendeeCommand command,
        int occupying)
    {
        _unitOfWork.ReservationAttendeeRepository
            .CountOccupyingByReservationAsync(
                command.ReservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(occupying));
    }

    // ---------------------------------------------------------------
    // Helpers: commands e entidades
    // ---------------------------------------------------------------

    private static AddReservationAttendeeCommand NewCommand(
        string? reservationId = null,
        string? requesterUserId = null,
        string? attendeeUserId = null,
        AttendeeRoleEnum role = AttendeeRoleEnum.Required)
    {
        return new AddReservationAttendeeCommand(
            ReservationId: reservationId ?? Guid.NewGuid().ToString(),
            RequesterUserId: requesterUserId ?? Guid.NewGuid().ToString(),
            Dto: new AddReservationAttendeeDto
            {
                UserId = attendeeUserId ?? Guid.NewGuid().ToString(),
                Role = role
            });
    }

    private static ReservationModel NewReservation(
        string userId,
        string roomId,
        ReservationStatusEnum status = ReservationStatusEnum.Confirmed,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        DateTimeOffset start =
            startsAt ?? DateTimeOffset.UtcNow.AddDays(3);

        return new ReservationModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            RoomId = roomId,
            CategoryId = Guid.NewGuid().ToString(),
            Title = "Integration Test Reservation",
            StartsAt = start,
            EndsAt = endsAt ?? start.AddHours(1),
            Status = status,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static UserModel NewUser(
        string userId,
        bool isActive = true)
    {
        return new UserModel
        {
            Id = userId,
            UserName = $"user-{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.com",
            FullName = "Attendee Test User",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            Permissions = []
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
            Name = "Integration Test Room",
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
    // Helpers: verificações
    // ---------------------------------------------------------------

    private async Task VerifyNoRepositoryInteractions()
    {
        await _unitOfWork.ReservationRepository
            .DidNotReceive()
            .FindByIdWithForUpdateAsync(
                Arg.Any<string>(),
                null,
                Arg.Any<CancellationToken>());

        await _unitOfWork.UserRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<string>());

        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .ExistsByReservationAndUserAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork.RoomRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .CountOccupyingByReservationAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await VerifyAddAsyncWasNotCalled();
    }

    private async Task VerifyDuplicateCheckWasNotCalled()
    {
        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .ExistsByReservationAndUserAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyAddAsyncWasNotCalled()
    {
        await _unitOfWork.ReservationAttendeeRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<ReservationAttendeeModel>(),
                Arg.Any<CancellationToken>());
    }
}