
using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairos.IntegrationTests.Module.Identity.UserPreference.Repository;

[Collection("Integration Tests")]
public sealed class UserPreferenceRepositoryTest : BaseIntegrationTest
{
    public UserPreferenceRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    // ---------------------------------------------------------------
    // GetByUserIdAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByUserIdAsync_ReturnsPreference_WhenPreferenceExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var preference = await CreatePreferenceAsync(
            scope.ServiceProvider,
            user.Id,
            model =>
            {
                model.CompactMode = true;
                model.ReducedMotion = true;
                model.HighContrast = true;
                model.SidebarCollapsed = true;
                model.AnimationsEnabled = false;

                model.Language = "en-US";
                model.TimeZoneId = "UTC";

                model.EmailNotificationsEnabled = true;
                model.NotificationsEnabled = false;
                model.ReservationRemindersEnabled = false;
                model.ReservationUpdatesEnabled = true;
            });

        var result = await repository.GetByUserIdAsync(user.Id);

        result.Should().NotBeNull();

        result!.Id.Should().Be(preference.Id);
        result.UserId.Should().Be(user.Id);

        // Appearance
        result.Theme.Should().Be(preference.Theme);
        result.CompactMode.Should().BeTrue();
        result.ReducedMotion.Should().BeTrue();
        result.HighContrast.Should().BeTrue();
        result.SidebarCollapsed.Should().BeTrue();
        result.AnimationsEnabled.Should().BeFalse();

        // Localization
        result.Language.Should().Be("en-US");
        result.TimeZoneId.Should().Be("UTC");
        result.TimeFormat.Should().Be(preference.TimeFormat);

        // Notifications
        result.NotificationsEnabled.Should().BeFalse();
        result.EmailNotificationsEnabled.Should().BeTrue();
        result.ReservationRemindersEnabled.Should().BeFalse();
        result.ReservationUpdatesEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsNull_WhenPreferenceDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var result = await repository.GetByUserIdAsync(user.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var nonExistingUserId = Guid.NewGuid().ToString();

        var result = await repository.GetByUserIdAsync(nonExistingUserId);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task GetByUserIdAsync_ThrowsArgumentException_WhenUserIdIsInvalid(
        string? userId)
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var act = async () => await repository.GetByUserIdAsync(userId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---------------------------------------------------------------
    // ExistsByUserIdAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByUserIdAsync_ReturnsTrue_WhenPreferenceExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        await CreatePreferenceAsync(
            scope.ServiceProvider,
            user.Id);

        var exists = await repository.ExistsByUserIdAsync(user.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByUserIdAsync_ReturnsFalse_WhenPreferenceDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var exists = await repository.ExistsByUserIdAsync(user.Id);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByUserIdAsync_ReturnsFalse_WhenUserDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var nonExistingUserId = Guid.NewGuid().ToString();

        var exists = await repository.ExistsByUserIdAsync(nonExistingUserId);

        exists.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExistsByUserIdAsync_ThrowsArgumentException_WhenUserIdIsInvalid(
        string? userId)
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var act = async () => await repository.ExistsByUserIdAsync(userId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static IUserPreferenceRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IUserPreferenceRepository>();
    }

    private static async Task<UserPreferenceModel> CreatePreferenceAsync(
        IServiceProvider services,
        string userId,
        Action<UserPreferenceModel>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var context = services.GetRequiredService<AppDbContext>();

        var preference = new UserPreferenceModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        configure?.Invoke(preference);

        await context.Set<UserPreferenceModel>()
            .AddAsync(preference);

        await context.SaveChangesAsync();

        return preference;
    }
}
