using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Seed;
using Moq;

namespace Kairos.Tests.Module.Identity.Permissions.Seed;

public class PermissionSeedTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IPermissionRepository> _permissionRepositoryMock = new();

    private readonly PermissionSeeder _sut;

    public PermissionSeedTest()
    {
        _uowMock
            .Setup(uow => uow.PermissionRepository)
            .Returns(_permissionRepositoryMock.Object);

        // Executa a operação recebida para testar o fluxo do seeder.
        _uowMock
            .Setup(uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Func<CancellationToken, Task> operation,
                CancellationToken ct) => operation(ct));

        _sut = new PermissionSeeder(_uowMock.Object);
    }

    // ------------------------------------------------------------------
    // Catálogo
    // ------------------------------------------------------------------

   [Fact]
    public void Permissions_ShouldContainAllExpectedPermissions()
    {
        // Arrange
        string[] expectedCodes =
        [
            // Room feedbacks
            "rooms.feedbacks.manage",

            // Room favorites
            "rooms.favorites.create",
            "rooms.favorites.delete",
            "rooms.favorites.list",
            
            // Room equipments
            "rooms.equipments.create",
            "rooms.equipments.read",
            "rooms.equipments.list",
            "rooms.equipments.update",
            "rooms.equipments.delete",

            // Room operating hours
            "rooms.operating-hours.create",
            "rooms.operating-hours.read",
            "rooms.operating-hours.list",
            "rooms.operating-hours.update",
            "rooms.operating-hours.delete",

            // Identity - Users
            "identity.users.create",
            "identity.users.read",
            "identity.users.list",
            "identity.users.update",
            "identity.users.delete",
            "identity.users.status.update",

            // Identity - Permissions
            "identity.permissions.create",
            "identity.permissions.read",
            "identity.permissions.list",
            "identity.permissions.update",
            "identity.permissions.delete",

            // Identity - User permissions
            "identity.permission-users.create",
            "identity.permission-users.read",
            "identity.permission-users.list",
            "identity.permission-users.update",
            "identity.permission-users.delete",

            // Categories
            "categories.create",
            "categories.read",
            "categories.list",
            "categories.update",
            "categories.delete",

            // Rooms
            "rooms.create",
            "rooms.read",
            "rooms.list",
            "rooms.update",
            "rooms.delete",

            // Room blockouts
            "rooms.blockouts.create",
            "rooms.blockouts.cancel",
            "rooms.blockouts.read",
            "rooms.blockouts.list",
            "rooms.blockouts.update",
            "rooms.blockouts.delete",

            // Room availability
            "rooms.availability.read",

            // Room types
            "room-types.create",
            "room-types.read",
            "room-types.list",
            "room-types.update",
            "room-types.delete",

            // Reservations
            "reservations.create",
            "reservations.delete",
            "reservations.read",
            "reservations.list",
            "reservations.update",
            "reservations.cancel",

            // Reservation attendees
            "reservations.attendees.create",
            "reservations.attendees.read",
            "reservations.attendees.list",
            "reservations.attendees.update",
            "reservations.attendees.delete",
            "reservations.attendees.respond",

            // Reservation history
            "reservations.history.read",
            "reservations.history.list",

            // Equipments
            "equipments.create",
            "equipments.read",
            "equipments.list",
            "equipments.update",
            "equipments.delete"
        ];

        // Act
        IReadOnlyList<PermissionsModel> permissions =
            PermissionSeeder.Permissions;

        // Assert
        permissions
            .Select(permission => permission.Code)
            .Should()
            .BeEquivalentTo(expectedCodes);

        permissions
            .Select(permission => permission.Code)
            .Should()
            .OnlyHaveUniqueItems();
    }

    [Fact]
    public void Permissions_ShouldUseExpectedModuleForEachPermission()
    {
        // Arrange
        IReadOnlyList<PermissionsModel> permissions =
            PermissionSeeder.Permissions;

        // Act & Assert
        permissions.Should().OnlyContain(
            permission =>
                permission.Module == ExpectedModuleFor(permission.Code));
    }

    [Fact]
    public void Permissions_ShouldHaveCorrectRoomBlockoutCancelMetadata()
    {
        // Arrange & Act
        PermissionsModel permission = PermissionSeeder.Permissions
            .Single(permission =>
                permission.Code == "rooms.blockouts.cancel");

        // Assert
        permission.Name.Should().Be("Cancel room blockouts");
        permission.Module.Should().Be(PermissionModuleEnum.Rooms);
        permission.IsSystem.Should().BeTrue();
        permission.IsActive.Should().BeTrue();
        
        permission.CreatedAt.Should().NotBe(default);
    }

    private static PermissionModuleEnum ExpectedModuleFor(string code)
    {
        if (code.StartsWith("identity.", StringComparison.Ordinal))
        {
            return PermissionModuleEnum.Identity;
        }

        if (code.StartsWith("categories.", StringComparison.Ordinal))
        {
            return PermissionModuleEnum.Categories;
        }

        if (code.StartsWith("rooms.", StringComparison.Ordinal) ||
            code.StartsWith("equipments.", StringComparison.Ordinal))
        {
            return PermissionModuleEnum.Rooms;
        }

        if (code.StartsWith("room-types.", StringComparison.Ordinal))
        {
            return PermissionModuleEnum.RoomTypes;
        }

        if (code.StartsWith("reservations.", StringComparison.Ordinal))
        {
            return PermissionModuleEnum.Reservations;
        }

        throw new ArgumentException(
            $"Permission code '{code}' does not belong to a known module.",
            nameof(code));
    }

    // ------------------------------------------------------------------
    // Inserção
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldInsertAllPermissions_WhenNoneExist()
    {
        // Arrange
        HashSet<string> existingCodes = new(
            StringComparer.OrdinalIgnoreCase);

        List<PermissionsModel> insertedPermissions = [];

        _permissionRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                string code,
                CancellationToken _) => existingCodes.Contains(code));

        _permissionRepositoryMock
            .Setup(repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()))
            .Callback((
                IEnumerable<PermissionsModel> permissions,
                CancellationToken _) =>
            {
                List<PermissionsModel> batch = permissions.ToList();

                insertedPermissions.AddRange(batch);

                foreach (PermissionsModel permission in batch)
                {
                    existingCodes.Add(permission.Code);
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute();

        // Assert
        insertedPermissions
            .Select(permission => permission.Code)
            .Should()
            .BeEquivalentTo(
                PermissionSeeder.Permissions.Select(
                    permission => permission.Code));

        _permissionRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(PermissionSeeder.Permissions.Count));

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _uowMock.Verify(
            uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldInsertOnlyMissingPermissions()
    {
        // Arrange
        HashSet<string> existingCodes = PermissionSeeder.Permissions
            .Take(5)
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] expectedMissingCodes = PermissionSeeder.Permissions
            .Where(permission => !existingCodes.Contains(permission.Code))
            .Select(permission => permission.Code)
            .ToArray();

        List<PermissionsModel> insertedPermissions = [];

        _permissionRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                string code,
                CancellationToken _) => existingCodes.Contains(code));

        _permissionRepositoryMock
            .Setup(repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()))
            .Callback((
                IEnumerable<PermissionsModel> permissions,
                CancellationToken _) =>
            {
                List<PermissionsModel> batch = permissions.ToList();

                insertedPermissions.AddRange(batch);

                foreach (PermissionsModel permission in batch)
                {
                    existingCodes.Add(permission.Code);
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute();

        // Assert
        insertedPermissions
            .Select(permission => permission.Code)
            .Should()
            .BeEquivalentTo(expectedMissingCodes);

        insertedPermissions.Should().HaveCount(
            PermissionSeeder.Permissions.Count - 5);

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldNotInsertAnything_WhenAllPermissionsExist()
    {
        // Arrange
        HashSet<string> existingCodes = PermissionSeeder.Permissions
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _permissionRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                string code,
                CancellationToken _) => existingCodes.Contains(code));

        // Act
        await _sut.Execute();

        // Assert
        _permissionRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(PermissionSeeder.Permissions.Count));

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _uowMock.Verify(
            uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------
    // Idempotência
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldBeIdempotent_WhenExecutedMultipleTimes()
    {
        // Arrange
        HashSet<string> persistedCodes = new(
            StringComparer.OrdinalIgnoreCase);

        List<PermissionsModel> insertedPermissions = [];

        _permissionRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                string code,
                CancellationToken _) => persistedCodes.Contains(code));

        _permissionRepositoryMock
            .Setup(repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()))
            .Callback((
                IEnumerable<PermissionsModel> permissions,
                CancellationToken _) =>
            {
                List<PermissionsModel> batch = permissions.ToList();

                insertedPermissions.AddRange(batch);

                foreach (PermissionsModel permission in batch)
                {
                    persistedCodes.Add(permission.Code);
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute();
        await _sut.Execute();

        // Assert
        insertedPermissions.Should().HaveCount(
            PermissionSeeder.Permissions.Count);

        persistedCodes.Should().HaveCount(
            PermissionSeeder.Permissions.Count);

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _uowMock.Verify(
            uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ------------------------------------------------------------------
    // Cancelamento
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrowOperationCanceledException_WhenAlreadyCanceled()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Func<Task> act = () => _sut.Execute(cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _uowMock.Verify(
            uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldPropagateCancellationTokenToRepository()
    {
        // Arrange
        using CancellationTokenSource cts = new();

        CancellationToken receivedToken = default;

        _permissionRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback((
                string _,
                CancellationToken token) => receivedToken = token)
            .ReturnsAsync(false);

        _permissionRepositoryMock
            .Setup(repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(cts.Token);

        // Assert
        receivedToken.Should().Be(cts.Token);

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                cts.Token),
            Times.Once);
    }
    
    [Theory]
    [InlineData(
        "rooms.feedbacks.manage",
        "Manage room feedback",
        "Allows viewing and deleting room feedback written by any user.")]
    [InlineData(
        "rooms.favorites.create",
        "Favorite rooms",
        "Allows adding rooms to the user's favorites.")]
    [InlineData(
        "rooms.favorites.delete",
        "Unfavorite rooms",
        "Allows removing rooms from the user's favorites.")]
    [InlineData(
        "rooms.favorites.list",
        "List favorite rooms",
        "Allows listing the user's favorite rooms with filters and pagination.")]
    public void Permissions_ShouldHaveCorrectMetadataForRoomFeatures(
        string code,
        string expectedName,
        string expectedDescription)
    {
        // Arrange & Act
        PermissionsModel permission = PermissionSeeder.Permissions
            .Single(permission => permission.Code == code);

        // Assert
        permission.Name.Should().Be(expectedName);
        permission.Description.Should().Be(expectedDescription);
        permission.Module.Should().Be(PermissionModuleEnum.Rooms);
        permission.IsSystem.Should().BeTrue();
        permission.IsActive.Should().BeTrue();
        permission.CreatedAt.Should().NotBe(default);
    }
    
}