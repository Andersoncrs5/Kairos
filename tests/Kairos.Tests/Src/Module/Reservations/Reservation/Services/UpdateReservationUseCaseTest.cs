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
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Reservations.Reservation.Services;

public sealed class UpdateReservationUseCaseTest
{
    private static readonly string ReservationId =
        Guid.NewGuid().ToString();

    private static readonly string UserId =
        Guid.NewGuid().ToString();

    private static readonly string ExistingRoomId =
        Guid.NewGuid().ToString();

    private static readonly string ExistingCategoryId =
        Guid.NewGuid().ToString();

    private static readonly DateTimeOffset ExistingStartsAt =
        new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExistingEndsAt =
        new(2026, 11, 10, 11, 0, 0, TimeSpan.Zero);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IReservationRepository _reservationRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomBlockoutRepository _roomBlockoutRepository;

    private readonly UpdateReservationUseCase _sut;

    public UpdateReservationUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _reservationRepository =
            Substitute.For<IReservationRepository>();

        _roomRepository =
            Substitute.For<IRoomRepository>();

        _roomBlockoutRepository =
            Substitute.For<IRoomBlockoutRepository>();

        _unitOfWork.ReservationRepository.Returns(_reservationRepository);
        _unitOfWork.RoomRepository.Returns(_roomRepository);
        _unitOfWork.RoomBlockoutRepository.Returns(_roomBlockoutRepository);

