using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Commands;
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

        _unitOfWork.PermissionRepository.Returns(_permissionRepository);

        _sut = new UpdatePermissionUseCase(_unitOfWork);
    }

    // ---------------------------------------------------------------
    // Validação do comando
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string id)
    {
        var command = NewCommand(id);

        var result = await _sut.ExecuteAsync(
            command,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Permission ID is required.");

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
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        var command = new UpdatePermissionCommand(
            Guid.NewGuid().ToString(),
            null!);

        var result = await _sut.ExecuteAsync(
            command,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "Permission update data is required.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure()
    {
        const string id = "invalid-uuid";
        var command = NewCommand(id);

        var result = await _sut.ExecuteAsync(
            command,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Id must be a valid UUID.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _permissionRepository
            .DidNotReceive()
            .Update(Arg.Any<PermissionsModel>());
    }

    // ---------------------------------------------------------------
    // Regras de negócio
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenPermissionDoesNotExist_ReturnsNotFound()
    {
        var id = Guid.NewGuid().ToString();
        var ct = CancellationToken.None;
        var command = NewCommand(id);

        _permissionRepository
            .GetByIdAsync(id, ct)
            .Returns(Task.FromResult<PermissionsModel?>(null));

        var result = await _sut.ExecuteAsync(command, ct);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Permission not found.");

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, ct);

        _permissionRepository
            .DidNotReceive()
            .Update(Arg.Any<PermissionsModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionIsSystem_ReturnsForbidden()
    {
        var id = Guid.NewGuid().ToString();
        var ct = CancellationToken.None;
        var permission = CreatePermission(id, isSystem: true);
        var originalName = permission.Name;
        var command = NewCommand(
            id,
            new UpdatePermissionDto
            {
                Name = "Modified system permission"
            });

        _permissionRepository
            .GetByIdAsync(id, ct)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        var result = await _sut.ExecuteAsync(command, ct);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);
        result.Errors.Should().Contain(
            "System permissions cannot be modified.");

        permission.Name.Should().Be(originalName);

        _permissionRepository
            .DidNotReceive()
            .Update(Arg.Any<PermissionsModel>());
    }

    // ---------------------------------------------------------------
    // Atualização
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsValid_UpdatesPermission()
    {
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var permission = CreatePermission(id, isSystem: false);

        var command = NewCommand(
            id,
            new UpdatePermissionDto
            {
                Name = "Updated permission",
                Description = "Updated description",
                IsActive = false
            });

        _permissionRepository
            .GetByIdAsync(id, ct)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        var result = await _sut.ExecuteAsync(command, ct);

        // Resultado
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(permission);

        // Propriedades atualizadas pelo Mapperly
        permission.Name.Should().Be("Updated permission");
        permission.Description.Should().Be("Updated description");
        permission.IsActive.Should().BeFalse();

        // Propriedades que não fazem parte do DTO permanecem inalteradas.
        permission.Id.Should().Be(id);
        permission.Code.Should().Be("users.read");
        permission.Module.Should().Be(PermissionModuleEnum.Identity);
        permission.IsSystem.Should().BeFalse();

        // O repositório recebe a entidade atualizada.
        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, ct);

        _permissionRepository
            .Received(1)
            .Update(Arg.Is<PermissionsModel>(
                entity => ReferenceEquals(entity, permission)));

        // A transação é controlada pelo chamador, não pelo Use Case.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Exceções
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenLookupThrows_PropagatesException()
    {
        var id = Guid.NewGuid().ToString();
        var ct = CancellationToken.None;
        var command = NewCommand(id);

        var exception = new InvalidOperationException(
            "Permission lookup failed.");

        _permissionRepository
            .GetByIdAsync(id, ct)
            .Returns(Task.FromException<PermissionsModel?>(exception));

        Func<Task> act = async () =>
            await _sut.ExecuteAsync(command, ct);

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
        var id = Guid.NewGuid().ToString();
        var ct = CancellationToken.None;
        var permission = CreatePermission(id, isSystem: false);
        var command = NewCommand(id);

        var exception = new InvalidOperationException(
            "Permission update failed.");

        _permissionRepository
            .GetByIdAsync(id, ct)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        _permissionRepository
            .When(repository => repository.Update(permission))
            .Do(_ => throw exception);

        Func<Task> act = async () =>
            await _sut.ExecuteAsync(command, ct);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission update failed.");

        _permissionRepository
            .Received(1)
            .Update(permission);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceled()
    {
        var id = Guid.NewGuid().ToString();
        var command = NewCommand(id);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = async () =>
            await _sut.ExecuteAsync(command, cts.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _permissionRepository
            .DidNotReceive()
            .Update(Arg.Any<PermissionsModel>());
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static UpdatePermissionCommand NewCommand(
        string? id = null,
        UpdatePermissionDto? dto = null)
    {
        return new UpdatePermissionCommand(
            id ?? Guid.NewGuid().ToString(),
            dto ?? new UpdatePermissionDto
            {
                Name = "Updated permission"
            });
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
            Module = PermissionModuleEnum.Identity,
            IsSystem = isSystem,
            IsActive = true
        };
    }
}