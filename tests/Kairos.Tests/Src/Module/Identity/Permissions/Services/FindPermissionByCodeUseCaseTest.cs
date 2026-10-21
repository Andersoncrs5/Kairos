using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.Permissions.Services;

public sealed class FindPermissionByCodeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionRepository _permissionRepository;
    private readonly FindPermissionByCodeUseCase _sut;

    public FindPermissionByCodeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _permissionRepository = Substitute.For<IPermissionRepository>();

        _unitOfWork.PermissionRepository
            .Returns(_permissionRepository);

        _sut = new FindPermissionByCodeUseCase(_unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenCodeIsNullOrWhitespace_ReturnsValidationFailure(
        string? code)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(
            code!,
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Value.Should().BeNull();

        result.Errors.Should().Contain(
            "Permission code is required.");

        await _permissionRepository
            .DidNotReceive()
            .GetByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string code = "users.read";

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _permissionRepository
            .GetByCodeAsync(code, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(code, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Value.Should().BeNull();

        result.Errors.Should().Contain("Permission not found.");

        await _permissionRepository
            .Received(1)
            .GetByCodeAsync(code, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeHasWhitespace_TrimsCodeBeforeSearching()
    {
        // Arrange
        const string code = "users.read";
        const string inputCode = "  users.read  ";

        var cancellationToken = CancellationToken.None;
        var permission = CreatePermission(code);

        _permissionRepository
            .GetByCodeAsync(code, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        // Act
        var result = await _sut.ExecuteAsync(
            inputCode,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeSameAs(permission);

        await _permissionRepository
            .Received(1)
            .GetByCodeAsync(code, cancellationToken);

        await _permissionRepository
            .DidNotReceive()
            .GetByCodeAsync(
                inputCode,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionExists_ReturnsPermission()
    {
        // Arrange
        const string code = "users.read";

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var permission = CreatePermission(code);

        _permissionRepository
            .GetByCodeAsync(code, cancellationToken)
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        // Act
        var result = await _sut.ExecuteAsync(code, cancellationToken);

        // Assert: resultado
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeSameAs(permission);

        // Assert: conteúdo retornado
        result.Value!.Id.Should().Be(permission.Id);
        result.Value.Code.Should().Be("users.read");
        result.Value.Name.Should().Be("Read users");
        result.Value.Description.Should().Be("Allows reading users");
        result.Value.Module.Should().Be(PermissionModuleEnum.Identity);
        result.Value.IsSystem.Should().BeFalse();
        result.Value.IsActive.Should().BeTrue();

        // Assert: consulta realizada exatamente uma vez
        await _permissionRepository
            .Received(1)
            .GetByCodeAsync(code, cancellationToken);

        // O caso de uso é somente de leitura.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        const string code = "users.read";

        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Permission lookup failed.");

        _permissionRepository
            .GetByCodeAsync(code, cancellationToken)
            .Returns(Task.FromException<PermissionsModel?>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(code, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission lookup failed.");

        await _permissionRepository
            .Received(1)
            .GetByCodeAsync(code, cancellationToken);
    }

    private static PermissionsModel CreatePermission(string code)
    {
        return new PermissionsModel
        {
            Id = Guid.NewGuid().ToString(),
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null,
            Code = code,
            Name = "Read users",
            Module = PermissionModuleEnum.Identity,
            IsSystem = false,
            Description = "Allows reading users",
            IsActive = true
        };
    }
}