using FluentAssertions;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.IntegrationTests.Module.Identity.User.Repository;

[Collection("Integration Tests")]
public sealed class UserRepositoryTest : BaseIntegrationTest
{
    public UserRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ExistsByIdAsync_ReturnsFalse_WhenUserDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var exists = await repository.ExistsByIdAsync(
            Guid.NewGuid().ToString());

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByIdAsync_ReturnsTrue_WhenUserExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var exists = await repository.ExistsByIdAsync(user.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser_WhenUserExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var result = await repository.GetByIdAsync(user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByEmail_ReturnsUser_WhenEmailExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var result = await repository.GetByEmail(user.Email!);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task ExistsByEmail_ReturnsFalse_WhenEmailDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var exists = await repository.ExistsByEmail(
            $"{Guid.NewGuid():N}@example.com");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByEmail_ReturnsTrue_WhenEmailExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var exists = await repository.ExistsByEmail(user.Email!);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task GetByUsername_ReturnsUser_WhenUsernameExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var result = await repository.GetByUsername(user.UserName!);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task ExistsByUsername_ReturnsFalse_WhenUsernameDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var exists = await repository.ExistsByUsername(
            $"missing-{Guid.NewGuid():N}");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByUsername_ReturnsTrue_WhenUsernameExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var exists = await repository.ExistsByUsername(user.UserName!);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPassword_ReturnsTrue_WhenPasswordIsCorrect()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var isValid = await repository.CheckPassword(user, ValidPassword);

        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPassword_ReturnsFalse_WhenPasswordIsIncorrect()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var isValid = await repository.CheckPassword(user, "Wrong123!Pass");

        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetByRefreshToken_ReturnsUser_WhenTokenIsValid()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var refreshToken = Guid.NewGuid().ToString("N");

        var user = await CreateUserAsync(
            scope.ServiceProvider,
            user =>
            {
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime =
                    DateTimeOffset.UtcNow.AddMinutes(10);
            });

        var result = await repository.GetByRefreshToken(refreshToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByRefreshToken_ReturnsNull_WhenTokenIsExpired()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope.ServiceProvider.CreateScope());

        var refreshToken = Guid.NewGuid().ToString("N");

        await CreateUserAsync(
            scope.ServiceProvider,
            user =>
            {
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime =
                    DateTimeOffset.UtcNow.AddMinutes(-10);
            });

        var result = await repository.GetByRefreshToken(refreshToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Update_ReturnsSuccess_AndPersistsChanges()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);
        var newName = $"Updated-{Guid.NewGuid():N}";

        user.FullName = newName;

        var updateResult = await repository.Update(user);

        updateResult.Succeeded.Should().BeTrue();

        var updatedUser = await repository.GetByIdAsync(user.Id);

        updatedUser.Should().NotBeNull();
        updatedUser!.FullName.Should().Be(newName);
        updatedUser.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteByIdAsync_ReturnsFailure_WhenUserDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var result = await repository.DeleteByIdAsync(
            Guid.NewGuid().ToString());

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(
            error => error.Code == "UserNotFound");
    }

    [Fact]
    public async Task DeleteByIdAsync_DeletesUser_WhenUserExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var deleteResult = await repository.DeleteByIdAsync(user.Id);

        deleteResult.Succeeded.Should().BeTrue();

        var deletedUser = await repository.GetByIdAsync(user.Id);

        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task CountAsync_Increases_WhenUserIsCreated()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var countBefore = await repository.CountAsync();

        await CreateUserAsync(scope.ServiceProvider);

        var countAfter = await repository.CountAsync();

        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task ExistsPermission_ReturnsTrue_WhenUserHasPermission()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        const string permission = "rooms.read";

        var user = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.Permissions = [permission]);

        var exists = await repository.ExistsPermission(permission, user.Id);

        exists.Should().BeTrue();
    }

    private static IUserRepository GetRepository(IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IUserRepository>();
    }

    
}
