using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.UserPreference.DTOs.Commands;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Repositories;
using Kairos.Api.Modules.Identity.UserPreference.Services.Providers;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Moq;

namespace Kairos.Tests.Module.Identity.UserPreference.Services;

public class CreateUserPreferenceUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private readonly Mock<IUserRepository> _userRepositoryMock = new();

    private readonly Mock<IUserPreferenceRepository>
        _userPreferenceRepositoryMock = new();

    private readonly CreateUserPreferenceUseCase _sut;

    private readonly string _userId = Guid.NewGuid().ToString();

    private readonly UserModel _user;

    public CreateUserPreferenceUseCaseTest()
    {
        _user = new UserModel
        {
            Id = _userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _uowMock.Setup(uow => uow.UserRepository)
            .Returns(_userRepositoryMock.Object);

        _uowMock.Setup(uow => uow.UserPreferenceRepository)
            .Returns(_userPreferenceRepositoryMock.Object);

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId))
            .ReturnsAsync(_user);

        _userPreferenceRepositoryMock
            .Setup(repository => repository.GetByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferenceModel?)null);

        _userPreferenceRepositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new CreateUserPreferenceUseCase(_uowMock.Object);
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldCreatePreference_WhenUserExists()
    {
        // Arrange
        var command = new CreateUserPreferenceCommand
        {
            UserId = _userId
        };

        UserPreferenceModel? createdPreference = null;

        _userPreferenceRepositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<UserPreferenceModel, CancellationToken>(
                (preference, _) => createdPreference = preference)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value.Should().NotBeNull();

        createdPreference.Should().NotBeNull();

        createdPreference!.UserId.Should().Be(_userId);
        createdPreference.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(createdPreference.Id, out _).Should().BeTrue();

        createdPreference.Version.Should().Be(1);
        createdPreference.CreatedAt.Should().NotBe(default);
        createdPreference.UpdatedAt.Should().BeNull();

        _userPreferenceRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Default preferences
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldCreatePreferenceWithDefaultValues()
    {
        // Arrange
        var command = new CreateUserPreferenceCommand
        {
            UserId = _userId
        };

        UserPreferenceModel? createdPreference = null;

        _userPreferenceRepositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<UserPreferenceModel, CancellationToken>(
                (preference, _) => createdPreference = preference)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ExecuteAsync(command);

        // Assert
        createdPreference.Should().NotBeNull();

        // Appearance
        createdPreference!.Theme.Should().Be(UserThemeEnum.System);
        createdPreference.CompactMode.Should().BeFalse();
        createdPreference.ReducedMotion.Should().BeFalse();
        createdPreference.HighContrast.Should().BeFalse();
        createdPreference.SidebarCollapsed.Should().BeFalse();
        createdPreference.AnimationsEnabled.Should().BeTrue();

        // Localization
        createdPreference.Language.Should().Be("pt-BR");
        createdPreference.TimeZoneId.Should().Be("America/Fortaleza");
        createdPreference.TimeFormat.Should().Be(TimeFormatEnum.Hour24);

        // Notifications
        createdPreference.NotificationsEnabled.Should().BeTrue();
        createdPreference.EmailNotificationsEnabled.Should().BeFalse();
        createdPreference.ReservationRemindersEnabled.Should().BeTrue();
        createdPreference.ReservationUpdatesEnabled.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenCommandIsNull()
    {
        // Arrange
        CreateUserPreferenceCommand command = null!;

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "User preference command is required.");

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>()),
            Times.Never);

        _userPreferenceRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-user-id")]
    [InlineData("123456")]
    public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenUserIdIsInvalid(
        string userId)
    {
        // Arrange
        var command = new CreateUserPreferenceCommand
        {
            UserId = userId
        };

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "User ID must be a valid UUID.");

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>()),
            Times.Never);

        _userPreferenceRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // User existence
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var command = new CreateUserPreferenceCommand
        {
            UserId = _userId
        };

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId))
            .ReturnsAsync((UserModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("User not found.");

        _userPreferenceRepositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _userPreferenceRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Duplicate preferences
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenPreferenceAlreadyExists()
    {
        // Arrange
        var command = new CreateUserPreferenceCommand
        {
            UserId = _userId
        };

        var existingPreference = new UserPreferenceModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = _userId,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _userPreferenceRepositoryMock
            .Setup(repository => repository.GetByUserIdAsync(
                _userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPreference);

        // Act
        var result = await _sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "User preferences already exist.");

        _userPreferenceRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldThrowOperationCanceledException_WhenAlreadyCanceled()
    {
        // Arrange
        var command = new CreateUserPreferenceCommand
        {
            UserId = _userId
        };

        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();

        // Act
        Func<Task> act = async () =>
            await _sut.ExecuteAsync(command, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<string>()),
            Times.Never);

        _userPreferenceRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
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
        var command = new CreateUserPreferenceCommand
        {
            UserId = _userId
        };

        using var cts = new CancellationTokenSource();

        // Act
        await _sut.ExecuteAsync(command, cts.Token);

        // Assert
        _userPreferenceRepositoryMock.Verify(
            repository => repository.GetByUserIdAsync(
                _userId,
                cts.Token),
            Times.Once);

        _userPreferenceRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<UserPreferenceModel>(),
                cts.Token),
            Times.Once);
    }
}