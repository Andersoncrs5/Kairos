using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.Permissions.Services;

public sealed class FindPermissionByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionRepository _permissionRepository;
    private readonly FindPermissionByIdUseCase _sut;

    public FindPermissionByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _permissionRepository = Substitute.For<IPermissionRepository>();

        _unitOfWork.PermissionRepository
            .Returns(_permissionRepository);

        _sut = new FindPermissionByIdUseCase(_unitOfWork);
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
        result.StatusCode.Should().Be(400);
        result.Value.Should().BeNull();

        result.Errors.Should().Contain("Id is required.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

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
        result.StatusCode.Should().Be(400);
        result.Value.Should().BeNull();

        result.Errors.Should().Contain(
            "Id must be a valid UUID.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Value.Should().BeNull();

        result.Errors.Should().Contain("Permission not found.");

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionExists_ReturnsPermission()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var permission = CreatePermission(id);

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert: resultado
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeSameAs(permission);

        // Assert: propriedades da permissão
        result.Value!.Id.Should().Be(id);
        result.Value.Code.Should().Be("users.read");
        result.Value.Name.Should().Be("Read users");
        result.Value.Description.Should().Be("Allows reading users");
        result.Value.Module.Should().Be(PermissionModuleEnum.Identity);
        result.Value.IsSystem.Should().BeFalse();
        result.Value.IsActive.Should().BeTrue();

        // Assert: consulta realizada exatamente uma vez
        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        // O Use Case é somente de leitura.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Permission lookup failed.");

        _permissionRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromException<PermissionsModel?>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission lookup failed.");

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    private static PermissionsModel CreatePermission(string id)
    {
        return new PermissionsModel
        {
            Id = id,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = null,
            Code = "users.read",
            Name = "Read users",
            Module = PermissionModuleEnum.Identity,
            IsSystem = false,
            Description = "Allows reading users",
            IsActive = true
        };
    }
}