using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Repositories;
using Kairos.Api.Modules.Identity.UserPreference.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Identity.UserPreference.Services;

public sealed class ResetUserPreferenceUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IUserPreferenceRepository> _repositoryMock = new();

    private readonly ResetUserPreferenceUseCase _sut;

    private readonly string _userId = Guid.NewGuid().ToString();
    private readonly UserPreferenceModel _preference;

    public ResetUserPreferenceUseCaseTest()
    {
        _preference = CreatePreference(_userId);

        _uowMock
            .Setup(uow => uow.UserPreferenceRepository)
            .Returns(_repositoryMock.Object);

        _repositoryMock
            .Setup(repository => repository.GetByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_preference);

        _sut = new ResetUserPreferenceUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsResetPreference_WhenPreferenceExists()
    {
        // Arrange
        DateTimeOffset previousUpdatedAt = _preference.UpdatedAt!.Value;

        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.StatusCode.Should().Be(200);

        result.Value.Should().NotBeNull();
        result.Value.Should().BeSameAs(_preference);

        _preference.UpdatedAt.Should().NotBeNull();
        _preference.UpdatedAt!.Value.Should().BeAfter(previousUpdatedAt);

        _repositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository => repository.Update(_preference),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Appearance reset
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ResetsAppearanceSettings_ToDefaultValues()
    {
        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value!.Theme.Should().Be(UserThemeEnum.System);
        result.Value.CompactMode.Should().BeFalse();
        result.Value.ReducedMotion.Should().BeFalse();
        result.Value.HighContrast.Should().BeFalse();
        result.Value.SidebarCollapsed.Should().BeFalse();
        result.Value.AnimationsEnabled.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Localization reset
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ResetsLocalizationSettings_ToDefaultValues()
    {
        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value!.Language.Should().Be("pt-BR");
        result.Value.TimeZoneId.Should().Be("America/Fortaleza");
        result.Value.TimeFormat.Should().Be(TimeFormatEnum.Hour24);
    }

    // ---------------------------------------------------------------
    // Notifications reset
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ResetsNotificationSettings_ToDefaultValues()
    {
        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value!.NotificationsEnabled.Should().BeTrue();
        result.Value.EmailNotificationsEnabled.Should().BeFalse();
        result.Value.ReservationRemindersEnabled.Should().BeTrue();
        result.Value.ReservationUpdatesEnabled.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Not Found
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenPreferenceDoesNotExist()
    {
        // Arrange
        _repositoryMock
            .Setup(repository => repository.GetByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferenceModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "User preferences not found.");

        VerifyNothingWasUpdated();
    }

    // ---------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("invalid-uuid")]
    [InlineData("123456")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenUserIdIsInvalid(
        string userId)
    {
        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "User ID must be a valid UUID.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    // ---------------------------------------------------------------
    // UUID normalization
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NormalizesUserId_BeforeFindingPreference()
    {
        // Arrange
        string userId = Guid.NewGuid().ToString().ToUpperInvariant();
        string normalizedUserId = Guid.Parse(userId).ToString();

        _repositoryMock
            .Setup(repository => repository.GetByUserIdAsync(
                normalizedUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_preference);

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(_preference);

        _repositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
                normalizedUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository => repository.Update(_preference),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Preserve entity metadata
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PreservesEntityMetadata_WhenResettingPreference()
    {
        // Arrange
        string originalId = _preference.Id;
        string originalUserId = _preference.UserId;
        long originalVersion = _preference.Version;
        DateTimeOffset originalCreatedAt = _preference.CreatedAt;

        // Act
        var result = await _sut.ExecuteAsync(_userId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value!.Id.Should().Be(originalId);
        result.Value.UserId.Should().Be(originalUserId);
        result.Value.Version.Should().Be(originalVersion);
        result.Value.CreatedAt.Should().Be(originalCreatedAt);
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => _sut.ExecuteAsync(_userId, cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    // ---------------------------------------------------------------
    // Cancellation token propagation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepository()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        CancellationToken ct = cts.Token;

        // Act
        var result = await _sut.ExecuteAsync(_userId, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _repositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
                _userId,
                ct),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static UserPreferenceModel CreatePreference(string userId)
    {
        return new UserPreferenceModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Version = 5,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),

            // Appearance
            Theme = UserThemeEnum.Dracula,
            CompactMode = true,
            ReducedMotion = true,
            HighContrast = true,
            SidebarCollapsed = true,
            AnimationsEnabled = false,

            // Localization
            Language = "en-US",
            TimeZoneId = "UTC",
            TimeFormat = TimeFormatEnum.Hour12,

            // Notifications
            NotificationsEnabled = false,
            EmailNotificationsEnabled = true,
            ReservationRemindersEnabled = false,
            ReservationUpdatesEnabled = false
        };
    }

    private void VerifyPreferenceWasNotLoaded()
    {
        _repositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifyNothingWasUpdated()
    {
        _repositoryMock.Verify(
            repository => repository.Update(
                It.IsAny<UserPreferenceModel>()),
            Times.Never);
    }
}