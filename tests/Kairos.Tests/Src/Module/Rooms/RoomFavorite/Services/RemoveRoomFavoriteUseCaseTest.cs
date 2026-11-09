using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFavorite.Services.Providers;
using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomFavorite.Services;

public sealed class RemoveRoomFavoriteUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly RemoveRoomFavoriteUseCase _sut;

    private readonly string _roomId = Guid.NewGuid().ToString();
    private readonly string _userId = Guid.NewGuid().ToString();

    public RemoveRoomFavoriteUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _sut = new RemoveRoomFavoriteUseCase(_uow);
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

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteByUserAndRoomAsync(default!, default!, default);
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
        result.Value.Should().BeFalse();

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteByUserAndRoomAsync(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsEmpty()
    {
        RemoveRoomFavoriteCommand command = new(
            string.Empty,
            _userId);

        Result<bool> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteByUserAndRoomAsync(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenUserIdIsEmpty()
    {
        RemoveRoomFavoriteCommand command = new(
            _roomId,
            string.Empty);

        Result<bool> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteByUserAndRoomAsync(default!, default!, default);
    }

    // ---------------------------------------------------------------
    // Identifier validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsNotValidUuid()
    {
        RemoveRoomFavoriteCommand command = new(
            "invalid-room-id",
            _userId);

        Result<bool> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.FirstError.Should().Be("Room ID must be a valid UUID.");

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteByUserAndRoomAsync(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenUserIdIsNotValidUuid()
    {
        RemoveRoomFavoriteCommand command = new(
            _roomId,
            "invalid-user-id");

        Result<bool> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.FirstError.Should().Be("User ID must be a valid UUID.");

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteByUserAndRoomAsync(default!, default!, default);
    }

    // ---------------------------------------------------------------
    // Remove favorite
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTrue_WhenFavoriteIsDeleted()
    {
        _uow.RoomFavoriteRepository
            .DeleteByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();

        await _uow.RoomFavoriteRepository
            .Received(1)
            .DeleteByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFalse_WhenFavoriteDoesNotExist()
    {
        _uow.RoomFavoriteRepository
            .DeleteByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeFalse();

        await _uow.RoomFavoriteRepository
            .Received(1)
            .DeleteByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTrue_WhenMultipleRowsAreDeleted()
    {
        _uow.RoomFavoriteRepository
            .DeleteByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(2));

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Idempotency
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldBeIdempotent_WhenFavoriteDoesNotExist()
    {
        _uow.RoomFavoriteRepository
            .DeleteByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        Result<bool> firstResult = await _sut.ExecuteAsync(
            CreateValidCommand());

        Result<bool> secondResult = await _sut.ExecuteAsync(
            CreateValidCommand());

        firstResult.IsSuccess.Should().BeTrue();
        firstResult.Value.Should().BeFalse();

        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().BeFalse();

        await _uow.RoomFavoriteRepository
            .Received(2)
            .DeleteByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Cancellation token propagation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateCancellationTokenToRepository()
    {
        using CancellationTokenSource cts = new();

        CancellationToken ct = cts.Token;

        _uow.RoomFavoriteRepository
            .DeleteByUserAndRoomAsync(_userId, _roomId, ct)
            .Returns(Task.FromResult(1));

        Result<bool> result = await _sut.ExecuteAsync(
            CreateValidCommand(),
            ct);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _uow.RoomFavoriteRepository
            .Received(1)
            .DeleteByUserAndRoomAsync(
                _userId,
                _roomId,
                ct);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private RemoveRoomFavoriteCommand CreateValidCommand()
    {
        return new RemoveRoomFavoriteCommand(
            _roomId,
            _userId);
    }
}