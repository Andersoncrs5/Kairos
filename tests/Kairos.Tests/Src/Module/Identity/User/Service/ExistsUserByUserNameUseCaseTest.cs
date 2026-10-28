using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Identity.User.Service;

public sealed class ExistsUserByUserNameUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();

    private readonly ExistsUserByUserNameUseCase _sut;

    public ExistsUserByUserNameUseCaseTest()
    {
        _uowMock
            .Setup(uow => uow.UserRepository)
            .Returns(_userRepositoryMock.Object);

        _sut = new ExistsUserByUserNameUseCase(
            _uowMock.Object);
    }

    // ------------------------------------------------------------------
    // Validação
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenUserNameIsNullOrWhitespace(
        string? userName)
    {
        // Act
        var result = await _sut.ExecuteAsync(userName!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Username is required.");

        _userRepositoryMock.Verify(
            repository => repository.ExistsByUsername(
                It.IsAny<string>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // Existência do usuário
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTrue_WhenUserNameExists()
    {
        // Arrange
        const string userName = "anderson";

        _userRepositoryMock
            .Setup(repository => repository.ExistsByUsername(userName))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(userName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _userRepositoryMock.Verify(
            repository => repository.ExistsByUsername(userName),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFalse_WhenUserNameDoesNotExist()
    {
        // Arrange
        const string userName = "available-user";

        _userRepositoryMock
            .Setup(repository => repository.ExistsByUsername(userName))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(userName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();

        _userRepositoryMock.Verify(
            repository => repository.ExistsByUsername(userName),
            Times.Once);
    }

    // ------------------------------------------------------------------
    // Normalização
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldTrimUserNameBeforeQueryingRepository()
    {
        // Arrange
        const string input = "  anderson  ";
        const string expected = "anderson";

        _userRepositoryMock
            .Setup(repository => repository.ExistsByUsername(expected))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _userRepositoryMock.Verify(
            repository => repository.ExistsByUsername(expected),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.ExistsByUsername(input),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveUserNameCasing()
    {
        // Arrange
        const string userName = "Anderson";

        _userRepositoryMock
            .Setup(repository => repository.ExistsByUsername(userName))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(userName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _userRepositoryMock.Verify(
            repository => repository.ExistsByUsername(userName),
            Times.Once);
    }

    // ------------------------------------------------------------------
    // Cancelamento
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenTokenIsAlreadyCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(
            "anderson",
            cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _userRepositoryMock.Verify(
            repository => repository.ExistsByUsername(
                It.IsAny<string>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // Exceções
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateException_WhenRepositoryFails()
    {
        // Arrange
        const string userName = "anderson";
        const string errorMessage = "Database unavailable.";

        _userRepositoryMock
            .Setup(repository => repository.ExistsByUsername(userName))
            .ThrowsAsync(
                new InvalidOperationException(errorMessage));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(userName);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(errorMessage);

        _userRepositoryMock.Verify(
            repository => repository.ExistsByUsername(userName),
            Times.Once);
    }
}