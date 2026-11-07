using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reservations.Reservation.Services;

public sealed class CreateReservationUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReservationRepository _reservationRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomBlockoutRepository _roomBlockoutRepository;
    private readonly IRoomOperatingHourRepository _roomOperatingHourRepository;

    private readonly CreateReservationUseCase _sut;

    public CreateReservationUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _reservationRepository = Substitute.For<IReservationRepository>();
        _roomRepository = Substitute.For<IRoomRepository>();
        _roomBlockoutRepository = Substitute.For<IRoomBlockoutRepository>();
        _roomOperatingHourRepository = Substitute.For<IRoomOperatingHourRepository>();

        _unitOfWork.ReservationRepository.Returns(_reservationRepository);
        _unitOfWork.RoomRepository.Returns(_roomRepository);
        _unitOfWork.RoomBlockoutRepository.Returns(_roomBlockoutRepository);
        _unitOfWork.RoomOperatingHourRepository.Returns(_roomOperatingHourRepository);

        _sut = new CreateReservationUseCase(_unitOfWork);
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
            "Reservation command is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var command = new CreateReservationCommand
        {
            Dto = null!,
            UserId = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Reservation data is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(userId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Reservation user ID is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(userId: "invalid-user-id");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Reservation user ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // 2. Validação do DTO
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(roomId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Room ID is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(roomId: "invalid-room-id");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryIdIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(categoryId: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Category ID is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(categoryId: "invalid-category-id");

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Category ID must be a valid UUID.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTitleIsEmpty_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(title: string.Empty);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Reservation title is required.");

        await VerifyNoRepositoryInteractions();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTitleExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(title: new string('A', 151));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Reservation title must be between 1 and 150 characters.");

        await VerifyNoRepositoryInteractions();
    }

    // ---------------------------------------------------------------
    // 3. Validação do período
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenStartsAtIsDefault_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            startsAt: DateTimeOffset.MinValue);

        SetupExistingCategory(command);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Reservation start and end dates are required.");

        await VerifyOnlyCategoryWasRead(command);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEndsAtIsDefault_ReturnsValidationFailure()
    {
        // Arrange
        var command = NewCommand(
            endsAt: DateTimeOffset.MinValue);

        SetupExistingCategory(command);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Reservation start and end dates are required.");

        await VerifyOnlyCategoryWasRead(command);
    }

    // ---------------------------------------------------------------
    // 4. Validação da categoria
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        SetupCategory(command, category: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain("Category not found.");

        var categoryRepository = _unitOfWork.CategoryRepository;

        await categoryRepository.Received(1).GetByIdAsync(
            command.Dto.CategoryId,
            Arg.Any<CancellationToken>());

        await VerifyNoCallsAfterCategoryLookup();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryIsInactive_ReturnsBadRequest()
    {
        // Arrange
        var command = NewCommand();

        var category = NewCategory(
            command.Dto.CategoryId,
            isActive: false);

        SetupCategory(command, category);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Category is not active.");

        var categoryRepository = _unitOfWork.CategoryRepository;

        await categoryRepository.Received(1).GetByIdAsync(
            command.Dto.CategoryId,
            Arg.Any<CancellationToken>());

        await VerifyNoCallsAfterCategoryLookup();
    }

    // ---------------------------------------------------------------
    // 5. Idempotência
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyAlreadyExists_ReturnsExistingReservation()
    {
        // Arrange
        var command = NewCommand();

        var existingReservation = NewReservation(
            userId: command.UserId,
            roomId: command.Dto.RoomId,
            categoryId: command.Dto.CategoryId);

        SetupExistingCategory(command);
        SetupExistingReservationByIdempotencyKey(
            command,
            existingReservation);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(existingReservation);

        var categoryRepository = _unitOfWork.CategoryRepository;

        await categoryRepository.Received(1).GetByIdAsync(
            command.Dto.CategoryId,
            Arg.Any<CancellationToken>());

        await _reservationRepository.Received(1)
            .GetByIdempotencyKeyAsync(
                command.UserId,
                command.Dto.IdempotencyKey!,
                Arg.Any<CancellationToken>());

        await _roomRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _roomOperatingHourRepository.DidNotReceive()
            .GetByRoomIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository.DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyIsEmpty_SkipsIdempotencyLookup()
    {
        // Arrange
        var command = NewCommand(idempotencyKey: string.Empty);

        SetupSuccessfulDependencies(command);

        _reservationRepository
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        await _reservationRepository.DidNotReceive()
            .GetByIdempotencyKeyAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.Received(1)
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 6. Validação da sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var command = NewCommand();

        SetupExistingCategory(command);
        SetupIdempotencyNotFound(command);
        SetupRoom(command, room: null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain("Room doesn't exist.");

        await _roomRepository.Received(1).GetByIdAsync(
            command.Dto.RoomId,
            Arg.Any<CancellationToken>());

        await VerifyNoCallsAfterRoomLookup();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIsInactive_ReturnsBadRequest()
    {
        // Arrange
        var command = NewCommand();

        var room = NewRoom(
            command.Dto.RoomId,
            isActive: false);

        SetupValidDependenciesBeforeConflictCheck(command, room);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Room is not active.");

        await VerifyNoCallsAfterRoomLookup();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIsNotReservable_ReturnsBadRequest()
    {
        // Arrange
        var command = NewCommand();

        var room = NewRoom(
            command.Dto.RoomId,
            isReservable: false);

        SetupValidDependenciesBeforeConflictCheck(command, room);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room is not available for reservations.");

        await VerifyNoCallsAfterRoomLookup();
    }

    // ---------------------------------------------------------------
    // 7. Horários de funcionamento da sala
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomHasNoOperatingHours_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();
        var room = NewRoom(command.Dto.RoomId);

        SetupValidDependenciesBeforeConflictCheck(
            command,
            room,
            Array.Empty<RoomOperatingHourModel>());

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room is closed or outside its operating hours during this period.");

        await _roomOperatingHourRepository.Received(1)
            .GetByRoomIdAsync(
                command.Dto.RoomId,
                Arg.Any<CancellationToken>());

        await VerifyNoCallsAfterOperatingHoursValidation();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperatingHoursAreInactive_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();
        var room = NewRoom(command.Dto.RoomId);

        var operatingHours = Enum.GetValues<DayOfWeek>()
            .Select(day => NewOperatingHour(
                command.Dto.RoomId,
                day,
                new TimeOnly(8, 0),
                new TimeOnly(18, 0),
                isActive: false))
            .ToArray();

        SetupValidDependenciesBeforeConflictCheck(
            command,
            room,
            operatingHours);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room is closed or outside its operating hours during this period.");

        await _roomOperatingHourRepository.Received(1)
            .GetByRoomIdAsync(
                command.Dto.RoomId,
                Arg.Any<CancellationToken>());

        await VerifyNoCallsAfterOperatingHoursValidation();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIsClosedOnReservationDay_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();
        var room = NewRoom(command.Dto.RoomId);

        var reservationDay = command.Dto.StartsAt.DayOfWeek;

        var operatingHours = new[]
        {
            NewOperatingHour(
                command.Dto.RoomId,
                reservationDay,
                TimeOnly.MinValue,
                TimeOnly.MinValue,
                isClosed: true)
        };

        SetupValidDependenciesBeforeConflictCheck(
            command,
            room,
            operatingHours);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room is closed or outside its operating hours during this period.");

        await _roomOperatingHourRepository.Received(1)
            .GetByRoomIdAsync(
                command.Dto.RoomId,
                Arg.Any<CancellationToken>());

        await VerifyNoCallsAfterOperatingHoursValidation();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationStartsBeforeOpening_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();
        var room = NewRoom(command.Dto.RoomId);

        var reservationDay = command.Dto.StartsAt.DayOfWeek;

        var operatingHours = new[]
        {
            NewOperatingHour(
                command.Dto.RoomId,
                reservationDay,
                new TimeOnly(10, 0),
                new TimeOnly(18, 0))
        };

        SetupValidDependenciesBeforeConflictCheck(
            command,
            room,
            operatingHours);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room is closed or outside its operating hours during this period.");

        await _roomOperatingHourRepository.Received(1)
            .GetByRoomIdAsync(
                command.Dto.RoomId,
                Arg.Any<CancellationToken>());

        await VerifyNoCallsAfterOperatingHoursValidation();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationEndsAfterClosing_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();
        var room = NewRoom(command.Dto.RoomId);

        var reservationDay = command.Dto.StartsAt.DayOfWeek;

        // Default reservation is from 09:00 to 10:00.
        // The room closes at 09:30.
        var operatingHours = new[]
        {
            NewOperatingHour(
                command.Dto.RoomId,
                reservationDay,
                new TimeOnly(8, 0),
                new TimeOnly(9, 30))
        };

        SetupValidDependenciesBeforeConflictCheck(
            command,
            room,
            operatingHours);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room is closed or outside its operating hours during this period.");

        await _roomOperatingHourRepository.Received(1)
            .GetByRoomIdAsync(
                command.Dto.RoomId,
                Arg.Any<CancellationToken>());

        await VerifyNoCallsAfterOperatingHoursValidation();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationCrossesOperatingHoursGap_ReturnsConflict()
    {
        // Arrange
        DateTimeOffset baseDate = DateTimeOffset.UtcNow.AddDays(3);
        var start = new DateTimeOffset(
            baseDate.Year,
            baseDate.Month,
            baseDate.Day,
            11,
            0,
            0,
            baseDate.Offset);

        var command = NewCommand(
            startsAt: start,
            endsAt: start.AddHours(3));

        var room = NewRoom(command.Dto.RoomId);
        var reservationDay = command.Dto.StartsAt.DayOfWeek;

        var operatingHours = new[]
        {
            NewOperatingHour(
                command.Dto.RoomId,
                reservationDay,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0)),

            NewOperatingHour(
                command.Dto.RoomId,
                reservationDay,
                new TimeOnly(13, 0),
                new TimeOnly(18, 0))
        };

        SetupValidDependenciesBeforeConflictCheck(
            command,
            room,
            operatingHours);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room is closed or outside its operating hours during this period.");

        await _roomOperatingHourRepository.Received(1)
            .GetByRoomIdAsync(
                command.Dto.RoomId,
                Arg.Any<CancellationToken>());

        await VerifyNoCallsAfterOperatingHoursValidation();
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationFitsSecondOperatingPeriod_CreatesReservation()
    {
        // Arrange
        DateTimeOffset baseDate = DateTimeOffset.UtcNow.AddDays(3);
        var start = new DateTimeOffset(
            baseDate.Year,
            baseDate.Month,
            baseDate.Day,
            14,
            0,
            0,
            baseDate.Offset);

        var command = NewCommand(
            startsAt: start,
            endsAt: start.AddHours(1));

        var room = NewRoom(command.Dto.RoomId);
        var reservationDay = command.Dto.StartsAt.DayOfWeek;

        var operatingHours = new[]
        {
            NewOperatingHour(
                command.Dto.RoomId,
                reservationDay,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0)),

            NewOperatingHour(
                command.Dto.RoomId,
                reservationDay,
                new TimeOnly(13, 0),
                new TimeOnly(18, 0))
        };

        SetupSuccessfulDependencies(
            command,
            room,
            operatingHours: operatingHours);

        _reservationRepository
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        result.Value!.StartsAt.Should().Be(command.Dto.StartsAt);
        result.Value.EndsAt.Should().Be(command.Dto.EndsAt);

        await _roomOperatingHourRepository.Received(1)
            .GetByRoomIdAsync(
                command.Dto.RoomId,
                Arg.Any<CancellationToken>());

        await _reservationRepository.Received(1)
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 8. Conflitos de reservas
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReservationAlreadyExistsInPeriod_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();

        var room = NewRoom(command.Dto.RoomId);

        SetupValidDependenciesBeforeConflictCheck(command, room);

        _reservationRepository
            .ExistsConflictingByRoomAsync(
                command.Dto.RoomId,
                command.Dto.StartsAt,
                command.Dto.EndsAt,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room already has a reservation during this period.");

        await _reservationRepository.Received(1)
            .ExistsConflictingByRoomAsync(
                command.Dto.RoomId,
                command.Dto.StartsAt,
                command.Dto.EndsAt,
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository.DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await VerifyAddAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // 9. Conflitos de bloqueios
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomHasActiveBlockout_ReturnsConflict()
    {
        // Arrange
        var command = NewCommand();

        var room = NewRoom(command.Dto.RoomId);

        var blockout = NewRoomBlockout(
            command.Dto.RoomId,
            RoomBlockoutStatus.Scheduled);

        SetupValidDependenciesBeforeConflictCheck(command, room);
        SetupNoReservationConflict(command);

        SetupOverlappingBlockouts(
            command,
            [blockout]);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.Errors.Should().Contain(
            "Room is blocked during this period.");

        await _roomBlockoutRepository.Received(1)
            .GetOverlappingAsync(
                command.Dto.RoomId,
                command.Dto.StartsAt,
                command.Dto.EndsAt,
                Arg.Any<CancellationToken>());

        await VerifyAddAsyncWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyCancelledBlockoutsOverlap_CreatesReservation()
    {
        // Arrange
        var command = NewCommand();

        var room = NewRoom(command.Dto.RoomId);

        var cancelledBlockout = NewRoomBlockout(
            command.Dto.RoomId,
            RoomBlockoutStatus.Cancelled);

        SetupValidDependenciesBeforeConflictCheck(command, room);
        SetupNoReservationConflict(command);

        SetupOverlappingBlockouts(
            command,
            [cancelledBlockout]);

        _reservationRepository
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        result.Value!.RoomId.Should().Be(command.Dto.RoomId);

        await _roomBlockoutRepository.Received(1)
            .GetOverlappingAsync(
                command.Dto.RoomId,
                command.Dto.StartsAt,
                command.Dto.EndsAt,
                Arg.Any<CancellationToken>());

        await _reservationRepository.Received(1)
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 10. Criação da reserva
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_CreatesReservation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var command = NewCommand(
            userId: $" {userId} ",
            roomId: $" {roomId} ",
            categoryId: $" {categoryId} ",
            title: "  Team meeting  ",
            description: "  Meeting description  ",
            idempotencyKey: "  reservation-key-123  ");

        var normalizedUserId = userId.ToString();
        var normalizedRoomId = roomId.ToString();
        var normalizedCategoryId = categoryId.ToString();

        var room = NewRoom(normalizedRoomId);
        var category = NewCategory(normalizedCategoryId);

        SetupSuccessfulDependencies(
            command,
            room,
            category);

        _reservationRepository
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(command);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().NotBeNull();

        var reservation = result.Value!;

        Guid.TryParse(reservation.Id, out _).Should().BeTrue();

        reservation.UserId.Should().Be(normalizedUserId);

        reservation.RoomId.Should().Be(normalizedRoomId);
        reservation.Room.Should().BeSameAs(room);

        reservation.CategoryId.Should().Be(normalizedCategoryId);
        reservation.Category.Should().BeSameAs(category);

        reservation.Title.Should().Be("Team meeting");
        reservation.Description.Should().Be("Meeting description");

        reservation.StartsAt.Should().Be(command.Dto.StartsAt);
        reservation.EndsAt.Should().Be(command.Dto.EndsAt);

        reservation.IdempotencyKey.Should().Be("reservation-key-123");

        reservation.Status.Should().Be(ReservationStatusEnum.Confirmed);

        reservation.Version.Should().Be(1);
        reservation.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        reservation.CreatedAt.Should().BeOnOrBefore(afterExecution);
        reservation.UpdatedAt.Should().BeNull();

        var categoryRepository = _unitOfWork.CategoryRepository;

        await categoryRepository.Received(1).GetByIdAsync(
            normalizedCategoryId,
            Arg.Any<CancellationToken>());

        await _reservationRepository.Received(1)
            .GetByIdempotencyKeyAsync(
                normalizedUserId,
                "reservation-key-123",
                Arg.Any<CancellationToken>());

        await _roomRepository.Received(1).GetByIdAsync(
            normalizedRoomId,
            Arg.Any<CancellationToken>());

        await _roomOperatingHourRepository.Received(1)
            .GetByRoomIdAsync(
                normalizedRoomId,
                Arg.Any<CancellationToken>());

        await _reservationRepository.Received(1)
            .ExistsConflictingByRoomAsync(
                normalizedRoomId,
                command.Dto.StartsAt,
                command.Dto.EndsAt,
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository.Received(1)
            .GetOverlappingAsync(
                normalizedRoomId,
                command.Dto.StartsAt,
                command.Dto.EndsAt,
                Arg.Any<CancellationToken>());

        await _reservationRepository.Received(1)
            .AddAsync(
                Arg.Is<ReservationModel>(entity =>
                    entity.Id == reservation.Id &&
                    entity.UserId == normalizedUserId &&
                    entity.RoomId == normalizedRoomId &&
                    entity.CategoryId == normalizedCategoryId &&
                    entity.Title == "Team meeting" &&
                    entity.Description == "Meeting description" &&
                    entity.Status == ReservationStatusEnum.Confirmed),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 11. Tratamento de exceções
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenAddAsyncFails_PropagatesException()
    {
        // Arrange
        var command = NewCommand();

        var room = NewRoom(command.Dto.RoomId);
        var category = NewCategory(command.Dto.CategoryId);

        SetupSuccessfulDependencies(command, room, category);

        var exception = new InvalidOperationException(
            "Reservation persistence failed.");

        _reservationRepository
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(exception));

        // Act
        var act = () => _sut.ExecuteAsync(command);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Reservation persistence failed.");

        await _reservationRepository.Received(1)
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>());
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

    private void SetupSuccessfulDependencies(
        CreateReservationCommand command,
        RoomModel? room = null,
        CategoryModel? category = null,
        IReadOnlyList<RoomOperatingHourModel>? operatingHours = null)
    {
        room ??= NewRoom(command.Dto.RoomId);
        category ??= NewCategory(command.Dto.CategoryId);

        SetupCategory(command, category);
        SetupIdempotencyNotFound(command);
        SetupRoom(command, room);
        SetupOperatingHours(command, operatingHours);
        SetupNoReservationConflict(command);
        SetupOverlappingBlockouts(command);
    }

    private void SetupValidDependenciesBeforeConflictCheck(
        CreateReservationCommand command,
        RoomModel room,
        IReadOnlyList<RoomOperatingHourModel>? operatingHours = null)
    {
        SetupExistingCategory(command);
        SetupIdempotencyNotFound(command);
        SetupRoom(command, room);
        SetupOperatingHours(command, operatingHours);
    }

    private void SetupExistingCategory(
        CreateReservationCommand command)
    {
        SetupCategory(
            command,
            NewCategory(command.Dto.CategoryId));
    }

    private void SetupCategory(
        CreateReservationCommand command,
        CategoryModel? category)
    {
        _unitOfWork.CategoryRepository
            .GetByIdAsync(
                command.Dto.CategoryId.Trim(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(category));
    }

    private void SetupIdempotencyNotFound(
        CreateReservationCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Dto.IdempotencyKey))
        {
            return;
        }

        _reservationRepository
            .GetByIdempotencyKeyAsync(
                command.UserId.Trim(),
                command.Dto.IdempotencyKey.Trim(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(null));
    }

    private void SetupExistingReservationByIdempotencyKey(
        CreateReservationCommand command,
        ReservationModel reservation)
    {
        _reservationRepository
            .GetByIdempotencyKeyAsync(
                command.UserId.Trim(),
                command.Dto.IdempotencyKey!.Trim(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(reservation));
    }

    private void SetupRoom(
        CreateReservationCommand command,
        RoomModel? room)
    {
        _roomRepository
            .GetByIdAsync(
                command.Dto.RoomId.Trim(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));
    }

    private void SetupOperatingHours(
        CreateReservationCommand command,
        IReadOnlyList<RoomOperatingHourModel>? operatingHours = null)
    {
        operatingHours ??= NewWeeklyOperatingHours(
            command.Dto.RoomId.Trim());

        _roomOperatingHourRepository
            .GetByRoomIdAsync(
                command.Dto.RoomId.Trim(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(operatingHours));
    }

    private void SetupNoReservationConflict(
        CreateReservationCommand command)
    {
        _reservationRepository
            .ExistsConflictingByRoomAsync(
                command.Dto.RoomId.Trim(),
                command.Dto.StartsAt,
                command.Dto.EndsAt,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
    }

    private void SetupOverlappingBlockouts(
        CreateReservationCommand command,
        IReadOnlyList<RoomBlockoutModel>? blockouts = null)
    {
        blockouts ??= Array.Empty<RoomBlockoutModel>();

        _roomBlockoutRepository
            .GetOverlappingAsync(
                command.Dto.RoomId.Trim(),
                command.Dto.StartsAt,
                command.Dto.EndsAt,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(blockouts));
    }

    // ---------------------------------------------------------------
    // Helpers: entidades
    // ---------------------------------------------------------------

    private static CreateReservationCommand NewCommand(
        string? userId = null,
        string? roomId = null,
        string? categoryId = null,
        string? title = "Team meeting",
        string? description = "Meeting description",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        string? idempotencyKey = "reservation-idempotency-key")
    {
        DateTimeOffset start =
            startsAt ?? DateTimeOffset.UtcNow.AddDays(3).Date.AddHours(9);

        return new CreateReservationCommand
        {
            UserId = userId ?? Guid.NewGuid().ToString(),

            Dto = new CreateReservationDto
            {
                RoomId = roomId ?? Guid.NewGuid().ToString(),
                CategoryId = categoryId ?? Guid.NewGuid().ToString(),
                Title = title!,
                Description = description,
                StartsAt = start,
                EndsAt = endsAt ?? start.AddHours(1),
                IdempotencyKey = idempotencyKey
            }
        };
    }

    private static RoomModel NewRoom(
        string roomId,
        bool isActive = true,
        bool isReservable = true)
    {
        return new RoomModel
        {
            Id = roomId,
            RoomTypeId = Guid.NewGuid().ToString(),
            Code = $"R-{Guid.NewGuid():N}"[..10],
            Name = "Integration Test Room",
            Description = "Room created for unit tests.",
            Location = "Building A",
            Capacity = 10,
            IsAccessible = false,
            IsReservable = isReservable,
            IsActive = isActive,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static CategoryModel NewCategory(
        string categoryId,
        bool isActive = true)
    {
        return new CategoryModel
        {
            Id = categoryId,
            Name = "Meeting",
            Code = "meeting-code",
            Description = "Meeting category.",
            IsActive = isActive,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static RoomBlockoutModel NewRoomBlockout(
        string roomId,
        RoomBlockoutStatus status)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new RoomBlockoutModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            Type = RoomBlockoutType.Maintenance,
            Status = status,
            Reason = "Scheduled maintenance",
            StartsAt = now.AddDays(3),
            EndsAt = now.AddDays(3).AddHours(2),
            CreatedBy = Guid.NewGuid().ToString(),
            Version = 1,
            CreatedAt = now
        };
    }

    private static ReservationModel NewReservation(
        string userId,
        string roomId,
        string categoryId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new ReservationModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            RoomId = roomId,
            CategoryId = categoryId,
            Title = "Existing reservation",
            StartsAt = now.AddDays(3),
            EndsAt = now.AddDays(3).AddHours(1),
            Status = ReservationStatusEnum.Confirmed,
            Version = 1,
            CreatedAt = now
        };
    }

    private static IReadOnlyList<RoomOperatingHourModel>
        NewWeeklyOperatingHours(string roomId)
    {
        return Enum.GetValues<DayOfWeek>()
            .Select(day => NewOperatingHour(
                roomId,
                day,
                new TimeOnly(8, 0),
                new TimeOnly(18, 0)))
            .ToArray();
    }

    private static RoomOperatingHourModel NewOperatingHour(
        string roomId,
        DayOfWeek dayOfWeek,
        TimeOnly opensAt,
        TimeOnly closesAt,
        bool isClosed = false,
        bool isActive = true)
    {
        return new RoomOperatingHourModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            DayOfWeek = dayOfWeek,
            OpensAt = opensAt,
            ClosesAt = closesAt,
            IsClosed = isClosed,
            IsActive = isActive,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    // ---------------------------------------------------------------
    // Helpers: verificações
    // ---------------------------------------------------------------

    private async Task VerifyNoRepositoryInteractions()
    {
        var categoryRepository = _unitOfWork.CategoryRepository;

        await categoryRepository.DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .GetByIdempotencyKeyAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>());

        await _roomRepository.DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository.DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _roomOperatingHourRepository.DidNotReceive()
            .GetByRoomIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyOnlyCategoryWasRead(
        CreateReservationCommand command)
    {
        var categoryRepository = _unitOfWork.CategoryRepository;

        await categoryRepository.Received(1)
            .GetByIdAsync(
                command.Dto.CategoryId,
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .GetByIdempotencyKeyAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>());

        await _roomRepository.DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository.DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _roomOperatingHourRepository.DidNotReceive()
            .GetByRoomIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyNoCallsAfterCategoryLookup()
    {
        await _reservationRepository.DidNotReceive()
            .GetByIdempotencyKeyAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>());

        await _roomRepository.DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository.DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _roomOperatingHourRepository.DidNotReceive()
            .GetByRoomIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    private async Task VerifyNoCallsAfterRoomLookup()
    {
        await _roomOperatingHourRepository.DidNotReceive()
            .GetByRoomIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository.DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository.DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await VerifyAddAsyncWasNotCalled();
    }

    private async Task VerifyNoCallsAfterOperatingHoursValidation()
    {
        await _reservationRepository.DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository.DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        await VerifyAddAsyncWasNotCalled();
    }

    private async Task VerifyAddAsyncWasNotCalled()
    {
        await _reservationRepository.DidNotReceive()
            .AddAsync(
                Arg.Any<ReservationModel>(),
                Arg.Any<CancellationToken>());
    }
}
