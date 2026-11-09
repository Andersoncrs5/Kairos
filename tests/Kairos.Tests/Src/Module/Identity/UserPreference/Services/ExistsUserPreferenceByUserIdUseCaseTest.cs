using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.Repositories;
using Kairos.Api.Modules.Identity.UserPreference.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Identity.UserPreference.Services;

public class ExistsUserPreferenceByUserIdUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private readonly Mock<IUserPreferenceRepository>
        _userPreferenceRepositoryMock = new();

    private readonly ExistsUserPreferenceByUserIdUseCase _sut;

    private readonly string _userId = Guid.NewGuid().ToString();

    public ExistsUserPreferenceByUserIdUseCaseTest()
    {
        _uowMock.Setup(uow => uow.UserPreferenceRepository)
            .Returns(_userPreferenceRepositoryMock.Object);

        _userPreferenceRepositoryMock
            .Setup(repository => repository.ExistsByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new ExistsUserPreferenceByUserIdUseCase(
            _uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Exists
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTrue_WhenPreferenceExists()
    {
        // Arrange
        _userPreferenceRepositoryMock
            .Setup(repository => repository.ExistsByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();

        _userPreferenceRepositoryMock.Verify(
            repository => repository.ExistsByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFalse_WhenPreferenceDoesNotExist()
    {
        // Arrange
        _userPreferenceRepositoryMock
            .Setup(repository => repository.ExistsByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeFalse();

        _userPreferenceRepositoryMock.Verify(
            repository => repository.ExistsByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-user-id")]
    [InlineData("123456")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenUserIdIsInvalid(
        string userId)
    {
        // Arrange & Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "User ID must be a valid UUID.");

        _userPreferenceRepositoryMock.Verify(
            repository => repository.ExistsByUserIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // UUID normalization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldNormalizeUserId_BeforeCheckingExistence()
    {
        // Arrange
        string userId = Guid.NewGuid().ToString().ToUpperInvariant();

        _userPreferenceRepositoryMock
            .Setup(repository => repository.ExistsByUserIdAsync(
                userId.ToLowerInvariant(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        _userPreferenceRepositoryMock.Verify(
            repository => repository.ExistsByUserIdAsync(
                userId.ToLowerInvariant(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenAlreadyCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();

        // Act
        Func<Task> act = async () =>
            await _sut.ExecuteAsync(_userId, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _userPreferenceRepositoryMock.Verify(
            repository => repository.ExistsByUserIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Cancellation token propagation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateCancellationTokenToRepository()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        await _sut.ExecuteAsync(_userId, cts.Token);

        // Assert
        _userPreferenceRepositoryMock.Verify(
            repository => repository.ExistsByUserIdAsync(
                _userId,
                cts.Token),
            Times.Once);
    }
}