using System.Collections.ObjectModel;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Seed;
using Moq;

namespace Kairos.Tests.Module.Identity.Permissions.Seed;

public sealed class PermissionSeedTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IPermissionRepository> _permissionRepositoryMock = new();

    private readonly PermissionSeeder _sut;

    private static readonly string[] ExpectedCodes =
    [
        // ----------------------------------------------------------
        // Reservation attachments
        // ----------------------------------------------------------

        "reservations.attachments.create",
        "reservations.attachments.read",
        "reservations.attachments.list",
        "reservations.attachments.update",
        "reservations.attachments.delete",
        "reservations.attachments.download",

        // ----------------------------------------------------------
        // Room feedback
        // ----------------------------------------------------------

        "rooms.feedbacks.create",
        "rooms.feedbacks.read",
        "rooms.feedbacks.list",
        "rooms.feedbacks.update",
        "rooms.feedbacks.delete",
        "rooms.feedbacks.summary",
        "rooms.feedbacks.manage",

        // ----------------------------------------------------------
        // Room favorites
        // ----------------------------------------------------------

        "rooms.favorites.create",
        "rooms.favorites.delete",
        "rooms.favorites.list",

        // ----------------------------------------------------------
        // Room equipments
        // ----------------------------------------------------------

        "rooms.equipments.create",
        "rooms.equipments.read",
        "rooms.equipments.list",
        "rooms.equipments.update",
        "rooms.equipments.delete",

        // ----------------------------------------------------------
        // Room operating hours
        // ----------------------------------------------------------

        "rooms.operating-hours.create",
        "rooms.operating-hours.read",
        "rooms.operating-hours.list",
        "rooms.operating-hours.update",
        "rooms.operating-hours.delete",

        // ----------------------------------------------------------
        // Identity - Users
        // ----------------------------------------------------------

        "identity.users.create",
        "identity.users.read",
        "identity.users.list",
        "identity.users.update",
        "identity.users.delete",
        "identity.users.status.update",

        // ----------------------------------------------------------
        // Identity - Permissions
        // ----------------------------------------------------------

        "identity.permissions.create",
        "identity.permissions.read",
        "identity.permissions.list",
        "identity.permissions.update",
        "identity.permissions.delete",

        // ----------------------------------------------------------
        // Identity - User permissions
        // ----------------------------------------------------------

        "identity.permission-users.create",
        "identity.permission-users.read",
        "identity.permission-users.list",
        "identity.permission-users.update",
        "identity.permission-users.delete",

        // ----------------------------------------------------------
        // Categories
        // ----------------------------------------------------------

        "categories.create",
        "categories.read",
        "categories.list",
        "categories.update",
        "categories.delete",

        // ----------------------------------------------------------
        // Rooms
        // ----------------------------------------------------------

        "rooms.create",
        "rooms.read",
        "rooms.list",
        "rooms.update",
        "rooms.delete",

        // ----------------------------------------------------------
        // Room types
        // ----------------------------------------------------------

        "room-types.create",
        "room-types.read",
        "room-types.list",
        "room-types.update",
        "room-types.delete",

        // ----------------------------------------------------------
        // Room blockouts
        // ----------------------------------------------------------

        "rooms.blockouts.create",
        "rooms.blockouts.cancel",
        "rooms.blockouts.read",
        "rooms.blockouts.list",
        "rooms.blockouts.update",
        "rooms.blockouts.delete",

        // ----------------------------------------------------------
        // Room availability
        // ----------------------------------------------------------

        "rooms.availability.read",

        // ----------------------------------------------------------
        // Reservations
        // ----------------------------------------------------------

        "reservations.create",
        "reservations.delete",
        "reservations.read",
        "reservations.list",
        "reservations.update",
        "reservations.cancel",

        // ----------------------------------------------------------
        // Reservation attendees
        // ----------------------------------------------------------

        "reservations.attendees.create",
        "reservations.attendees.read",
        "reservations.attendees.list",
        "reservations.attendees.update",
        "reservations.attendees.delete",
        "reservations.attendees.respond",

        // ----------------------------------------------------------
        // Reservation history
        // ----------------------------------------------------------

        "reservations.history.read",
        "reservations.history.list",

        // ----------------------------------------------------------
        // Equipments
        // ----------------------------------------------------------

        "equipments.create",
        "equipments.read",
        "equipments.list",
        "equipments.update",
        "equipments.delete"
    ];

    public PermissionSeedTest()
    {
        _uowMock
            .Setup(uow => uow.PermissionRepository)
            .Returns(_permissionRepositoryMock.Object);

        _uowMock
            .Setup(uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Func<CancellationToken, Task> operation,
                CancellationToken ct) => operation(ct));

        _sut = new PermissionSeeder(_uowMock.Object);
    }

    // ===============================================================
    // Permission catalog
    // ===============================================================

    [Fact]
    public void Permissions_ShouldContainAllExpectedPermissions()
    {
        // Arrange
        IReadOnlyList<PermissionsModel> permissions =
            PermissionSeeder.Permissions;

        string[] actualCodes = permissions
            .Select(permission => permission.Code)
            .ToArray();

        // Act & Assert
        actualCodes.Should()
            .BeEquivalentTo(ExpectedCodes);

        actualCodes.Should()
            .HaveCount(83);

        actualCodes.Should()
            .OnlyHaveUniqueItems();
    }

    [Fact]
    public void Permissions_ShouldHaveValidMetadata()
    {
        // Arrange
        IReadOnlyList<PermissionsModel> permissions =
            PermissionSeeder.Permissions;

        // Act & Assert
        permissions.Should().NotBeEmpty();

        permissions.Should().OnlyContain(permission =>
            !string.IsNullOrWhiteSpace(permission.Id) &&
            !string.IsNullOrWhiteSpace(permission.Code) &&
            !string.IsNullOrWhiteSpace(permission.Name) &&
            !string.IsNullOrWhiteSpace(permission.Description) &&
            permission.IsSystem &&
            permission.IsActive &&
            permission.CreatedAt != default);
    }

    [Fact]
    public void Permissions_ShouldHaveUniqueIdentifiers()
    {
        // Arrange
        IReadOnlyList<string> identifiers = PermissionSeeder.Permissions
            .Select(permission => permission.Id)
            .ToList();

        // Act & Assert
        identifiers.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Permissions_ShouldHaveUniqueCodesIgnoringCase()
    {
        // Arrange
        IReadOnlyList<string> codes = PermissionSeeder.Permissions
            .Select(permission => permission.Code)
            .ToList();

        // Act
        int uniqueCount = codes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        // Assert
        uniqueCount.Should().Be(codes.Count);
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
    public void Permissions_ShouldBeReadOnly()
    {
        // Arrange
        IReadOnlyList<PermissionsModel> permissions =
            PermissionSeeder.Permissions;

        // Act & Assert
        permissions.Should().NotBeNull();

        permissions.Should().BeAssignableTo<IReadOnlyList<PermissionsModel>>();

        permissions.Should().BeAssignableTo<IList<PermissionsModel>>();
        permissions.Should().BeAssignableTo<ReadOnlyCollection<PermissionsModel>>();
    }

    // ===============================================================
    // Reservation attachments metadata
    // ===============================================================

    [Theory]
    [InlineData(
        "reservations.attachments.create",
        "Upload reservation attachments",
        "Allows uploading attachments to a reservation.")]
    [InlineData(
        "reservations.attachments.read",
        "Read reservation attachments",
        "Allows retrieving a reservation attachment by ID.")]
    [InlineData(
        "reservations.attachments.list",
        "List reservation attachments",
        "Allows listing attachments of a reservation.")]
    [InlineData(
        "reservations.attachments.update",
        "Update reservation attachments",
        "Allows updating attachment name, description and visibility.")]
    [InlineData(
        "reservations.attachments.delete",
        "Delete reservation attachments",
        "Allows deleting reservation attachments.")]
    [InlineData(
        "reservations.attachments.download",
        "Download reservation attachments",
        "Allows downloading reservation attachment files.")]
    public void Permissions_ShouldHaveCorrectMetadataForReservationAttachments(
        string code,
        string expectedName,
        string expectedDescription)
    {
        // Arrange
        PermissionsModel permission = PermissionSeeder.Permissions
            .Single(permission => permission.Code == code);

        // Act & Assert
        permission.Name.Should().Be(expectedName);
        permission.Description.Should().Be(expectedDescription);

        permission.Module.Should().Be(PermissionModuleEnum.Reservations);

        permission.IsSystem.Should().BeTrue();
        permission.IsActive.Should().BeTrue();
        permission.CreatedAt.Should().NotBe(default);
    }

    // ===============================================================
    // Room blockouts metadata
    // ===============================================================

    [Fact]
    public void Permissions_ShouldHaveCorrectRoomBlockoutCancelMetadata()
    {
        // Arrange
        PermissionsModel permission = PermissionSeeder.Permissions
            .Single(permission =>
                permission.Code == "rooms.blockouts.cancel");

        // Act & Assert
        permission.Name.Should().Be("Cancel room blockouts");

        permission.Description.Should().Be(
            "Allows blocking a room for a time range.");

        permission.Module.Should().Be(PermissionModuleEnum.Rooms);

        permission.IsSystem.Should().BeTrue();
        permission.IsActive.Should().BeTrue();
        permission.CreatedAt.Should().NotBe(default);
    }

    // ===============================================================
    // Room features metadata
    // ===============================================================

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
    public void Permissions_ShouldHaveCorrectMetadataForRoomFeatures(
        string code,
        string expectedName,
        string expectedDescription)
    {
        // Arrange
        PermissionsModel permission = PermissionSeeder.Permissions
            .Single(permission => permission.Code == code);

        // Act & Assert
        permission.Name.Should().Be(expectedName);
        permission.Description.Should().Be(expectedDescription);

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

        if (code.StartsWith("room-types.", StringComparison.Ordinal))
        {
            return PermissionModuleEnum.RoomTypes;
        }

        if (code.StartsWith("reservations.", StringComparison.Ordinal))
        {
            return PermissionModuleEnum.Reservations;
        }

        if (code.StartsWith("rooms.", StringComparison.Ordinal) ||
            code.StartsWith("equipments.", StringComparison.Ordinal))
        {
            return PermissionModuleEnum.Rooms;
        }

        throw new ArgumentException(
            $"Permission code '{code}' does not belong to a known module.",
            nameof(code));
    }

    // ===============================================================
    // Insertion
    // ===============================================================

    [Fact]
    public async Task Execute_ShouldInsertAllPermissions_WhenNoneExist()
    {
        // Arrange
        HashSet<string> existingCodes = new(
            StringComparer.OrdinalIgnoreCase);

        List<PermissionsModel> insertedPermissions = [];

        SetupRepository(existingCodes, insertedPermissions);

        // Act
        await _sut.Execute();

        // Assert
        insertedPermissions
            .Select(permission => permission.Code)
            .Should()
            .BeEquivalentTo(ExpectedCodes);

        insertedPermissions.Should()
            .HaveCount(ExpectedCodes.Length);

        _permissionRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(ExpectedCodes.Length));

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifyTransaction(Times.Once());
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

        SetupRepository(existingCodes, insertedPermissions);

        // Act
        await _sut.Execute();

        // Assert
        insertedPermissions
            .Select(permission => permission.Code)
            .Should()
            .BeEquivalentTo(expectedMissingCodes);

        insertedPermissions.Should()
            .HaveCount(ExpectedCodes.Length - 5);

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifyTransaction(Times.Once());
    }

    [Fact]
    public async Task Execute_ShouldNotInsertAnything_WhenAllPermissionsExist()
    {
        // Arrange
        HashSet<string> existingCodes = PermissionSeeder.Permissions
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        SetupRepository(existingCodes);

        // Act
        await _sut.Execute();

        // Assert
        _permissionRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(ExpectedCodes.Length));

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyTransaction(Times.Once());
    }

    [Fact]
    public async Task Execute_ShouldBeIdempotent_WhenExecutedMultipleTimes()
    {
        // Arrange
        HashSet<string> persistedCodes = new(
            StringComparer.OrdinalIgnoreCase);

        List<PermissionsModel> insertedPermissions = [];

        SetupRepository(persistedCodes, insertedPermissions);

        // Act
        await _sut.Execute();
        await _sut.Execute();

        // Assert
        insertedPermissions.Should()
            .HaveCount(ExpectedCodes.Length);

        persistedCodes.Should()
            .HaveCount(ExpectedCodes.Length);

        persistedCodes.Should()
            .BeEquivalentTo(ExpectedCodes);

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifyTransaction(Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_ShouldNotInsertEmptyBatch_WhenNoPermissionsAreMissing()
    {
        // Arrange
        HashSet<string> existingCodes = PermissionSeeder.Permissions
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        SetupRepository(existingCodes);

        // Act
        await _sut.Execute();

        // Assert
        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.Is<IEnumerable<PermissionsModel>>(
                    permissions => !permissions.Any()),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ===============================================================
    // Cancellation
    // ===============================================================

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

        VerifyTransaction(Times.Never());

        _permissionRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldStopProcessing_WhenCanceledDuringCatalogIteration()
    {
        // Arrange
        using CancellationTokenSource cts = new();

        int existenceChecks = 0;

        _permissionRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                existenceChecks++;

                if (existenceChecks == 1)
                {
                    cts.Cancel();
                }
            })
            .ReturnsAsync(false);

        // Act
        Func<Task> act = () => _sut.Execute(cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        existenceChecks.Should().Be(1);

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyTransaction(Times.Once(), cts.Token);
    }

    [Fact]
    public async Task Execute_ShouldPropagateCancellationTokenToRepositories()
    {
        // Arrange
        using CancellationTokenSource cts = new();

        SetupRepository(new HashSet<string>(
            StringComparer.OrdinalIgnoreCase));

        // Act
        await _sut.Execute(cts.Token);

        // Assert
        _permissionRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                cts.Token),
            Times.Exactly(ExpectedCodes.Length));

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                cts.Token),
            Times.Once);

        VerifyTransaction(Times.Once(), cts.Token);
    }

    // ===============================================================
    // Exception handling
    // ===============================================================

    [Fact]
    public async Task Execute_ShouldPropagateException_WhenPermissionLookupFails()
    {
        // Arrange
        _permissionRepositoryMock
            .Setup(repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Permission lookup failed."));

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission lookup failed.");

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyTransaction(Times.Once());
    }

    [Fact]
    public async Task Execute_ShouldPropagateException_WhenPermissionInsertionFails()
    {
        // Arrange
        SetupRepository(new HashSet<string>(
            StringComparer.OrdinalIgnoreCase));

        _permissionRepositoryMock
            .Setup(repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Permission insertion failed."));

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission insertion failed.");

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        VerifyTransaction(Times.Once());
    }

    [Fact]
    public async Task Execute_ShouldPropagateException_WhenTransactionFails()
    {
        // Arrange
        _uowMock
            .Setup(uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Transaction failed."));

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction failed.");

        _permissionRepositoryMock.Verify(
            repository => repository.ExistsByCodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _permissionRepositoryMock.Verify(
            repository => repository.AddAllAsync(
                It.IsAny<IEnumerable<PermissionsModel>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyTransaction(Times.Once());
    }

    // ===============================================================
    // Helpers
    // ===============================================================

    private void SetupRepository(
        HashSet<string> existingCodes,
        List<PermissionsModel>? insertedPermissions = null)
    {
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

                insertedPermissions?.AddRange(batch);

                foreach (PermissionsModel permission in batch)
                {
                    existingCodes.Add(permission.Code);
                }
            })
            .Returns(Task.CompletedTask);
    }

    private void VerifyTransaction(
        Times times,
        CancellationToken? cancellationToken = null)
    {
        _uowMock.Verify(
            uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.Is<CancellationToken>(ct =>
                    cancellationToken == null ||
                    ct == cancellationToken.Value)),
            times);
    }
}