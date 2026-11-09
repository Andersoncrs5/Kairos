using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;
using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomFeedback.Services;

public sealed class DeleteRoomFeedbackUseCaseTest
{
    private const string ManagePermission = "rooms.feedbacks.manage";

    private readonly IUnitOfWork _uow;
    private readonly DeleteRoomFeedbackUseCase _sut;

    private readonly string _feedbackId = Guid.NewGuid().ToString();
    private readonly string _requesterUserId = Guid.NewGuid().ToString();
    private readonly string _authorUserId = Guid.NewGuid().ToString();

    public DeleteRoomFeedbackUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _sut = new DeleteRoomFeedbackUseCase(_uow);
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

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);

        _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .Delete(default!);
    }

    // ---------------------------------------------------------------
    // Command validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        Result<bool> result = await _sut.ExecuteAsync(null!);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.FirstError.Should()
            .Be("Room feedback delete command is required.");

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenFeedbackIdIsEmpty()
    {
        DeleteRoomFeedbackCommand command = new(
            string.Empty,
            _requesterUserId);

        Result<bool> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRequesterUserIdIsEmpty()
    {
        DeleteRoomFeedbackCommand command = new(
            _feedbackId,
            string.Empty);

        Result<bool> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    // ---------------------------------------------------------------
    // Identifier validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenFeedbackIdIsNotValidUuid()
    {
        DeleteRoomFeedbackCommand command = new(
            "invalid-feedback-id",
            _requesterUserId);

        Result<bool> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.FirstError.Should()
            .Be("Room feedback ID must be a valid UUID.");

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRequesterUserIdIsNotValidUuid()
    {
        DeleteRoomFeedbackCommand command = new(
            _feedbackId,
            "invalid-user-id");

        Result<bool> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.FirstError.Should()
            .Be("Requester user ID must be a valid UUID.");

        await _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    // ---------------------------------------------------------------
    // Feedback lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenFeedbackDoesNotExist()
    {
        _uow.RoomFeedbackRepository
            .GetByIdAsync(
                _feedbackId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomFeedbackModel?>(null));

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.FirstError.Should().Be("Room feedback not found.");

        await _uow.UserRepository
            .DidNotReceiveWithAnyArgs()
            .ExistsPermission(default!, default!);

        _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .Delete(default!);
    }

    // ---------------------------------------------------------------
    // Author authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteFeedback_WhenRequesterIsAuthor()
    {
        RoomFeedbackModel feedback = CreateFeedback(
            userId: _requesterUserId);

        ConfigureFeedback(feedback);

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();

        _uow.RoomFeedbackRepository
            .Received(1)
            .Delete(
                Arg.Is<RoomFeedbackModel>(entity =>
                    entity.Id == _feedbackId &&
                    entity.UserId == _requesterUserId));

        await _uow.UserRepository
            .DidNotReceiveWithAnyArgs()
            .ExistsPermission(default!, default!);
    }

    // ---------------------------------------------------------------
    // Moderator authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteFeedback_WhenRequesterHasManagePermission()
    {
        RoomFeedbackModel feedback = CreateFeedback(
            userId: _authorUserId);

        ConfigureFeedback(feedback);

        _uow.UserRepository
            .ExistsPermission(ManagePermission, _requesterUserId)
            .Returns(Task.FromResult(true));

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();

        await _uow.UserRepository
            .Received(1)
            .ExistsPermission(ManagePermission, _requesterUserId);

        _uow.RoomFeedbackRepository
            .Received(1)
            .Delete(
                Arg.Is<RoomFeedbackModel>(entity =>
                    entity.Id == _feedbackId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnForbidden_WhenRequesterIsNotAuthorOrModerator()
    {
        RoomFeedbackModel feedback = CreateFeedback(
            userId: _authorUserId);

        ConfigureFeedback(feedback);

        _uow.UserRepository
            .ExistsPermission(ManagePermission, _requesterUserId)
            .Returns(Task.FromResult(false));

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.FirstError.Should()
            .Be("Only the author or a moderator can delete this room feedback.");

        await _uow.UserRepository
            .Received(1)
            .ExistsPermission(ManagePermission, _requesterUserId);

        _uow.RoomFeedbackRepository
            .DidNotReceiveWithAnyArgs()
            .Delete(default!);
    }

    // ---------------------------------------------------------------
    // Authorization short-circuit
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldNotCheckModeratorPermission_WhenRequesterIsAuthor()
    {
        RoomFeedbackModel feedback = CreateFeedback(
            userId: _requesterUserId);

        ConfigureFeedback(feedback);

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();

        await _uow.UserRepository
            .DidNotReceiveWithAnyArgs()
            .ExistsPermission(default!, default!);
    }

    // ---------------------------------------------------------------
    // Deletion
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteExactlyOnce_WhenAuthorizationSucceeds()
    {
        RoomFeedbackModel feedback = CreateFeedback(
            userId: _requesterUserId);

        ConfigureFeedback(feedback);

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();

        _uow.RoomFeedbackRepository
            .Received(1)
            .Delete(feedback);
    }

    // ---------------------------------------------------------------
    // Cancellation token propagation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateCancellationTokenToRepository()
    {
        using CancellationTokenSource cts = new();

        CancellationToken ct = cts.Token;

        RoomFeedbackModel feedback = CreateFeedback(
            userId: _requesterUserId);

        _uow.RoomFeedbackRepository
            .GetByIdAsync(_feedbackId, ct)
            .Returns(Task.FromResult<RoomFeedbackModel?>(feedback));

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand(),
            ct);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _uow.RoomFeedbackRepository
            .Received(1)
            .GetByIdAsync(_feedbackId, ct);

        _uow.RoomFeedbackRepository
            .Received(1)
            .Delete(feedback);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private DeleteRoomFeedbackCommand CreateValidCommand()
    {
        return new DeleteRoomFeedbackCommand(
            _feedbackId,
            _requesterUserId);
    }

    private RoomFeedbackModel CreateFeedback(string userId)
    {
        return new RoomFeedbackModel
        {
            Id = _feedbackId,
            ReservationId = Guid.NewGuid().ToString(),
            RoomId = Guid.NewGuid().ToString(),
            UserId = userId,
            Rating = 5,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null
        };
    }

    private void ConfigureFeedback(RoomFeedbackModel feedback)
    {
        _uow.RoomFeedbackRepository
            .GetByIdAsync(
                _feedbackId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomFeedbackModel?>(feedback));
    }
}