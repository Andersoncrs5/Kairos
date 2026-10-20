using FluentAssertions;
using Kairos.IntegrationTests.Config;

namespace Kairos.IntegrationTests.Module.Identity.Permission.Repository;

[Collection("Integration Tests")]
public sealed class PermissionRepositoryTest : BaseIntegrationTest
{
    public PermissionRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsNull_WhenCodeDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetPermissionRepository(scope);

        var code = $"missing.{Guid.NewGuid():N}";

        var result = await repository.GetByCodeAsync(code);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsPermission_WhenCodeExists()
    {
        using var scope = CreateScope();
        var repository = GetPermissionRepository(scope);

        var code = $"test.permission.{Guid.NewGuid():N}";

        var permission = await CreatePermissionAsync(
            scope.ServiceProvider,
            permission => permission.Code = code);

        var result = await repository.GetByCodeAsync(code);

        result.Should().NotBeNull();
        result!.Id.Should().Be(permission.Id);
        result.Code.Should().Be(code);
    }

    [Fact]
    public async Task ExistsByCodeAsync_ReturnsFalse_WhenCodeDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetPermissionRepository(scope);

        var exists = await repository.ExistsByCodeAsync(
            $"missing.{Guid.NewGuid():N}");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByCodeAsync_ReturnsTrue_WhenCodeExists()
    {
        using var scope = CreateScope();
        var repository = GetPermissionRepository(scope);

        var permission = await CreatePermissionAsync(
            scope.ServiceProvider);

        var exists = await repository.ExistsByCodeAsync(permission.Code);

        exists.Should().BeTrue();
    }

    
}