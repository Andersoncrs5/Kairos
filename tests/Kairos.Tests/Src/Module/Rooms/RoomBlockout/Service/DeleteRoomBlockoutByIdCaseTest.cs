using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomBlockout.Service;

public sealed class DeleteRoomBlockoutByIdCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IRoomBlockoutRepository> _repositoryMock = new();

    private readonly DeleteRoomBlockoutByIdCase _sut;

    public DeleteRoomBlockoutByIdCaseTest()
    {
        _uowMock
            .Setup(uow => uow.RoomBlockoutRepository)
            .Returns(_repositoryMock.Object);

        _sut = new DeleteRoomBlockoutByIdCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsTrueAndDeletesRoomBlockout_WhenIdExists()
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
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository => repository.Delete(blockout),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação do identificador
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsEmpty(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room blockout ID is required.");

        VerifyRepositoryWasNotCalled();
    }

    [Theory]
    [InlineData("invalid-id")]
    [InlineData("123")]
    [InlineData("not-a-uuid")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenIdIsInvalid(
        string id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Id must be a valid UUID.");

        VerifyRepositoryWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Registro inexistente
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenRoomBlockoutDoesNotExist()
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

        _repositoryMock.Verify(
            repository => repository.Delete(
                It.IsAny<RoomBlockoutModel>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancelled()
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

        VerifyRepositoryWasNotCalled();
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
            Notes = "Test room blockout",
            StartsAt = DateTimeOffset.UtcNow.AddHours(1),
            EndsAt = DateTimeOffset.UtcNow.AddHours(2),
            CreatedBy = Guid.NewGuid().ToString(),
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private void VerifyRepositoryWasNotCalled()
    {
        _repositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _repositoryMock.Verify(
            repository => repository.Delete(
                It.IsAny<RoomBlockoutModel>()),
            Times.Never);
    }
}