using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Rooms.RoomType.Services;

public sealed class ExistsRoomTypeByNameUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IRoomTypeRepository> _repositoryMock = new();
    private readonly ExistsRoomTypeByNameUseCase _sut;

    public ExistsRoomTypeByNameUseCaseTest()
    {
        _uowMock
            .Setup(uow => uow.RoomTypeRepository)
            .Returns(_repositoryMock.Object);

        _sut = new ExistsRoomTypeByNameUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Sucesso
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsTrue_WhenRoomTypeExists()
    {
        // Arrange
        const string name = "Double";

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(name);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                name,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFalse_WhenRoomTypeDoesNotExist()
    {
        // Arrange
        const string name = "Penthouse";

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(name);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeFalse();

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                name,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TrimsNameBeforeCheckingExistence()
    {
        // Arrange
        const string name = "Double";

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync("  Double  ");

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                name,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepository()
    {
        // Arrange
        const string name = "Double";

        using var cts = new CancellationTokenSource();

        _repositoryMock
            .Setup(repository => repository.ExistsByNameAsync(
                name,
                cts.Token))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(name, cts.Token);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeTrue();

        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                name,
                cts.Token),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validação do nome
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenNameIsEmpty(
        string name)
    {
        // Act
        var result = await _sut.ExecuteAsync(name);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room type name is required.");

        VerifyRepositoryWasNotCalled();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenNameExceedsMaximumLength()
    {
        // Arrange
        string name = new('a', 101);

        // Act
        var result = await _sut.ExecuteAsync(name);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Room type name cannot exceed 100 characters.");

        VerifyRepositoryWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Cancelamento
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => _sut.ExecuteAsync("Double", cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        VerifyRepositoryWasNotCalled();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void VerifyRepositoryWasNotCalled()
    {
        _repositoryMock.Verify(
            repository => repository.ExistsByNameAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}