        _sut = new UpdateReservationUseCase(_unitOfWork);
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
        var command = new UpdateReservationCommand(
            ReservationId,
            null!);

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _reservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationIdIsInvalid_ReturnsValidationFailure()
    {
        var command = NewCommand(reservationId: "invalid-id");

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _reservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        _reservationRepository.GetByIdAsync(
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

    // ---------------------------------------------------------------
    // 2. Room validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdIsEmpty_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                RoomId = string.Empty
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _roomRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdIsInvalid_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                RoomId = "invalid-room-id"
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _roomRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewRoomDoesNotExist_ReturnsNotFound()
    {
        ArrangeExistingReservation();

        string newRoomId = Guid.NewGuid().ToString();

        _roomRepository.GetByIdAsync(
                newRoomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(null));

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                RoomId = newRoomId
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _roomRepository
            .Received(1)
            .GetByIdAsync(
                newRoomId,
                Arg.Any<CancellationToken>());

        await _reservationRepository
            .DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewRoomIsInactive_ReturnsBadRequest()
    {
        ArrangeExistingReservation();

        RoomModel inactiveRoom = NewRoom(isActive: false);

        SetupRoom(inactiveRoom);

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                RoomId = inactiveRoom.Id
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewRoomIsNotReservable_ReturnsBadRequest()
    {
        ArrangeExistingReservation();

        RoomModel unavailableRoom = NewRoom(isReservable: false);

        SetupRoom(unavailableRoom);

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                RoomId = unavailableRoom.Id
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIdIsUnchanged_DoesNotFetchRoomAgain()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                RoomId = reservation.RoomId
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        await _roomRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _reservationRepository
            .DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomChanges_UpdatesRoomAndChecksConflicts()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        RoomModel newRoom = NewRoom();

        SetupRoom(newRoom);
        SetupNoConflicts();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                RoomId = newRoom.Id
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        result.Value!.RoomId.Should().Be(newRoom.Id);
        result.Value.Room.Should().BeSameAs(newRoom);

        await _roomRepository
            .Received(1)
            .GetByIdAsync(
                newRoom.Id,
                Arg.Any<CancellationToken>());

        await _reservationRepository
            .Received(1)
            .ExistsConflictingByRoomAsync(
                newRoom.Id,
                ExistingStartsAt,
                ExistingEndsAt,
                reservation.Id,
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository
            .Received(1)
            .GetOverlappingAsync(
                newRoom.Id,
                ExistingStartsAt,
                ExistingEndsAt,
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 3. Category validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCategoryIdIsEmpty_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                CategoryId = string.Empty
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _unitOfWork.CategoryRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryIdIsInvalid_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                CategoryId = "invalid-category-id"
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _unitOfWork.CategoryRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewCategoryDoesNotExist_ReturnsNotFound()
    {
        ArrangeExistingReservation();

        string newCategoryId = Guid.NewGuid().ToString();

        _unitOfWork.CategoryRepository.GetByIdAsync(
                newCategoryId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(null));

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                CategoryId = newCategoryId
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _unitOfWork.CategoryRepository
            .Received(1)
            .GetByIdAsync(
                newCategoryId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewCategoryIsInactive_ReturnsBadRequest()
    {
        ArrangeExistingReservation();

        CategoryModel inactiveCategory = NewCategory(isActive: false);

        SetupCategory(inactiveCategory);

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                CategoryId = inactiveCategory.Id
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryChanges_UpdatesCategory()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        CategoryModel newCategory = NewCategory();

        SetupCategory(newCategory);

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                CategoryId = newCategory.Id
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        result.Value!.CategoryId.Should().Be(newCategory.Id);
        result.Value.Category.Should().BeSameAs(newCategory);

        await _unitOfWork.CategoryRepository
            .Received(1)
            .GetByIdAsync(
                newCategory.Id,
                Arg.Any<CancellationToken>());

        // Changing only the category does not change room availability.
        await _reservationRepository
            .DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryIdIsUnchanged_DoesNotFetchCategoryAgain()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                CategoryId = reservation.CategoryId
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        await _unitOfWork.CategoryRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 4. Reservation period validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenStartsAtIsDefault_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                StartsAt = DateTimeOffset.MinValue
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenEndsAtIsDefault_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                EndsAt = DateTimeOffset.MinValue
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartIsAfterEnd_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                StartsAt = ExistingEndsAt,
                EndsAt = ExistingStartsAt
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartEqualsEnd_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                StartsAt = ExistingStartsAt,
                EndsAt = ExistingStartsAt
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyStartsAtChanges_UsesExistingEndDateForConflictCheck()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        SetupNoConflicts();

        DateTimeOffset newStartsAt =
            new(2026, 11, 10, 9, 30, 0, TimeSpan.Zero);

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                StartsAt = newStartsAt
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        result.Value!.StartsAt.Should().Be(newStartsAt);
        result.Value.EndsAt.Should().Be(ExistingEndsAt);

        await _reservationRepository
            .Received(1)
            .ExistsConflictingByRoomAsync(
                ExistingRoomId,
                newStartsAt,
                ExistingEndsAt,
                reservation.Id,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyEndsAtChanges_UsesExistingStartDateForConflictCheck()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        SetupNoConflicts();

        DateTimeOffset newEndsAt =
            new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                EndsAt = newEndsAt
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        result.Value!.StartsAt.Should().Be(ExistingStartsAt);
        result.Value.EndsAt.Should().Be(newEndsAt);

        await _reservationRepository
            .Received(1)
            .ExistsConflictingByRoomAsync(
                ExistingRoomId,
                ExistingStartsAt,
                newEndsAt,
                reservation.Id,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationPeriodChangesAndConflicts_ReturnsConflict()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        DateTimeOffset newStartsAt =
            new(2026, 11, 10, 9, 30, 0, TimeSpan.Zero);

        _reservationRepository.ExistsConflictingByRoomAsync(
                ExistingRoomId,
                newStartsAt,
                ExistingEndsAt,
                reservation.Id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                StartsAt = newStartsAt
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _reservationRepository
            .Received(1)
            .ExistsConflictingByRoomAsync(
                ExistingRoomId,
                newStartsAt,
                ExistingEndsAt,
                reservation.Id,
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 5. Room blockout conflicts
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomHasActiveBlockout_ReturnsConflict()
    {
        ArrangeExistingReservation();

        SetupNoReservationConflict();

        RoomBlockoutModel blockout =
            NewBlockout(RoomBlockoutStatus.Scheduled);

        SetupBlockouts(blockout);

        DateTimeOffset newStartsAt =
            new(2026, 11, 10, 9, 30, 0, TimeSpan.Zero);

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                StartsAt = newStartsAt
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _roomBlockoutRepository
            .Received(1)
            .GetOverlappingAsync(
                ExistingRoomId,
                newStartsAt,
                ExistingEndsAt,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenOverlappingBlockoutIsCancelled_AllowsUpdate()
    {
        ArrangeExistingReservation();

        SetupNoReservationConflict();

        RoomBlockoutModel cancelledBlockout =
            NewBlockout(RoomBlockoutStatus.Cancelled);

        SetupBlockouts(cancelledBlockout);

        DateTimeOffset newStartsAt =
            new(2026, 11, 10, 9, 30, 0, TimeSpan.Zero);

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                StartsAt = newStartsAt
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.StartsAt.Should().Be(newStartsAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomAndPeriodChange_ChecksConflictsUsingNewValues()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        RoomModel newRoom = NewRoom();

        SetupRoom(newRoom);
        SetupNoConflicts();

        DateTimeOffset newStartsAt =
            new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

        DateTimeOffset newEndsAt =
            new(2026, 11, 10, 14, 0, 0, TimeSpan.Zero);

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                RoomId = newRoom.Id,
                StartsAt = newStartsAt,
                EndsAt = newEndsAt
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        result.Value!.RoomId.Should().Be(newRoom.Id);
        result.Value.StartsAt.Should().Be(newStartsAt);
        result.Value.EndsAt.Should().Be(newEndsAt);

        await _reservationRepository
            .Received(1)
            .ExistsConflictingByRoomAsync(
                newRoom.Id,
                newStartsAt,
                newEndsAt,
                reservation.Id,
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository
            .Received(1)
            .GetOverlappingAsync(
                newRoom.Id,
                newStartsAt,
                newEndsAt,
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // 6. Title and description
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenTitleExceedsLimit_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                Title = new string('A', 151)
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _reservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTitleIsEmpty_ReturnsValidationFailure()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                Title = "   "
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTitleAndDescriptionAreUpdated_NormalizesValues()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                Title = "  Architecture meeting  ",
                Description = "  Discuss system design  "
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        result.Value!.Title.Should().Be("Architecture meeting");
        result.Value.Description.Should().Be("Discuss system design");

        result.Value.RoomId.Should().Be(reservation.RoomId);
        result.Value.CategoryId.Should().Be(reservation.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDescriptionIsWhitespace_ClearsDescription()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                Description = "   "
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Description.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDescriptionIsNull_KeepsExistingDescription()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        string? originalDescription = reservation.Description;

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                Description = null
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Description.Should().Be(originalDescription);
    }

    // ---------------------------------------------------------------
    // 7. Partial update and audit fields
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenNoFieldsAreChanged_ReturnsExistingReservation()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        var command = NewCommand();

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(reservation);

        await _reservationRepository
            .DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyTitleChanges_DoesNotCheckScheduleConflicts()
    {
        ArrangeExistingReservation();

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                Title = "Updated meeting"
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Updated meeting");

        await _reservationRepository
            .DidNotReceive()
            .ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _roomBlockoutRepository
            .DidNotReceive()
            .GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUpdateSucceeds_IncrementsVersionAndUpdatesTimestamp()
    {
        ReservationModel reservation = ArrangeExistingReservation();

        DateTimeOffset previousUpdatedAt =
            new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

        reservation.UpdatedAt = previousUpdatedAt;

        long previousVersion = reservation.Version;

        var command = NewCommand(
            dto: new UpdateReservationDto
            {
                Title = "Updated meeting"
            });

        var result = await _sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();

        result.Value!.Version.Should().Be(previousVersion + 1);
        result.Value.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationTokenIsCancelled_ThrowsOperationCanceledException()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        var act = async () => await _sut.ExecuteAsync(
            NewCommand(),
            cancellationTokenSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        await _reservationRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private ReservationModel ArrangeExistingReservation()
    {
        ReservationModel reservation = NewReservation();

        _reservationRepository.GetByIdAsync(
                reservation.Id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(reservation));

        return reservation;
    }

    private void SetupRoom(RoomModel room)
    {
        _roomRepository.GetByIdAsync(
                room.Id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));
    }

    private void SetupCategory(CategoryModel category)
    {
        _unitOfWork.CategoryRepository.GetByIdAsync(
                category.Id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CategoryModel?>(category));
    }

    private void SetupNoConflicts()
    {
        SetupNoReservationConflict();
        SetupBlockouts();
    }

    private void SetupNoReservationConflict()
    {
        _reservationRepository.ExistsConflictingByRoomAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
    }

    private void SetupBlockouts(params RoomBlockoutModel[] blockouts)
    {
        _roomBlockoutRepository.GetOverlappingAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<RoomBlockoutModel>>(blockouts));
    }

    private static UpdateReservationCommand NewCommand(
        string? reservationId = null,
        UpdateReservationDto? dto = null)
    {
        return new UpdateReservationCommand(
            reservationId ?? ReservationId,
            dto ?? new UpdateReservationDto());
    }

    private static ReservationModel NewReservation()
    {
        RoomModel room = NewRoom(ExistingRoomId);
        CategoryModel category = NewCategory(ExistingCategoryId);

        return new ReservationModel
        {
            Id = ReservationId,
            RoomId = room.Id,
            Room = room,
            UserId = UserId,
            CategoryId = category.Id,
            Category = category,
            Title = "Architecture meeting",
            Description = "Original description",
            StartsAt = ExistingStartsAt,
            EndsAt = ExistingEndsAt,
            Status = ReservationStatusEnum.Confirmed,
            Version = 4,
            CreatedAt = ExistingStartsAt.AddDays(-5),
            UpdatedAt = null
        };
    }

    private static RoomModel NewRoom(
        string? id = null,
        bool isActive = true,
        bool isReservable = true)
    {
        return new RoomModel
        {
            Id = id ?? Guid.NewGuid().ToString(),
            RoomTypeId = Guid.NewGuid().ToString(),
            Code = "ROOM-01",
            Name = "Meeting room",
            Location = "First floor",
            Capacity = 10,
            IsAccessible = false,
            IsReservable = isReservable,
            IsActive = isActive,
            Version = 1,
            CreatedAt = ExistingStartsAt.AddDays(-10)
        };
    }

    private static CategoryModel NewCategory(
        string? id = null,
        bool isActive = true)
    {
        return new CategoryModel
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Name = "Business",
            Code = "business-code",
            Description = "Business meetings",
            IsActive = isActive,
            Version = 1,
            CreatedAt = ExistingStartsAt.AddDays(-10)
        };
    }

    private static RoomBlockoutModel NewBlockout(
        RoomBlockoutStatus status)
    {
        return new RoomBlockoutModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = ExistingRoomId,
            Type = RoomBlockoutType.Maintenance,
            Status = status,
            Reason = "Scheduled maintenance",
            Notes = "Unit test blockout",
            StartsAt = ExistingStartsAt,
            EndsAt = ExistingEndsAt,
            CreatedBy = UserId,
            Version = 1,
            CreatedAt = ExistingStartsAt.AddDays(-2)
        };
    }
}