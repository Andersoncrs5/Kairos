using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.Permissions.Services;

public class DeletePermissionByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionRepository _permissionRepository;
    private readonly DeletePermissionByIdUseCase _sut;

    public DeletePermissionByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _permissionRepository = Substitute.For<IPermissionRepository>();

        _unitOfWork.PermissionRepository.Returns(_permissionRepository);

        _sut = new DeletePermissionByIdUseCase(_unitOfWork);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsEmpty_ReturnsValidationFailure(
        string id)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Id is required.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _permissionRepository
            .DidNotReceive()
            .Delete(Arg.Any<PermissionsModel>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure()
    {
        // Arrange
        const string id = "invalid-uuid";
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Id must be a valid UUID.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        _permissionRepository
            .DidNotReceive()
            .Delete(Arg.Any<PermissionsModel>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Permission not found.");

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        _permissionRepository
            .DidNotReceive()
            .Delete(Arg.Any<PermissionsModel>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionIsSystem_ReturnsForbidden()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;
        var permission = CreatePermission(id, isSystem: true);

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);

        result.Errors.Should().Contain(
            "Permission is system and cannot be deleted.");

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        _permissionRepository
            .DidNotReceive()
            .Delete(Arg.Any<PermissionsModel>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionExistsAndIsNotSystem_DeletesPermission()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var permission = CreatePermission(id, isSystem: false);

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().Be(true);

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        _permissionRepository
            .Received(1)
            .Delete(
                Arg.Is<PermissionsModel>(entity =>
                    ReferenceEquals(entity, permission)));

        // O commit fica sob responsabilidade do orquestrador.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrowsDuringLookup_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Database operation failed.");

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromException<PermissionsModel?>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Database operation failed.");

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        _permissionRepository
            .DidNotReceive()
            .Delete(Arg.Any<PermissionsModel>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrowsDuringDelete_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;
        var permission = CreatePermission(id, isSystem: false);

        var exception = new InvalidOperationException(
            "Delete operation failed.");

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        _permissionRepository
            .When(repository => repository.Delete(permission))
            .Do(_ => throw exception);

        // Act
        var act = () => _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Delete operation failed.");

        _permissionRepository
            .Received(1)
            .Delete(permission);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    private static PermissionsModel CreatePermission(
        string id,
        bool isSystem)
    {
        return new PermissionsModel
        {
            Id = id,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null,
            Code = "TEST_PERMISSION",
            Name = "Test permission",
            IsSystem = isSystem,
            IsActive = true
        };
    }
}