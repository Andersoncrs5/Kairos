using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;
using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomFeedback.Services;

public sealed class CreateRoomFeedbackUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly CreateRoomFeedbackUseCase _sut;

    private readonly string _reservationId = Guid.NewGuid().ToString();
    private readonly string _requesterUserId = Guid.NewGuid().ToString();
    private readonly string _organizerUserId = Guid.NewGuid().ToString();
    private readonly string _roomId = Guid.NewGuid().ToString();

    public CreateRoomFeedbackUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _sut = new CreateRoomFeedbackUseCase(_uow);
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCanceled()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = () => _sut.ExecuteAsync(
            CreateValidCommand(),
            cts.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _uow.ReservationRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    // ---------------------------------------------------------------
    // Command validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(null!);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.ReservationRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenReservationIdIsEmpty()
    {
        CreateRoomFeedbackCommand command = new(
            string.Empty,
            _requesterUserId,
            CreateValidDto());

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRequesterUserIdIsEmpty()
    {
        CreateRoomFeedbackCommand command = new(
            _reservationId,
            string.Empty,
            CreateValidDto());

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenDtoIsNull()
    {
        CreateRoomFeedbackCommand command = new(
            _reservationId,
            _requesterUserId,
            null!);

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
    }

    // ---------------------------------------------------------------
    // DTO validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRatingIsBelowMinimum()
    {
        CreateRoomFeedbackDto dto = new()
        {
            Rating = 0
        };

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand(dto));

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRatingIsAboveMaximum()
    {
        CreateRoomFeedbackDto dto = new()
        {
            Rating = 6
        };

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand(dto));

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommentExceedsMaximumLength()
    {
        CreateRoomFeedbackDto dto = new()
        {
            Rating = 5,
            Comment = new string('A', 1001)
        };

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand(dto));

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenIssueTypeIsInvalid()
    {
        CreateRoomFeedbackDto dto = new()
        {
            Rating = 5,
            IssueType = (RoomFeedbackIssueTypeEnum)999
        };

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand(dto));

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
    }

    // ---------------------------------------------------------------
    // Identifier validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenReservationIdIsNotValidUuid()
    {
        CreateRoomFeedbackCommand command = new(
            "invalid-reservation-id",
            _requesterUserId,
            CreateValidDto());

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.FirstError.Should()
            .Be("Reservation ID must be a valid UUID.");

        await _uow.ReservationRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRequesterUserIdIsNotValidUuid()
    {
        CreateRoomFeedbackCommand command = new(
            _reservationId,
            "invalid-user-id",
            CreateValidDto());

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.FirstError.Should()
            .Be("Requester user ID must be a valid UUID.");

        await _uow.ReservationRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    // ---------------------------------------------------------------
    // Reservation lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenReservationDoesNotExist()
    {
        _uow.ReservationRepository
            .GetByIdAsync(
                _reservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(null));

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.FirstError.Should().Be("Reservation not found.");

        await _uow.ReservationAttendeeRepository
            .DidNotReceiveWithAnyArgs()
            .GetByReservationAndUserAsync(default!, default!, default);

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .ExistsByReservationAndUserAsync(default!, default!, default);
    }

    // ---------------------------------------------------------------
    // Organizer authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldAllowFeedback_WhenRequesterIsOrganizer()
    {
        ReservationModel reservation = CreateReservation(
            organizerUserId: _requesterUserId);

        ConfigureReservation(reservation);
        ConfigureFeedbackDoesNotExist();

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);

        await _uow.ReservationAttendeeRepository
            .DidNotReceiveWithAnyArgs()
            .GetByReservationAndUserAsync(default!, default!, default);

        await _uow.RoomFeedbackRepository
            .Received(1)
            .AddAsync(
                Arg.Any<RoomFeedbackModel>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Attendee authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnForbidden_WhenRequesterIsNotOrganizerOrAttendee()
    {
        ReservationModel reservation = CreateReservation(
            organizerUserId: _organizerUserId);

        ConfigureReservation(reservation);

        _uow.ReservationAttendeeRepository
            .GetByReservationAndUserAsync(
                _reservationId,
                _requesterUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationAttendeeModel?>(null));

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.FirstError.Should()
            .Be("Only the organizer or attendees who accepted the invitation can review this reservation.");

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .ExistsByReservationAndUserAsync(default!, default!, default);

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, default);
    }

    [Theory]
    [InlineData(AttendeeStatusEnum.Pending)]
    [InlineData(AttendeeStatusEnum.Declined)]
    [InlineData(AttendeeStatusEnum.Tentative)]
    public async Task ExecuteAsync_ShouldReturnForbidden_WhenAttendeeHasNotAcceptedInvitation(
        AttendeeStatusEnum status)
    {
        ReservationModel reservation = CreateReservation(
            organizerUserId: _organizerUserId);

        ReservationAttendeeModel attendee = CreateAttendee(status);

        ConfigureReservation(reservation);

        _uow.ReservationAttendeeRepository
            .GetByReservationAndUserAsync(
                _reservationId,
                _requesterUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationAttendeeModel?>(attendee));

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .ExistsByReservationAndUserAsync(default!, default!, default);

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowFeedback_WhenAttendeeAcceptedInvitation()
    {
        ReservationModel reservation = CreateReservation(
            organizerUserId: _organizerUserId);

        ReservationAttendeeModel attendee = CreateAttendee(
            AttendeeStatusEnum.Accepted);

        ConfigureReservation(reservation);

        _uow.ReservationAttendeeRepository
            .GetByReservationAndUserAsync(
                _reservationId,
                _requesterUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationAttendeeModel?>(attendee));

        ConfigureFeedbackDoesNotExist();

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);

        await _uow.ReservationAttendeeRepository
            .Received(1)
            .GetByReservationAndUserAsync(
                _reservationId,
                _requesterUserId,
                Arg.Any<CancellationToken>());

        await _uow.RoomFeedbackRepository
            .Received(1)
            .AddAsync(
                Arg.Any<RoomFeedbackModel>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Reservation status
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenReservationIsCancelled()
    {
        ReservationModel reservation = CreateReservation(
            organizerUserId: _requesterUserId,
            status: ReservationStatusEnum.Cancelled);

        ConfigureReservation(reservation);

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.FirstError.Should()
            .Be("Cancelled reservations cannot be reviewed.");

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .ExistsByReservationAndUserAsync(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenReservationHasNotEnded()
    {
        ReservationModel reservation = CreateReservation(
            organizerUserId: _requesterUserId,
            endsAt: DateTimeOffset.UtcNow.AddMinutes(30));

        ConfigureReservation(reservation);

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.FirstError.Should()
            .Be("Reservation has not ended yet.");

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .ExistsByReservationAndUserAsync(default!, default!, default);
    }

    // ---------------------------------------------------------------
    // Duplicate feedback
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenRequesterAlreadyReviewedReservation()
    {
        ReservationModel reservation = CreateReservation(
            organizerUserId: _requesterUserId);

        ConfigureReservation(reservation);

        _uow.RoomFeedbackRepository
            .ExistsByReservationAndUserAsync(
                _reservationId,
                _requesterUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        result.FirstError.Should()
            .Be("You have already reviewed this reservation.");

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, default);
    }

    // ---------------------------------------------------------------
    // Feedback creation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldCreateFeedback_WhenAllValidationsPass()
    {
        ReservationModel reservation = CreateReservation(
            organizerUserId: _requesterUserId);

        CreateRoomFeedbackDto dto = new()
        {
            Rating = 5,
            Comment = "The room was clean and comfortable."
        };

        ConfigureReservation(reservation);
        ConfigureFeedbackDoesNotExist();

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand(dto));

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value.Should().NotBeNull();

        RoomFeedbackModel feedback = result.Value!;

        feedback.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(feedback.Id, out _).Should().BeTrue();

        feedback.ReservationId.Should().Be(_reservationId);
        feedback.RoomId.Should().Be(_roomId);
        feedback.UserId.Should().Be(_requesterUserId);
        feedback.Rating.Should().Be(5);
        feedback.Comment.Should().Be(dto.Comment);

        await _uow.RoomFeedbackRepository
            .Received(1)
            .AddAsync(
                Arg.Is<RoomFeedbackModel>(entity =>
                    entity.ReservationId == _reservationId &&
                    entity.RoomId == _roomId &&
                    entity.UserId == _requesterUserId &&
                    entity.Rating == 5 &&
                    entity.Comment == dto.Comment),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateFeedbackWithoutComment_WhenCommentIsNull()
    {
        ReservationModel reservation = CreateReservation(
            organizerUserId: _requesterUserId);

        CreateRoomFeedbackDto dto = new()
        {
            Rating = 4,
            Comment = null
        };

        ConfigureReservation(reservation);
        ConfigureFeedbackDoesNotExist();

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand(dto));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Rating.Should().Be(4);
        result.Value.Comment.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Cancellation token propagation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateCancellationTokenToRepositories()
    {
        using CancellationTokenSource cts = new();

        CancellationToken ct = cts.Token;

        ReservationModel reservation = CreateReservation(
            organizerUserId: _requesterUserId);

        _uow.ReservationRepository
            .GetByIdAsync(_reservationId, ct)
            .Returns(Task.FromResult<ReservationModel?>(reservation));

        _uow.RoomFeedbackRepository
            .ExistsByReservationAndUserAsync(
                _reservationId,
                _requesterUserId,
                ct)
            .Returns(Task.FromResult(false));

        Result<RoomFeedbackModel> result =
            await _sut.ExecuteAsync(CreateValidCommand(), ct);

        result.IsSuccess.Should().BeTrue();

        await _uow.ReservationRepository
            .Received(1)
            .GetByIdAsync(_reservationId, ct);

        await _uow.RoomFeedbackRepository
            .Received(1)
            .ExistsByReservationAndUserAsync(
                _reservationId,
                _requesterUserId,
                ct);

        await _uow.RoomFeedbackRepository
            .Received(1)
            .AddAsync(
                Arg.Any<RoomFeedbackModel>(),
                ct);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private CreateRoomFeedbackCommand CreateValidCommand(
        CreateRoomFeedbackDto? dto = null)
    {
        return new CreateRoomFeedbackCommand(
            _reservationId,
            _requesterUserId,
            dto ?? CreateValidDto());
    }

    private static CreateRoomFeedbackDto CreateValidDto()
    {
        return new CreateRoomFeedbackDto
        {
            Rating = 5,
            Comment = "Excellent room."
        };
    }

    private ReservationModel CreateReservation(
        string? organizerUserId = null,
        DateTimeOffset? endsAt = null,
        ReservationStatusEnum? status = null)
    {
        ReservationModel reservation =
            Activator.CreateInstance<ReservationModel>();

        reservation.UserId = organizerUserId ?? _requesterUserId;
        reservation.RoomId = _roomId;
        reservation.Status = status ?? (ReservationStatusEnum)int.MaxValue;
        reservation.EndsAt = endsAt ?? DateTimeOffset.UtcNow.AddMinutes(-30);

        return reservation;
    }

    private ReservationAttendeeModel CreateAttendee(
        AttendeeStatusEnum status)
    {
        ReservationAttendeeModel attendee =
            Activator.CreateInstance<ReservationAttendeeModel>();

        attendee.ReservationId = _reservationId;
        attendee.UserId = _requesterUserId;
        attendee.Status = status;

        return attendee;
    }

    private void ConfigureReservation(ReservationModel reservation)
    {
        _uow.ReservationRepository
            .GetByIdAsync(
                _reservationId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ReservationModel?>(reservation));
    }

    private void ConfigureFeedbackDoesNotExist()
    {
        _uow.RoomFeedbackRepository
            .ExistsByReservationAndUserAsync(
                _reservationId,
                _requesterUserId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
    }
}