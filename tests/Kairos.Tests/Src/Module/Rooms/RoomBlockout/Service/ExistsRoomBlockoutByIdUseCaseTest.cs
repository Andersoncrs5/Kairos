using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomBlockout.Service;

public sealed class ExistsRoomBlockoutByIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IRoomBlockoutRepository> _repositoryMock = new();

    private readonly ExistsRoomBlockoutByIdUseCase _sut;

    public ExistsRoomBlockoutByIdUseCaseTest()
    {
        _uowMock
            .SetupGet(uow => uow.RoomBlockoutRepository)
            .Returns(_repositoryMock.Object);

        _sut = new ExistsRoomBlockoutByIdUseCase(
            _uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomBlockoutExists_ShouldReturnTrue()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var blockout = CreateRoomBlockout(id);

        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockout);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomBlockoutDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoomBlockoutModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCancellationTokenToRepository()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var blockout = CreateRoomBlockout(id);

        using var cts = new CancellationTokenSource();

        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(
                id,
                cts.Token))
            .ReturnsAsync(blockout);

        // Act
        var result = await _sut.ExecuteAsync(id, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                id,
                cts.Token),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ShouldReturnValidationFailure(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room blockout ID is required.");

        VerifyGetByIdAsyncWasNotCalled();
    }

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task ExecuteAsync_WhenIdIsInvalid_ShouldReturnValidationFailure(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Id must be a valid UUID.");

        VerifyGetByIdAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var id = Guid.NewGuid().ToString();

        // Act
        var act = () => _sut.ExecuteAsync(id, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        VerifyGetByIdAsyncWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static RoomBlockoutModel CreateRoomBlockout(string id)
    {
        return new RoomBlockoutModel
        {
            Id = id,
            RoomId = Guid.NewGuid().ToString(),
            Type = Kairos.Api.Common.Enums.RoomBlockoutType.Maintenance,
            Status = Kairos.Api.Common.Enums.RoomBlockoutStatus.Scheduled,
            Reason = "Scheduled maintenance",
            StartsAt = DateTimeOffset.UtcNow.AddHours(1),
            EndsAt = DateTimeOffset.UtcNow.AddHours(2),
            CreatedBy = Guid.NewGuid().ToString(),
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null
        };
    }

    private void VerifyGetByIdAsyncWasNotCalled()
    {
        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}