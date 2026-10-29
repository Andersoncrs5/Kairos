using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.Room.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.Room.Services;

public sealed class FindRoomByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IRoomRepository> _roomRepository;
    private readonly FindRoomByIdUseCase _sut;

    public FindRoomByIdUseCaseTest()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _roomRepository = new Mock<IRoomRepository>();

        _unitOfWork
            .SetupGet(uow => uow.RoomRepository)
            .Returns(_roomRepository.Object);

        _sut = new FindRoomByIdUseCase(_unitOfWork.Object);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenRoomExists()
    {
        var roomId = Guid.NewGuid();
        var room = CreateRoom(roomId.ToString());

        _roomRepository
            .Setup(repository => repository.GetByIdAsync(
                roomId.ToString(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        var result = await _sut.ExecuteAsync(roomId.ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(room);

        _roomRepository.Verify(
            repository => repository.GetByIdAsync(
                roomId.ToString(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsEmpty(
        string id)
    {
        var result = await _sut.ExecuteAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Room ID is required.");

        _roomRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsNotValidUuid()
    {
        var result = await _sut.ExecuteAsync("invalid-uuid");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Id must be a valid UUID.");

        _roomRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        var roomId = Guid.NewGuid();

        _roomRepository
            .Setup(repository => repository.GetByIdAsync(
                roomId.ToString(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomModel?)null);

        var result = await _sut.ExecuteAsync(roomId.ToString());

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Room doesn't exist.");

        _roomRepository.Verify(
            repository => repository.GetByIdAsync(
                roomId.ToString(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancellationIsRequested()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        var act = async () => await _sut.ExecuteAsync(
            Guid.NewGuid().ToString(),
            cancellationTokenSource.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _roomRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------

    private static RoomModel CreateRoom(string id)
    {
        return new RoomModel
        {
            Id = id,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            RoomTypeId = Guid.NewGuid().ToString(),
            Code = $"R-{Guid.NewGuid():N}"[..30],
            Name = "Integration Test Room",
            Description = "Room created for unit testing.",
            Location = "First floor",
            Floor = "1",
            Capacity = 10,
            AreaSquareMeters = 25.50m,
            IsAccessible = true,
            IsReservable = true,
            IsActive = true
        };
    }
}