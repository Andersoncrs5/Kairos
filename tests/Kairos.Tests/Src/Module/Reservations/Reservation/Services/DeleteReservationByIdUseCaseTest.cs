using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Reservations.Reservation.Services;

public class DeleteReservationByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReservationRepository> _reservationRepositoryMock;
    private readonly DeleteReservationByIdUseCase _sut;

    public DeleteReservationByIdUseCaseTest()
    {
        _reservationRepositoryMock = new Mock<IReservationRepository>();

        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock
            .Setup(x => x.ReservationRepository)
            .Returns(_reservationRepositoryMock.Object);

        _sut = new DeleteReservationByIdUseCase(
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationExists_ReturnsTrue()
    {
        // Arrange
        const string id = "reservation-123";

        _reservationRepositoryMock
            .Setup(x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        Assert.False(result.IsFailure);
        Assert.True(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExist_ReturnsFalse()
    {
        // Arrange
        const string id = "reservation-123";

        _reservationRepositoryMock
            .Setup(x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        Assert.False(result.IsFailure);
        Assert.False(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        Assert.True(result.IsFailure);

        _reservationRepositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdContainsWhitespace_TrimsId()
    {
        // Arrange
        const string id = " reservation-123 ";
        const string expectedId = "reservation-123";

        _reservationRepositoryMock
            .Setup(x => x.DeleteByIdAndCountAsync(
                expectedId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        Assert.False(result.IsFailure);
        Assert.True(result.Value);

        _reservationRepositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                expectedId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Task act() => _sut.ExecuteAsync(
            "reservation-123",
            cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);

        _reservationRepositoryMock.Verify(
            x => x.DeleteByIdAndCountAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}