using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Seed;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Identity.FindPermissionUser.Seed;

public class LinkAllPermissionToUserMasterTest
{
    private const string MasterUserName = "master";
    private const string MasterUserId = "master-user-id";

    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IPermissionRepository> _permissionRepositoryMock = new();
    private readonly Mock<IPermissionUserRepository> _permissionUserRepositoryMock = new();
    private readonly Mock<ILogger<LinkAllPermissionToUserMaster>> _loggerMock = new();

    private readonly UserModel _masterUser;
    private readonly Dictionary<string, PermissionUserModel> _existingLinks = [];
    private readonly List<PermissionUserModel> _addedLinks = [];
    private readonly List<PermissionUserModel> _updatedLinks = [];

    private readonly LinkAllPermissionToUserMaster _sut;

    public LinkAllPermissionToUserMasterTest()
    {
        _masterUser = CreateMasterUser();

        _uowMock
            .Setup(uow => uow.UserRepository)
            .Returns(_userRepositoryMock.Object);

        _uowMock
            .Setup(uow => uow.PermissionRepository)
            .Returns(_permissionRepositoryMock.Object);

        _uowMock
            .Setup(uow => uow.PermissionUserRepository)
            .Returns(_permissionUserRepositoryMock.Object);

        _userRepositoryMock
            .Setup(repository => repository.GetByUsername(MasterUserName))
            .ReturnsAsync(_masterUser);

        _userRepositoryMock
            .Setup(repository => repository.Update(It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Success);

        _permissionUserRepositoryMock
            .Setup(repository => repository.GetByUserIdAndPermissionIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                string userId,
                string permissionId,
                CancellationToken _) =>
            {
                if (_existingLinks.TryGetValue(permissionId, out var link) &&
                    link.UserId == userId)
                {
                    return link;
                }

                return null;
            });

        _permissionUserRepositoryMock
            .Setup(repository => repository.AddAsync(
                It.IsAny<PermissionUserModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<PermissionUserModel, CancellationToken>((link, _) =>
            {
                _addedLinks.Add(link);
                _existingLinks[link.PermissionId] = link;
            })
            .Returns(Task.CompletedTask);

        _permissionUserRepositoryMock
            .Setup(repository => repository.Update(
                It.IsAny<PermissionUserModel>()))
            .Callback<PermissionUserModel>(link =>
            {
                _updatedLinks.Add(link);
                _existingLinks[link.PermissionId] = link;
            });

        // Executa a operação recebida, simulando o fluxo transacional.
        _uowMock
            .Setup(uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Func<CancellationToken, Task> operation,
                CancellationToken ct) => operation(ct));

        _sut = new LinkAllPermissionToUserMaster(
            _uowMock.Object,
            Options.Create(new UserMasterOptions
            {
                UserName = MasterUserName,
                Email = "master@example.com",
                FullName = "Master User",
                Password = "MasterPassword123!"
            }),
            _loggerMock.Object);
    }

    // ------------------------------------------------------------------
    // Criação dos vínculos
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldCreateAllLinks_WhenNoLinksExist()
    {
        // Arrange
        PermissionsModel[] permissions =
        [
            CreatePermission("permission-1", "identity.users.create"),
            CreatePermission("permission-2", "identity.users.read"),
            CreatePermission("permission-3", "identity.users.update")
        ];

        ConfigurePermissions(permissions);

        // Act
        await _sut.Execute();

        // Assert - vínculos
        _addedLinks.Should().HaveCount(permissions.Length);

        _addedLinks.Select(link => link.PermissionId)
            .Should()
            .BeEquivalentTo(permissions.Select(permission => permission.Id));

        _addedLinks.Should().OnlyContain(
            link => link.UserId == MasterUserId && link.IsActive);

        // Assert - array de permissões do usuário
        _masterUser.Permissions.Should().BeEquivalentTo(
            permissions.Select(permission => permission.Code));

        _userRepositoryMock.Verify(
            repository => repository.Update(_masterUser),
            Times.Once);

        // Assert - transação
        VerifyTransaction(Times.Once());
    }

    [Fact]
    public async Task Execute_ShouldLinkEachPermissionToMasterUser()
    {
        // Arrange
        PermissionsModel[] permissions =
        [
            CreatePermission("permission-1", "identity.users.create"),
            CreatePermission("permission-2", "identity.users.read")
        ];

        ConfigurePermissions(permissions);

        // Act
        await _sut.Execute();

        // Assert
        foreach (PermissionsModel permission in permissions)
        {
            _permissionUserRepositoryMock.Verify(
                repository => repository.GetByUserIdAndPermissionIdAsync(
                    MasterUserId,
                    permission.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        _addedLinks.Should().ContainSingle(
            link => link.PermissionId == permissions[0].Id);

        _addedLinks.Should().ContainSingle(
            link => link.PermissionId == permissions[1].Id);
    }

    // ------------------------------------------------------------------
    // Vínculos existentes
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReactivateInactiveLinks_AndCreateMissingLinks()
    {
        // Arrange
        PermissionsModel[] permissions =
        [
            CreatePermission("permission-1", "identity.users.create"),
            CreatePermission("permission-2", "identity.users.read"),
            CreatePermission("permission-3", "identity.users.update")
        ];

        ConfigurePermissions(permissions);

        PermissionUserModel activeLink = CreateLink(
            MasterUserId,
            permissions[0].Id,
            isActive: true);

        PermissionUserModel inactiveLink = CreateLink(
            MasterUserId,
            permissions[1].Id,
            isActive: false);

        _existingLinks[activeLink.PermissionId] = activeLink;
        _existingLinks[inactiveLink.PermissionId] = inactiveLink;

        // Act
        await _sut.Execute();

        // Assert - o vínculo ativo foi preservado
        _updatedLinks.Should().ContainSingle(
            link => link.PermissionId == permissions[1].Id);

        activeLink.IsActive.Should().BeTrue();

        // Assert - o inativo foi reativado
        inactiveLink.IsActive.Should().BeTrue();
        inactiveLink.UpdatedAt.Should().NotBeNull();

        // Assert - somente a permissão ausente foi inserida
        _addedLinks.Should().ContainSingle();
        _addedLinks[0].PermissionId.Should().Be(permissions[2].Id);

        _masterUser.Permissions.Should().BeEquivalentTo(
            permissions.Select(permission => permission.Code));
    }

    [Fact]
    public async Task Execute_ShouldNotDuplicatePermissionCodes_WhenSomeAlreadyExist()
    {
        // Arrange
        PermissionsModel[] permissions =
        [
            CreatePermission("permission-1", "identity.users.read"),
            CreatePermission("permission-2", "identity.users.update")
        ];

        ConfigurePermissions(permissions);

        _masterUser.Permissions =
        [
            "identity.users.read",
            "CUSTOM.permission"
        ];

        _existingLinks[permissions[0].Id] = CreateLink(
            MasterUserId,
            permissions[0].Id,
            isActive: true);

        // Act
        await _sut.Execute();

        // Assert
        _masterUser.Permissions.Should().Contain(
            "CUSTOM.permission");

        _masterUser.Permissions.Should().Contain(
            "identity.users.read");

        _masterUser.Permissions.Should().Contain(
            "identity.users.update");

        _masterUser.Permissions
            .Where(code => code == "identity.users.read")
            .Should()
            .ContainSingle();

        _addedLinks.Should().ContainSingle(
            link => link.PermissionId == permissions[1].Id);
    }

    // ------------------------------------------------------------------
    // Idempotência
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldNotCreateOrUpdateAnything_WhenEverythingIsLinked()
    {
        // Arrange
        PermissionsModel[] permissions =
        [
            CreatePermission("permission-1", "identity.users.read"),
            CreatePermission("permission-2", "identity.users.update")
        ];

        ConfigurePermissions(permissions);

        _masterUser.Permissions =
            permissions.Select(permission => permission.Code).ToArray();

        foreach (PermissionsModel permission in permissions)
        {
            PermissionUserModel link = CreateLink(
                MasterUserId,
                permission.Id,
                isActive: true);

            _existingLinks[permission.Id] = link;
        }

        // Act
        await _sut.Execute();

        // Assert
        _addedLinks.Should().BeEmpty();
        _updatedLinks.Should().BeEmpty();

        _userRepositoryMock.Verify(
            repository => repository.Update(It.IsAny<UserModel>()),
            Times.Never);

        VerifyTransaction(Times.Once());
    }

    // ------------------------------------------------------------------
    // Erros de negócio
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow_WhenMasterUserDoesNotExist()
    {
        // Arrange
        _userRepositoryMock
            .Setup(repository => repository.GetByUsername(MasterUserName))
            .ReturnsAsync((UserModel?)null);

        ConfigurePermissions(
            CreatePermission("permission-1", "identity.users.read"));

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Master user*was not found*");

        _permissionRepositoryMock.Verify(
            repository => repository.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        _permissionUserRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<PermissionUserModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldThrow_WhenPermissionIdIsMissing()
    {
        // Arrange
        PermissionsModel permission = CreatePermission(
            "",
            "identity.users.read");

        ConfigurePermissions(permission);

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*invalid ID*");

        _permissionUserRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<PermissionUserModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldThrow_WhenPermissionCodeIsMissing()
    {
        // Arrange
        PermissionsModel permission = CreatePermission(
            "permission-1",
            "");

        ConfigurePermissions(permission);

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*invalid code*");

        _permissionUserRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<PermissionUserModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldThrow_WhenUpdatingMasterPermissionsFails()
    {
        // Arrange
        PermissionsModel permission = CreatePermission(
            "permission-1",
            "identity.users.read");

        ConfigurePermissions(permission);

        _userRepositoryMock
            .Setup(repository => repository.Update(It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError
                {
                    Code = "UpdateFailed",
                    Description = "User update failed."
                }));

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to update the master user's permission array*");

        _userRepositoryMock.Verify(
            repository => repository.Update(_masterUser),
            Times.Once);
    }

    // ------------------------------------------------------------------
    // Cancelamento
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow_WhenCancellationIsAlreadyRequested()
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
            repository => repository.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        _permissionUserRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<PermissionUserModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldPropagateCancellationTokenToRepositories()
    {
        // Arrange
        using CancellationTokenSource cts = new();

        ConfigurePermissions(
            CreatePermission("permission-1", "identity.users.read"));

        // Act
        await _sut.Execute(cts.Token);

        // Assert
        _permissionRepositoryMock.Verify(
            repository => repository.GetAllAsync(cts.Token),
            Times.Once);

        _permissionUserRepositoryMock.Verify(
            repository => repository.GetByUserIdAndPermissionIdAsync(
                MasterUserId,
                "permission-1",
                cts.Token),
            Times.Once);

        _permissionUserRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.IsAny<PermissionUserModel>(),
                cts.Token),
            Times.Once);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private void ConfigurePermissions(
        params PermissionsModel[] permissions)
    {
        _permissionRepositoryMock
            .Setup(repository => repository.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PermissionsModel>)permissions);
    }

    private void VerifyTransaction(Times times)
    {
        _uowMock.Verify(
            uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            times);
    }

    private static UserModel CreateMasterUser()
    {
        return new UserModel
        {
            Id = MasterUserId,
            UserName = MasterUserName,
            Email = "master@example.com",
            FullName = "Master User",
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static PermissionsModel CreatePermission(
        string id,
        string code)
    {
        return new PermissionsModel
        {
            Id = id,
            Code = code,
            Name = string.IsNullOrWhiteSpace(code)
                ? "Test permission"
                : code,
            Module = PermissionModuleEnum.Identity,
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static PermissionUserModel CreateLink(
        string userId,
        string permissionId,
        bool isActive)
    {
        return new PermissionUserModel
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            PermissionId = permissionId,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}