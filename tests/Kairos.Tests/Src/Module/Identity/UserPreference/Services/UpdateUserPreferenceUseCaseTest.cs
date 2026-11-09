using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Commands;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Requests;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Repositories;
using Kairos.Api.Modules.Identity.UserPreference.Services.Providers;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Identity.UserPreference.Services;

public sealed class UpdateUserPreferenceUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IUserPreferenceRepository> _repositoryMock = new();

    private readonly UpdateUserPreferenceUseCase _sut;

    private readonly string _userId = Guid.NewGuid().ToString();
    private readonly UserPreferenceModel _preference;

    public UpdateUserPreferenceUseCaseTest()
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

        _sut = new UpdateUserPreferenceUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsUpdatedPreference_WhenDataIsValid()
    {
        // Arrange
        DateTimeOffset previousUpdatedAt = _preference.UpdatedAt!.Value;

        UpdateUserPreferenceDTO dto = new()
        {
            Theme = UserThemeEnum.Ocean,
            Language = "pt-BR",
            TimeZoneId = "UTC",
            TimeFormat = TimeFormatEnum.Hour24,

            CompactMode = false,
            ReducedMotion = false,
            HighContrast = false,
            SidebarCollapsed = false,
            AnimationsEnabled = true,

            NotificationsEnabled = true,
            EmailNotificationsEnabled = false,
            ReservationRemindersEnabled = true,
            ReservationUpdatesEnabled = true
        };

        UpdateUserPreferenceCommand command = CreateCommand(dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.StatusCode.Should().Be(200);

        result.Value.Should().NotBeNull();
        result.Value.Should().BeSameAs(_preference);

        result.Value!.Theme.Should().Be(UserThemeEnum.Ocean);
        result.Value.Language.Should().Be("pt-BR");
        result.Value.TimeZoneId.Should().Be("UTC");
        result.Value.TimeFormat.Should().Be(TimeFormatEnum.Hour24);

        result.Value.CompactMode.Should().BeFalse();
        result.Value.ReducedMotion.Should().BeFalse();
        result.Value.HighContrast.Should().BeFalse();
        result.Value.SidebarCollapsed.Should().BeFalse();
        result.Value.AnimationsEnabled.Should().BeTrue();

        result.Value.NotificationsEnabled.Should().BeTrue();
        result.Value.EmailNotificationsEnabled.Should().BeFalse();
        result.Value.ReservationRemindersEnabled.Should().BeTrue();
        result.Value.ReservationUpdatesEnabled.Should().BeTrue();

        result.Value.UpdatedAt.Should().NotBeNull();
        result.Value.UpdatedAt!.Value.Should().BeAfter(previousUpdatedAt);

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
    // Partial update
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PreservesUnchangedFields_WhenOnlyLanguageIsProvided()
    {
        // Arrange
        UpdateUserPreferenceDTO dto = new()
        {
            Language = "en-US"
        };

        UpdateUserPreferenceCommand command = CreateCommand(dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value!.Language.Should().Be("en-US");

        // Appearance must remain unchanged.
        result.Value.Theme.Should().Be(UserThemeEnum.Dracula);
        result.Value.CompactMode.Should().BeTrue();
        result.Value.ReducedMotion.Should().BeTrue();
        result.Value.HighContrast.Should().BeTrue();
        result.Value.SidebarCollapsed.Should().BeTrue();
        result.Value.AnimationsEnabled.Should().BeFalse();

        // Localization fields not supplied must remain unchanged.
        result.Value.TimeZoneId.Should().Be("America/Fortaleza");
        result.Value.TimeFormat.Should().Be(TimeFormatEnum.Hour12);

        // Notifications must remain unchanged.
        result.Value.NotificationsEnabled.Should().BeFalse();
        result.Value.EmailNotificationsEnabled.Should().BeTrue();
        result.Value.ReservationRemindersEnabled.Should().BeFalse();
        result.Value.ReservationUpdatesEnabled.Should().BeFalse();

        _repositoryMock.Verify(
            repository => repository.Update(_preference),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Command validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenCommandIsNull()
    {
        // Arrange & Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "User preference update command is required.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenUserIdIsEmpty()
    {
        // Arrange
        UpdateUserPreferenceCommand command = new()
        {
            UserId = string.Empty,
            Dto = CreateValidDto()
        };

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("User ID is required.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenDtoIsNull()
    {
        // Arrange
        UpdateUserPreferenceCommand command = new()
        {
            UserId = _userId,
            Dto = null!
        };

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Preference data is required.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenUserIdIsNotValidUuid()
    {
        // Arrange
        UpdateUserPreferenceCommand command = new()
        {
            UserId = "invalid-uuid",
            Dto = CreateValidDto()
        };

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "User ID must be a valid UUID.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    // ---------------------------------------------------------------
    // DTO validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenThemeIsInvalid()
    {
        // Arrange
        UpdateUserPreferenceDTO dto = new()
        {
            Theme = (UserThemeEnum)999
        };

        // Act
        var result = await _sut.ExecuteAsync(CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Theme is invalid.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenTimeFormatIsInvalid()
    {
        // Arrange
        UpdateUserPreferenceDTO dto = new()
        {
            TimeFormat = (TimeFormatEnum)999
        };

        // Act
        var result = await _sut.ExecuteAsync(CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Time format is invalid.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenLanguageIsEmpty()
    {
        // Arrange
        UpdateUserPreferenceDTO dto = new()
        {
            Language = "   "
        };

        // Act
        var result = await _sut.ExecuteAsync(CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Language cannot be empty.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenTimeZoneIsEmpty()
    {
        // Arrange
        UpdateUserPreferenceDTO dto = new()
        {
            TimeZoneId = "   "
        };

        // Act
        var result = await _sut.ExecuteAsync(CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Time zone cannot be empty.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenTimeZoneIsInvalid()
    {
        // Arrange
        UpdateUserPreferenceDTO dto = new()
        {
            TimeZoneId = "Invalid/Timezone_123"
        };

        // Act
        var result = await _sut.ExecuteAsync(CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Time zone is invalid.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    // ---------------------------------------------------------------
    // Empty update
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenNoPreferenceIsProvided()
    {
        // Arrange
        UpdateUserPreferenceDTO dto = new();

        UpdateUserPreferenceCommand command = CreateCommand(dto);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "At least one preference must be provided.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    // ---------------------------------------------------------------
    // Multiple validation errors
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsAllValidationErrors_WhenMultipleFieldsAreInvalid()
    {
        // Arrange
        UpdateUserPreferenceDTO dto = new()
        {
            Theme = (UserThemeEnum)999,
            TimeFormat = (TimeFormatEnum)999,
            Language = " ",
            TimeZoneId = "Invalid/Timezone_123"
        };

        // Act
        var result = await _sut.ExecuteAsync(CreateCommand(dto));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain("Theme is invalid.");
        result.Errors.Should().Contain("Time format is invalid.");
        result.Errors.Should().Contain("Language cannot be empty.");
        result.Errors.Should().Contain("Time zone is invalid.");

        VerifyPreferenceWasNotLoaded();
        VerifyNothingWasUpdated();
    }

    // ---------------------------------------------------------------
    // Existence
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

        UpdateUserPreferenceCommand command =
            CreateCommand(new UpdateUserPreferenceDTO
            {
                Language = "en-US"
            });

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);

        result.Errors.Should().Contain(
            "User preferences not found.");

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

        UserPreferenceModel preference =
            CreatePreference(normalizedUserId);

        _repositoryMock
            .Setup(repository => repository.GetByUserIdAsync(
                normalizedUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);

        UpdateUserPreferenceCommand command = new()
        {
            UserId = userId,
            Dto = new UpdateUserPreferenceDTO
            {
                Language = "en-US"
            }
        };

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(preference);

        _repositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
                normalizedUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            repository => repository.Update(preference),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Preserve entity metadata
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PreservesEntityMetadata_WhenUpdatingPreference()
    {
        // Arrange
        string originalId = _preference.Id;
        string originalUserId = _preference.UserId;
        long originalVersion = _preference.Version;
        DateTimeOffset originalCreatedAt = _preference.CreatedAt;

        UpdateUserPreferenceDTO dto = new()
        {
            Language = "en-US"
        };

        // Act
        var result = await _sut.ExecuteAsync(CreateCommand(dto));

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

        UpdateUserPreferenceCommand command =
            CreateCommand(CreateValidDto());

        // Act
        var act = () => _sut.ExecuteAsync(command, cts.Token);

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

        UpdateUserPreferenceCommand command =
            CreateCommand(new UpdateUserPreferenceDTO
            {
                Language = "en-US"
            });

        // Act
        var result = await _sut.ExecuteAsync(command, ct);

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

    private UpdateUserPreferenceCommand CreateCommand(
        UpdateUserPreferenceDTO dto)
    {
        return new UpdateUserPreferenceCommand
        {
            UserId = _userId,
            Dto = dto
        };
    }

    private static UpdateUserPreferenceDTO CreateValidDto()
    {
        return new UpdateUserPreferenceDTO
        {
            Language = "en-US"
        };
    }

    private static UserPreferenceModel CreatePreference(string userId)
    {
        return new UserPreferenceModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Version = 7,
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
            TimeZoneId = "America/Fortaleza",
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