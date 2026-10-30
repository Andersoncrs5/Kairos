using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomBlockout.Service;

public sealed class FindRoomBlockoutUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IRoomBlockoutRepository> _repositoryMock = new();

    private readonly FindRoomBlockoutUseCase _sut;

    public FindRoomBlockoutUseCaseTest()
    {
        _uowMock
            .SetupGet(uow => uow.RoomBlockoutRepository)
            .Returns(_repositoryMock.Object);

        _sut = new FindRoomBlockoutUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenIdIsValid_ShouldReturnRoomBlockout()
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
        result.Value.Should().NotBeNull();
        result.Value.Should().BeSameAs(blockout);

        result.Value!.Id.Should().Be(id);
        result.Value.RoomId.Should().Be(blockout.RoomId);
        result.Value.Type.Should().Be(blockout.Type);
        result.Value.Status.Should().Be(RoomBlockoutStatus.Scheduled);
        result.Value.Reason.Should().Be(blockout.Reason);
        result.Value.StartsAt.Should().Be(blockout.StartsAt);
        result.Value.EndsAt.Should().Be(blockout.EndsAt);
        result.Value.CreatedBy.Should().Be(blockout.CreatedBy);

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

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                id,
                cts.Token),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenIdIsNull_ShouldReturnValidationFailure()
    {
        // Arrange
        string id = null!;

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
    public async Task ExecuteAsync_WhenIdIsNotValidUuid_ShouldReturnValidationFailure(
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
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenRoomBlockoutDoesNotExist_ShouldReturnNotFound()
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
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "Room blockout doesn't exist.");

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
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
            Type = RoomBlockoutType.Maintenance,
            Status = RoomBlockoutStatus.Scheduled,
            Reason = "Scheduled maintenance",
            Notes = "Integration test blockout",
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