using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.Services.Providers;
using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomFavorite.Services;

public sealed class AddRoomFavoriteUseCaseTest
{
    private const int MaxFavoritesPerUser = 50;

    private readonly IUnitOfWork _uow;
    private readonly AddRoomFavoriteUseCase _sut;

    private readonly string _roomId = Guid.NewGuid().ToString();
    private readonly string _userId = Guid.NewGuid().ToString();

    public AddRoomFavoriteUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _sut = new AddRoomFavoriteUseCase(_uow);
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsCanceled()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => _sut.ExecuteAsync(
            CreateValidCommand(),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        await _uow.RoomRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);

    }

    // ---------------------------------------------------------------
    // Command validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(
            null!);

        result.IsFailure.Should().BeTrue();

        await _uow.RoomRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsEmpty()
    {
        AddRoomFavoriteCommand command = new(
            string.Empty,
            _userId);

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _uow.RoomRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenUserIdIsEmpty()
    {
        AddRoomFavoriteCommand command = new(
            _roomId,
            string.Empty);

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();

        await _uow.RoomRepository
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomIdIsNotValidUuid()
    {
        AddRoomFavoriteCommand command = new(
            "invalid-room-id",
            _userId);

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenUserIdIsNotValidUuid()
    {
        AddRoomFavoriteCommand command = new(
            _roomId,
            "invalid-user-id");

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Room validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRoomDoesNotExist()
    {
        _uow.RoomRepository
            .GetByIdAsync(_roomId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(null));

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .GetByUserAndRoomAsync(default!, default!, default);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnBadRequest_WhenRoomIsInactive()
    {
        RoomModel room = CreateRoom(isActive: false);

        _uow.RoomRepository
            .GetByIdAsync(_roomId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .GetByUserAndRoomAsync(default!, default!, default);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, default);
    }

    // ---------------------------------------------------------------
    // Idempotency
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnExistingFavorite_WhenAlreadyFavorited()
    {
        RoomModel room = CreateRoom();

        RoomFavoriteModel existing = CreateFavorite();

        _uow.RoomRepository
            .GetByIdAsync(_roomId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));

        _uow.RoomFavoriteRepository
            .GetByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomFavoriteModel?>(existing));

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(existing);

        result.Value!.Room.Should().BeSameAs(room);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .CountByUserAsync(default!, default);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, default);
    }

    // ---------------------------------------------------------------
    // Favorite limit
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenFavoriteLimitIsReached()
    {
        RoomModel room = CreateRoom();

        _uow.RoomRepository
            .GetByIdAsync(_roomId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));

        _uow.RoomFavoriteRepository
            .GetByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomFavoriteModel?>(null));

        _uow.RoomFavoriteRepository
            .CountByUserAsync(
                _userId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MaxFavoritesPerUser));

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenFavoriteCountExceedsLimit()
    {
        RoomModel room = CreateRoom();

        _uow.RoomRepository
            .GetByIdAsync(_roomId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));

        _uow.RoomFavoriteRepository
            .GetByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomFavoriteModel?>(null));

        _uow.RoomFavoriteRepository
            .CountByUserAsync(
                _userId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MaxFavoritesPerUser + 1));

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, default);
    }

    // ---------------------------------------------------------------
    // Creation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldCreateFavorite_WhenAllValidationsPass()
    {
        RoomModel room = CreateRoom();

        _uow.RoomRepository
            .GetByIdAsync(_roomId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));

        _uow.RoomFavoriteRepository
            .GetByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomFavoriteModel?>(null));

        _uow.RoomFavoriteRepository
            .CountByUserAsync(
                _userId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        RoomFavoriteModel favorite = result.Value!;

        favorite.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(favorite.Id, out _).Should().BeTrue();

        favorite.UserId.Should().Be(_userId);
        favorite.RoomId.Should().Be(_roomId);
        favorite.Room.Should().BeSameAs(room);

        favorite.Version.Should().Be(1);
        favorite.CreatedAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(5));

        favorite.UpdatedAt.Should().BeNull();

        await _uow.RoomFavoriteRepository
            .Received(1)
            .AddAsync(
                Arg.Is<RoomFavoriteModel>(entity =>
                    entity.UserId == _userId &&
                    entity.RoomId == _roomId &&
                    entity.Version == 1),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowFavorite_WhenUserHas49Favorites()
    {
        RoomModel room = CreateRoom();

        _uow.RoomRepository
            .GetByIdAsync(_roomId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomModel?>(room));

        _uow.RoomFavoriteRepository
            .GetByUserAndRoomAsync(
                _userId,
                _roomId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RoomFavoriteModel?>(null));

        _uow.RoomFavoriteRepository
            .CountByUserAsync(
                _userId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MaxFavoritesPerUser - 1));

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(
            CreateValidCommand());

        result.IsSuccess.Should().BeTrue();

        await _uow.RoomFavoriteRepository
            .Received(1)
            .AddAsync(
                Arg.Any<RoomFavoriteModel>(),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Cancellation token propagation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateCancellationTokenToRepositories()
    {
        using CancellationTokenSource cts = new();

        CancellationToken ct = cts.Token;
        RoomModel room = CreateRoom();

        _uow.RoomRepository
            .GetByIdAsync(_roomId, ct)
            .Returns(Task.FromResult<RoomModel?>(room));

        _uow.RoomFavoriteRepository
            .GetByUserAndRoomAsync(_userId, _roomId, ct)
            .Returns(Task.FromResult<RoomFavoriteModel?>(null));

        _uow.RoomFavoriteRepository
            .CountByUserAsync(_userId, ct)
            .Returns(Task.FromResult(0));

        Result<RoomFavoriteModel> result = await _sut.ExecuteAsync(
            CreateValidCommand(),
            ct);

        result.IsSuccess.Should().BeTrue();

        await _uow.RoomRepository
            .Received(1)
            .GetByIdAsync(_roomId, ct);

        await _uow.RoomFavoriteRepository
            .Received(1)
            .GetByUserAndRoomAsync(_userId, _roomId, ct);

        await _uow.RoomFavoriteRepository
            .Received(1)
            .CountByUserAsync(_userId, ct);

        await _uow.RoomFavoriteRepository
            .Received(1)
            .AddAsync(Arg.Any<RoomFavoriteModel>(), ct);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private AddRoomFavoriteCommand CreateValidCommand()
    {
        return new AddRoomFavoriteCommand(
            _roomId,
            _userId);
    }

    private static RoomModel CreateRoom(bool isActive = true)
    {
        return new RoomModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomTypeId = Guid.NewGuid().ToString(),
            Code = "ROOM-001",
            Name = "Meeting Room",
            Location = "First Floor",
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null,
            IsActive = isActive
        };
    }

    private RoomFavoriteModel CreateFavorite()
    {
        return new RoomFavoriteModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = _userId,
            RoomId = _roomId,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null
        };
    }
}