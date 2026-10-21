using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.Permissions.Services;

public sealed class UpdatePermissionUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionRepository _permissionRepository;
    private readonly UpdatePermissionUseCase _sut;

    public UpdatePermissionUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _permissionRepository = Substitute.For<IPermissionRepository>();

        _unitOfWork.PermissionRepository
            .Returns(_permissionRepository);

        _sut = new UpdatePermissionUseCase(_unitOfWork);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string id)
    {
        // Arrange
        var dto = new UpdatePermissionDto
        {
            Name = "Updated permission"
        };

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            dto,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id is required.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _permissionRepository
            .DidNotReceive()
            .Update(Arg.Any<PermissionsModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        const string id = "invalid-uuid";

        var dto = new UpdatePermissionDto
        {
            Name = "Updated permission"
        };

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            dto,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Id must be a valid UUID.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _permissionRepository
            .DidNotReceive()
            .Update(Arg.Any<PermissionsModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;

        var dto = new UpdatePermissionDto
        {
            Name = "Updated permission"
        };

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            dto,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Permission not found.");

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        _permissionRepository
            .DidNotReceive()
            .Update(Arg.Any<PermissionsModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionIsSystem_ReturnsForbidden()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;
        var permission = CreatePermission(id, isSystem: true);

        var originalName = permission.Name;

        var dto = new UpdatePermissionDto
        {
            Name = "Modified system permission"
        };

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            dto,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.Errors.Should().Contain(
            "System permissions cannot be modified.");

        permission.Name.Should().Be(originalName);

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        _permissionRepository
            .DidNotReceive()
            .Update(Arg.Any<PermissionsModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_UpdatesPermission()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var permission = CreatePermission(id, isSystem: false);

        var dto = new UpdatePermissionDto
        {
            Name = "Updated permission",
            Description = "Updated description",
            IsActive = false
        };

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        // Act
        var result = await _sut.ExecuteAsync(
            id,
            dto,
            cancellationToken);

        // Assert: resultado
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(permission);

        // Assert: propriedades alteradas pelo Mapperly
        permission.Name.Should().Be("Updated permission");
        permission.Description.Should().Be("Updated description");
        permission.IsActive.Should().BeFalse();

        // Propriedades que não fazem parte do DTO permanecem inalteradas.
        permission.Id.Should().Be(id);
        permission.Code.Should().Be("users.read");
        permission.Module.Should().Be(
            Kairos.Api.Common.Enums.PermissionModuleEnum.Identity);
        permission.IsSystem.Should().BeFalse();

        // Assert: leitura e atualização do repositório
        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        _permissionRepository
            .Received(1)
            .Update(
                Arg.Is<PermissionsModel>(entity =>
                    ReferenceEquals(entity, permission)));

        // O Use Case não confirma a transação.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLookupThrows_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;

        var dto = new UpdatePermissionDto
        {
            Name = "Updated permission"
        };

        var exception = new InvalidOperationException(
            "Permission lookup failed.");

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromException<PermissionsModel?>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(
            id,
            dto,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission lookup failed.");

        _permissionRepository
            .DidNotReceive()
            .Update(Arg.Any<PermissionsModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUpdateThrows_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;
        var permission = CreatePermission(id, isSystem: false);

        var dto = new UpdatePermissionDto
        {
            Name = "Updated permission"
        };

        var exception = new InvalidOperationException(
            "Permission update failed.");

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        _permissionRepository
            .When(repository => repository.Update(permission))
            .Do(_ => throw exception);

        // Act
        var act = () => _sut.ExecuteAsync(
            id,
            dto,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission update failed.");

        _permissionRepository
            .Received(1)
            .Update(permission);
    }

    private static PermissionsModel CreatePermission(
        string id,
        bool isSystem)
    {
        return new PermissionsModel
        {
            Id = id,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = null,
            Code = "users.read",
            Name = "Read users",
            Description = "Original description",
            Module = Kairos.Api.Common.Enums.PermissionModuleEnum.Identity,
            IsSystem = isSystem,
            IsActive = true
        };
    }
}