using FluentAssertions;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.Services.Providers;
using NSubstitute;

namespace Kairos.Tests.Module.Rooms.RoomFavorite.Services;

public sealed class FindRoomFavoritesUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly FindRoomFavoritesUseCase _sut;

    private readonly string _requesterUserId = Guid.NewGuid().ToString();
    private readonly string _roomId = Guid.NewGuid().ToString();

    public FindRoomFavoritesUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _sut = new FindRoomFavoritesUseCase(_uow);
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

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .Find(default!, default);
    }

    // ---------------------------------------------------------------
    // Command validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(null!);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .Find(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRequesterUserIdIsEmpty()
    {
        FindRoomFavoritesCommand command = new(
            string.Empty,
            CreateValidFilter());

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .Find(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenFilterIsNull()
    {
        FindRoomFavoritesCommand command = new(
            _requesterUserId,
            null!);

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .Find(default!, default);
    }

    // ---------------------------------------------------------------
    // Filter validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomCodeExceedsMaximumLength()
    {
        FilterRoomFavoriteDto filter = new()
        {
            CodeRoom = new string('A', 31)
        };

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(CreateValidCommand(filter));

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .Find(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRoomNameExceedsMaximumLength()
    {
        FilterRoomFavoriteDto filter = new()
        {
            NameRoom = new string('A', 121)
        };

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(CreateValidCommand(filter));

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .Find(default!, default);
    }

    // ---------------------------------------------------------------
    // Requester validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenRequesterUserIdIsNotValidUuid()
    {
        FindRoomFavoritesCommand command = new(
            "invalid-user-id",
            CreateValidFilter());

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(command);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .Find(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenFilterUserIdIsNotValidUuid()
    {
        FilterRoomFavoriteDto filter = new()
        {
            UserId = "invalid-user-id"
        };

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(CreateValidCommand(filter));

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .Find(default!, default);
    }

    // ---------------------------------------------------------------
    // User scope
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnForbidden_WhenFilterUserIdBelongsToAnotherUser()
    {
        string anotherUserId = Guid.NewGuid().ToString();

        FilterRoomFavoriteDto filter = new()
        {
            UserId = anotherUserId
        };

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(CreateValidCommand(filter));

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        await _uow.RoomFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .Find(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldApplyRequesterUserId_WhenFilterDoesNotSpecifyUserId()
    {
        FilterRoomFavoriteDto filter = CreateValidFilter();

        Page<RoomFavoriteModel> expectedPage =
            CreatePage(CreateFavorite());

        _uow.RoomFavoriteRepository
            .Find(
                Arg.Any<FilterRoomFavoriteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedPage));

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(CreateValidCommand(filter));

        result.IsSuccess.Should().BeTrue();

        filter.UserId.Should().Be(_requesterUserId);

        await _uow.RoomFavoriteRepository
            .Received(1)
            .Find(
                Arg.Is<FilterRoomFavoriteDto>(dto =>
                    dto.UserId == _requesterUserId),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowSearch_WhenFilterUserIdMatchesRequester()
    {
        FilterRoomFavoriteDto filter = new()
        {
            UserId = _requesterUserId
        };

        Page<RoomFavoriteModel> expectedPage =
            CreatePage(CreateFavorite());

        _uow.RoomFavoriteRepository
            .Find(
                Arg.Any<FilterRoomFavoriteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedPage));

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(CreateValidCommand(filter));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expectedPage);

        await _uow.RoomFavoriteRepository
            .Received(1)
            .Find(
                Arg.Is<FilterRoomFavoriteDto>(dto =>
                    dto.UserId == _requesterUserId),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Repository search
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFavorites_WhenSearchSucceeds()
    {
        RoomFavoriteModel favorite = CreateFavorite();

        Page<RoomFavoriteModel> expectedPage =
            CreatePage(favorite);

        _uow.RoomFavoriteRepository
            .Find(
                Arg.Any<FilterRoomFavoriteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedPage));

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        result.Value.Should().BeSameAs(expectedPage);
        result.Value!.Data.Should().ContainSingle()
            .Which.Should().BeSameAs(favorite);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyPage_WhenNoFavoritesExist()
    {
        Page<RoomFavoriteModel> emptyPage =
            Page<RoomFavoriteModel>.Create(
                [],
                0,
                1,
                10);

        _uow.RoomFavoriteRepository
            .Find(
                Arg.Any<FilterRoomFavoriteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(emptyPage));

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(CreateValidCommand());

        result.IsSuccess.Should().BeTrue();

        result.Value.Should().NotBeNull();
        result.Value!.IsEmpty.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveSearchFilters_WhenCallingRepository()
    {
        FilterRoomFavoriteDto filter = new()
        {
            RoomId = _roomId,
            CodeRoom = "ROOM-001",
            CodeRoomIgnoreCase = true,
            NameRoom = "Meeting Room",
            NameRoomIgnoreCase = true
        };

        Page<RoomFavoriteModel> expectedPage =
            CreatePage(CreateFavorite());

        _uow.RoomFavoriteRepository
            .Find(
                Arg.Any<FilterRoomFavoriteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedPage));

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(CreateValidCommand(filter));

        result.IsSuccess.Should().BeTrue();

        await _uow.RoomFavoriteRepository
            .Received(1)
            .Find(
                Arg.Is<FilterRoomFavoriteDto>(dto =>
                    dto.UserId == _requesterUserId &&
                    dto.RoomId == _roomId &&
                    dto.CodeRoom == "ROOM-001" &&
                    dto.CodeRoomIgnoreCase == true &&
                    dto.NameRoom == "Meeting Room" &&
                    dto.NameRoomIgnoreCase == true),
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

        FilterRoomFavoriteDto filter = CreateValidFilter();

        Page<RoomFavoriteModel> expectedPage =
            CreatePage(CreateFavorite());

        _uow.RoomFavoriteRepository
            .Find(
                Arg.Any<FilterRoomFavoriteDto>(),
                ct)
            .Returns(Task.FromResult(expectedPage));

        Result<Page<RoomFavoriteModel>> result =
            await _sut.ExecuteAsync(
                CreateValidCommand(filter),
                ct);

        result.IsSuccess.Should().BeTrue();

        await _uow.RoomFavoriteRepository
            .Received(1)
            .Find(
                Arg.Is<FilterRoomFavoriteDto>(dto =>
                    dto.UserId == _requesterUserId),
                ct);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private FindRoomFavoritesCommand CreateValidCommand(
        FilterRoomFavoriteDto? filter = null)
    {
        return new FindRoomFavoritesCommand(
            _requesterUserId,
            filter ?? CreateValidFilter());
    }

    private static FilterRoomFavoriteDto CreateValidFilter()
    {
        return new FilterRoomFavoriteDto();
    }

    private RoomFavoriteModel CreateFavorite()
    {
        return new RoomFavoriteModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = _requesterUserId,
            RoomId = _roomId,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null
        };
    }

    private Page<RoomFavoriteModel> CreatePage(
        params RoomFavoriteModel[] favorites)
    {
        return Page<RoomFavoriteModel>.Create(
            favorites,
            favorites.Length,
            1,
            10);
    }
}