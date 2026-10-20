using FluentAssertions;
using Kairos.IntegrationTests.Config;

namespace Kairos.IntegrationTests.Module.Identity.PermissionUser.Repository;

[Collection("Integration Tests")]
public sealed class PermissionUserRepositoryTest : BaseIntegrationTest
{
    public PermissionUserRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsEmpty_WhenUserHasNoPermissionLinks()
    {
        using var scope = CreateScope();
        var repository = GetPermissionUserRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var result = await repository.GetByUserIdAsync(user.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsLinks_WhenUserHasPermissions()
    {
        using var scope = CreateScope();
        var repository = GetPermissionUserRepository(scope);

        var permissionUser = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        var result = await repository.GetByUserIdAsync(
            permissionUser.UserId);

        result.Should().ContainSingle(
            item => item.Id == permissionUser.Id);
    }

    [Fact]
    public async Task GetByUserIdAndPermissionIdAsync_ReturnsNull_WhenLinkDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetPermissionUserRepository(scope);

        var user = await CreateUserAsync(scope.ServiceProvider);
        var permission = await CreatePermissionAsync(scope.ServiceProvider);

        var result = await repository.GetByUserIdAndPermissionIdAsync(
            user.Id,
            permission.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAndPermissionIdAsync_ReturnsLink_WhenLinkExists()
    {
        using var scope = CreateScope();
        var repository = GetPermissionUserRepository(scope);

        var permissionUser = await CreatePermissionUserAsync(
            scope.ServiceProvider);

        var result = await repository.GetByUserIdAndPermissionIdAsync(
            permissionUser.UserId,
            permissionUser.PermissionId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(permissionUser.Id);
    }

    [Fact]
    public async Task HasActivePermissionAsync_ReturnsTrue_WhenLinkIsActive()
    {
        using var scope = CreateScope();
        var repository = GetPermissionUserRepository(scope);

        var permissionUser = await CreatePermissionUserAsync(
            scope.ServiceProvider,
            isActive: true);

        var result = await repository.HasActivePermissionAsync(
            permissionUser.UserId,
            permissionUser.PermissionId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActivePermissionAsync_ReturnsFalse_WhenLinkIsInactive()
    {
        using var scope = CreateScope();
        var repository = GetPermissionUserRepository(scope);

        var permissionUser = await CreatePermissionUserAsync(
            scope.ServiceProvider,
            isActive: false);

        var result = await repository.HasActivePermissionAsync(
            permissionUser.UserId,
            permissionUser.PermissionId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActivePermissionAsync_ReturnsFalse_WhenLinkDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetPermissionUserRepository(scope);

        var result = await repository.HasActivePermissionAsync(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());

        result.Should().BeFalse();
    }

    
}