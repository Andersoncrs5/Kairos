using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Repositories;
using Kairos.Api.Modules.Identity.UserPreference.Services.Providers;
using Moq;

namespace Kairos.Tests.Module.Identity.UserPreference.Services;

public class FindUserPreferenceUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private readonly Mock<IUserPreferenceRepository>
        _userPreferenceRepositoryMock = new();

    private readonly FindUserPreferenceUseCase _sut;

    private readonly string _userId = Guid.NewGuid().ToString();

    private readonly UserPreferenceModel _preference;

    public FindUserPreferenceUseCaseTest()
    {
        _preference = new UserPreferenceModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = _userId,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _uowMock.Setup(uow => uow.UserPreferenceRepository)
            .Returns(_userPreferenceRepositoryMock.Object);

        _userPreferenceRepositoryMock
            .Setup(repository => repository.GetByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_preference);

        _sut = new FindUserPreferenceUseCase(
            _uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Find
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnPreference_WhenPreferenceExists()
    {
        // Arrange
        // Configured in constructor.

        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(_preference);

        _userPreferenceRepositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenPreferenceDoesNotExist()
    {
        // Arrange
        _userPreferenceRepositoryMock
            .Setup(repository => repository.GetByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferenceModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        _userPreferenceRepositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
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
            repository => repository.GetByUserIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // UUID normalization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldNormalizeUserId_BeforeFindingPreference()
    {
        // Arrange
        string userId = Guid.NewGuid().ToString().ToUpperInvariant();
        string normalizedUserId = userId.ToLowerInvariant();

        _userPreferenceRepositoryMock
            .Setup(repository => repository.GetByUserIdAsync(
                normalizedUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_preference);

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(_preference);

        _userPreferenceRepositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
                normalizedUserId,
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
        cts.Cancel();

        // Act
        Func<Task> act = async () =>
            await _sut.ExecuteAsync(_userId, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _userPreferenceRepositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
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
            repository => repository.GetByUserIdAsync(
                _userId,
                cts.Token),
            Times.Once);
    }
}