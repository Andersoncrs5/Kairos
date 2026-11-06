using FluentAssertions;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.IntegrationTests.Module.Room.Equipment.Repository;

[Collection("Integration Tests")]
public sealed class EquipmentRepositoryTest : BaseIntegrationTest
{
    public EquipmentRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenNameDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var name = $"Missing Equipment {Guid.NewGuid():N}";

        var result = await repository.GetByNameAsync(name);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsEquipment_WhenNameExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var equipment = await CreateEquipmentAsync(scope.ServiceProvider);

        var result = await repository.GetByNameAsync(equipment.Name);

        result.Should().NotBeNull();
        result!.Id.Should().Be(equipment.Id);
        result.Name.Should().Be(equipment.Name);
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsNull_WhenCodeDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var code = $"missing-{Guid.NewGuid():N}";

        var result = await repository.GetByCodeAsync(code);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsEquipment_WhenCodeExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var equipment = await CreateEquipmentAsync(scope.ServiceProvider);

        var result = await repository.GetByCodeAsync(equipment.Code);

        result.Should().NotBeNull();
        result!.Id.Should().Be(equipment.Id);
        result.Code.Should().Be(equipment.Code);
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsFalse_WhenNameDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var exists = await repository.ExistsByNameAsync(
            $"Missing Equipment {Guid.NewGuid():N}");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsTrue_WhenNameExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var equipment = await CreateEquipmentAsync(scope.ServiceProvider);

        var exists = await repository.ExistsByNameAsync(equipment.Name);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByCodeAsync_ReturnsFalse_WhenCodeDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var exists = await repository.ExistsByCodeAsync(
            $"missing-{Guid.NewGuid():N}");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByCodeAsync_ReturnsTrue_WhenCodeExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var equipment = await CreateEquipmentAsync(scope.ServiceProvider);

        var exists = await repository.ExistsByCodeAsync(equipment.Code);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveEquipments()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);

        var activeEquipment = await CreateEquipmentAsync(
            scope.ServiceProvider);

        var inactiveEquipment = await CreateEquipmentAsync(
            scope.ServiceProvider,
            equipment => equipment.IsActive = false);

        var result = await repository.GetActiveAsync();

        result.Should().Contain(equipment =>
            equipment.Id == activeEquipment.Id);

        result.Should().NotContain(equipment =>
            equipment.Id == inactiveEquipment.Id);
    }

    private static IEquipmentRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IEquipmentRepository>();
    }

    private static async Task<EquipmentModel> CreateEquipmentAsync(
        IServiceProvider services,
        Action<EquipmentModel>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N");

        var equipment = new EquipmentModel
        {
            Id = Guid.NewGuid().ToString(),
            Code = $"eq-{uniqueId}"[..Math.Min(50, $"eq-{uniqueId}".Length)],
            Name = $"Integration Equipment {uniqueId}",
            Description = "Equipment created by integration test.",
            IsActive = true,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        configure?.Invoke(equipment);

        await dbContext.Set<EquipmentModel>().AddAsync(
            equipment,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return equipment;
    }
}