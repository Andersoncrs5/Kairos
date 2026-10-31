using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Reservations.Reservation.Services;

public class FindReservationByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly FindReservationByIdUseCase _sut;

    public FindReservationByIdUseCaseTest()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>
        {
            DefaultValue = DefaultValue.Mock
        };

        _sut = new FindReservationByIdUseCase(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationExists_ReturnsSuccess()
    {
        // Arrange
        const string id = "reservation-123";

        ReservationModel reservation = new()
        {
            Id = id,
            RoomId = "room-123",
            UserId = "user-123",
            CategoryId = "category-123",
            Title = "Team Meeting",
            StartsAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _unitOfWorkMock
            .Setup(x => x.ReservationRepository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        Assert.False(result.IsFailure);
        Assert.NotNull(result.Value);
        Assert.Same(reservation, result.Value);

        _unitOfWorkMock.Verify(
            x => x.ReservationRepository.GetByIdAsync(
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

        _unitOfWorkMock.Verify(
            x => x.ReservationRepository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string id = "reservation-123";

        _unitOfWorkMock
            .Setup(x => x.ReservationRepository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        Assert.True(result.IsFailure);

        _unitOfWorkMock.Verify(
            x => x.ReservationRepository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        Task act() => _sut.ExecuteAsync("reservation-123", cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);

        _unitOfWorkMock.Verify(
            x => x.ReservationRepository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}