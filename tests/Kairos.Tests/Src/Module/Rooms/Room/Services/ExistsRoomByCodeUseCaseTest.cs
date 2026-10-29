using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.Room.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.Room.Services;

public sealed class ExistsRoomByCodeUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IRoomRepository> _roomRepository;
    private readonly ExistsRoomByCodeUseCase _sut;

    public ExistsRoomByCodeUseCaseTest()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _roomRepository = new Mock<IRoomRepository>();

        _unitOfWork
            .SetupGet(uow => uow.RoomRepository)
            .Returns(_roomRepository.Object);

        _sut = new ExistsRoomByCodeUseCase(_unitOfWork.Object);
    }

    // ---------------------------------------------------------------
    // Existência
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsTrue_WhenRoomCodeExists()
    {
        const string code = "ROOM-101";

        _roomRepository
            .Setup(repository => repository.ExistsByCodeAsync(
                code,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(code);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _roomRepository.Verify(
            repository => repository.ExistsByCodeAsync(
                code,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFalse_WhenRoomCodeDoesNotExist()
    {
        const string code = "ROOM-999";

        _roomRepository
            .Setup(repository => repository.ExistsByCodeAsync(
                code,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.ExecuteAsync(code);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();

        _roomRepository.Verify(
            repository => repository.ExistsByCodeAsync(
                code,
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
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenCodeIsEmpty(
        string code)
    {
        var result = await _sut.ExecuteAsync(code);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Room code is required.");

        _roomRepository.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenCodeExceedsMaximumLength()
    {
        var code = new string('A', 31);

        var result = await _sut.ExecuteAsync(code);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Room code cannot exceed 30 characters.");

        _roomRepository.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_TrimsCode_BeforeCheckingExistence()
    {
        const string code = "ROOM-101";

        _roomRepository
            .Setup(repository => repository.ExistsByCodeAsync(
                code,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync("  ROOM-101  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _roomRepository.Verify(
            repository => repository.ExistsByCodeAsync(
                code,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancellationIsRequested()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _sut.ExecuteAsync(
            "ROOM-101",
            cts.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _roomRepository.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}