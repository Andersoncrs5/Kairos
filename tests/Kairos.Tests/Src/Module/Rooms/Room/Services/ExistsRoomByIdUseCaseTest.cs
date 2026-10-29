using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.Room.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.Room.Services;

public sealed class ExistsRoomByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IRoomRepository> _roomRepository;
    private readonly ExistsRoomByIdUseCase _sut;

    public ExistsRoomByIdUseCaseTest()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _roomRepository = new Mock<IRoomRepository>();

        _unitOfWork
            .SetupGet(uow => uow.RoomRepository)
            .Returns(_roomRepository.Object);

        _sut = new ExistsRoomByIdUseCase(_unitOfWork.Object);
    }

    // ---------------------------------------------------------------
    // Existência
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsTrue_WhenRoomExists()
    {
        var id = Guid.NewGuid().ToString();

        _roomRepository
            .Setup(repository => repository.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _roomRepository.Verify(
            repository => repository.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFalse_WhenRoomDoesNotExist()
    {
        var id = Guid.NewGuid().ToString();

        _roomRepository
            .Setup(repository => repository.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.ExecuteAsync(id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();

        _roomRepository.Verify(
            repository => repository.ExistsAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsEmpty(
        string id)
    {
        var result = await _sut.ExecuteAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Room ID is required.");

        _roomRepository.Verify(
            repository => repository.ExistsAsync(
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
            repository => repository.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancellationIsRequested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await _sut.ExecuteAsync(
            Guid.NewGuid().ToString(),
            cts.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _roomRepository.Verify(
            repository => repository.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}