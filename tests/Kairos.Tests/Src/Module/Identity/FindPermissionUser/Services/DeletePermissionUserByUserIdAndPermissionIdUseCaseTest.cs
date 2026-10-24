using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Identity.FindPermissionUser.Services;

public sealed class DeletePermissionUserByUserIdAndPermissionIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IPermissionUserRepository _permissionUserRepository;
    private readonly IFindUserByIdUseCase _findUserByIdUseCase;
    private readonly DeletePermissionUserByUserIdAndPermissionIdUseCase _sut;

    public DeletePermissionUserByUserIdAndPermissionIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _permissionRepository = Substitute.For<IPermissionRepository>();
        _permissionUserRepository =
            Substitute.For<IPermissionUserRepository>();
        _findUserByIdUseCase =
            Substitute.For<IFindUserByIdUseCase>();

        _unitOfWork.PermissionRepository
            .Returns(_permissionRepository);

        _unitOfWork.PermissionUserRepository
            .Returns(_permissionUserRepository);

        _sut = new DeletePermissionUserByUserIdAndPermissionIdUseCase(
            _unitOfWork,
            _findUserByIdUseCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task ExecuteAsync_WhenUserIdIsInvalid_ReturnsValidationFailure(
        string? userId)
    {
        // Arrange
        var permissionId = Guid.NewGuid().ToString();

        // Act
        var result = await _sut.ExecuteAsync(
            userId!,
            permissionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("UserId is required.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _findUserByIdUseCase
            .DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task ExecuteAsync_WhenPermissionIdIsInvalid_ReturnsValidationFailure(
        string? permissionId)
    {
        // Act
        var result = await _sut.ExecuteAsync(
            "user-test-id",
            permissionId!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("PermissionId is required.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _findUserByIdUseCase
            .DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .GetByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task ExecuteAsync_WhenPermissionIdIsNotUuid_ReturnsValidationFailure(
        string permissionId)
    {
        // Act
        var result = await _sut.ExecuteAsync(
            "user-test-id",
            permissionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain(
            "PermissionId must be a valid UUID.");

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _findUserByIdUseCase
            .DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsAlreadyRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(
            "user-test-id",
            Guid.NewGuid().ToString(),
            cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _permissionRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _findUserByIdUseCase
            .DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var permissionId = Guid.NewGuid().ToString();

        _permissionRepository
            .GetByIdAsync(permissionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PermissionsModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(
            "user-test-id",
            permissionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("Permission not found.");

        await _findUserByIdUseCase
            .DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .GetByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionCodeIsEmpty_ReturnsFailure()
    {
        // Arrange
        var permissionId = Guid.NewGuid().ToString();
        var permission = CreatePermission(permissionId, code: "   ");

        SetupPermission(permission);

        // Act
        var result = await _sut.ExecuteAsync(
            "user-test-id",
            permissionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(500);
        result.Errors.Should().Contain(
            "The permission has an invalid code.");

        await _findUserByIdUseCase
            .DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsNotFound_PreservesNotFoundFailure()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();
        var permission = CreatePermission(permissionId);

        SetupPermission(permission);

        _findUserByIdUseCase
            .ExecuteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    Result<UserModel>.NotFound("User not found.")));

        // Act
        var result = await _sut.ExecuteAsync(userId, permissionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("User not found.");

        await _permissionUserRepository
            .DidNotReceive()
            .GetByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserLookupReturnsOtherFailure_PreservesStatusAndErrors()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        SetupPermission(CreatePermission(permissionId));

        _findUserByIdUseCase
            .ExecuteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    Result<UserModel>.Forbidden("User lookup forbidden.")));

        // Act
        var result = await _sut.ExecuteAsync(userId, permissionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);
        result.Errors.Should().Contain("User lookup forbidden.");

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserLookupSucceedsWithNullValue_ReturnsNotFound()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        SetupPermission(CreatePermission(permissionId));

        _findUserByIdUseCase
            .ExecuteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    Result<UserModel>.Success(null!)));

        // Act
        var result = await _sut.ExecuteAsync(userId, permissionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("User not found.");

        await _permissionUserRepository
            .DidNotReceive()
            .GetByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssociationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var user = CreateUser(userId, ["users.read"]);
        var permission = CreatePermission(permissionId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, null);

        // Act
        var result = await _sut.ExecuteAsync(userId, permissionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain(
            "Permission-user association not found.");

        user.Permissions.Should().ContainSingle()
            .Which.Should().Be("users.read");

        await _permissionUserRepository
            .Received(1)
            .GetByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssociationExists_RemovesPermissionAndDeletesAssociation()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var user = CreateUser(
            userId,
            ["USERS.READ", "users.write"]);

        var permission = CreatePermission(
            permissionId,
            code: "users.read");

        var association = CreateAssociation(userId, permissionId);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        SetupPermission(permission, ct);
        SetupUser(user, ct);
        SetupAssociation(userId, permissionId, association, ct);

        _permissionUserRepository
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                ct)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(userId, permissionId, ct);

        // Assert: resultado sem conteúdo
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
        result.Value.Should().BeNull();
        result.Errors.Should().BeEmpty();

        // Assert: a permissão foi removida, preservando as outras.
        user.Permissions.Should().ContainSingle()
            .Which.Should().Be("users.write");

        // Assert: consultas
        await _permissionRepository
            .Received(1)
            .GetByIdAsync(permissionId, ct);

        await _findUserByIdUseCase
            .Received(1)
            .ExecuteAsync(userId, ct);

        await _permissionUserRepository
            .Received(1)
            .GetByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                ct);

        // Assert: exclusão
        await _permissionUserRepository
            .Received(1)
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                ct);

        // A confirmação é responsabilidade da orquestração.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotContainPermissionCode_StillDeletesAssociation()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var user = CreateUser(userId, ["users.write"]);
        var permission = CreatePermission(permissionId);
        var association = CreateAssociation(userId, permissionId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, association);

        _permissionUserRepository
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(userId, permissionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
        result.Value.Should().BeNull();

        user.Permissions.Should().ContainSingle()
            .Which.Should().Be("users.write");

        await _permissionUserRepository
            .Received(1)
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdsContainWhitespace_NormalizesIds()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionGuid = Guid.NewGuid();
        var permissionId = permissionGuid.ToString();

        var user = CreateUser(userId, ["users.read"]);
        var permission = CreatePermission(permissionId);
        var association = CreateAssociation(userId, permissionId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, association);

        _permissionUserRepository
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(
            $" {userId} ",
            $" {permissionId} ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Permissions.Should().BeEmpty();

        await _findUserByIdUseCase
            .Received(1)
            .ExecuteAsync(
                userId,
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .Received(1)
            .GetByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .Received(1)
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionIdIsUppercase_NormalizesUuid()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionGuid = Guid.NewGuid();
        var permissionId = permissionGuid.ToString();

        var dtoPermissionId = permissionGuid
            .ToString()
            .ToUpperInvariant();

        var user = CreateUser(userId, ["users.read"]);
        var permission = CreatePermission(permissionId);
        var association = CreateAssociation(userId, permissionId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, association);

        _permissionUserRepository
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(userId, dtoPermissionId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(
                permissionId,
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .Received(1)
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionLookupThrows_PropagatesException()
    {
        // Arrange
        var permissionId = Guid.NewGuid().ToString();

        _permissionRepository
            .GetByIdAsync(permissionId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<PermissionsModel?>(
                    new InvalidOperationException(
                        "Permission lookup failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(
            "user-test-id",
            permissionId);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission lookup failed.");

        await _findUserByIdUseCase
            .DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserLookupThrows_PropagatesException()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        SetupPermission(CreatePermission(permissionId));

        _findUserByIdUseCase
            .ExecuteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<Result<UserModel>>(
                    new InvalidOperationException(
                        "User lookup failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(userId, permissionId);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("User lookup failed.");

        await _permissionUserRepository
            .DidNotReceive()
            .GetByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssociationLookupThrows_PropagatesException()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        SetupPermission(CreatePermission(permissionId));
        SetupUser(CreateUser(userId, ["users.read"]));

        _permissionUserRepository
            .GetByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<PermissionUserModel?>(
                    new InvalidOperationException(
                        "Association lookup failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(userId, permissionId);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Association lookup failed.");

        await _permissionUserRepository
            .DidNotReceive()
            .DeleteByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeleteThrows_PropagatesExceptionWithoutChangingUserPermissions()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var user = CreateUser(userId, ["users.read"]);
        var permission = CreatePermission(permissionId);
        var association = CreateAssociation(userId, permissionId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, association);

        _permissionUserRepository
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException(
                    new InvalidOperationException(
                        "Permission-user deletion failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(userId, permissionId);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission-user deletion failed.");

        user.Permissions.Should().ContainSingle()
            .Which.Should().Be("users.read");

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_DoesNotCommitTransaction()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        SetupPermission(CreatePermission(permissionId));
        SetupUser(CreateUser(userId, ["users.read"]));
        SetupAssociation(
            userId,
            permissionId,
            CreateAssociation(userId, permissionId));

        _permissionUserRepository
            .DeleteByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(userId, permissionId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    private void SetupPermission(
        PermissionsModel permission,
        CancellationToken ct = default)
    {
        _permissionRepository
            .GetByIdAsync(permission.Id, ct)
            .Returns(
                Task.FromResult<PermissionsModel?>(permission));
    }

    private void SetupUser(
        UserModel user,
        CancellationToken ct = default)
    {
        _findUserByIdUseCase
            .ExecuteAsync(user.Id, ct)
            .Returns(
                Task.FromResult(
                    Result<UserModel>.Success(user)));
    }

    private void SetupAssociation(
        string userId,
        string permissionId,
        PermissionUserModel? association,
        CancellationToken ct = default)
    {
        _permissionUserRepository
            .GetByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                ct)
            .Returns(
                Task.FromResult<PermissionUserModel?>(association));
    }

    private static PermissionsModel CreatePermission(
        string id,
        string code = "users.read")
    {
        return new PermissionsModel
        {
            Id = id,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = null,
            Code = code,
            Name = "Read users",
            Module = PermissionModuleEnum.Identity,
            IsSystem = false,
            Description = "Allows reading users",
            IsActive = true
        };
    }

    private static UserModel CreateUser(
        string id,
        string[]? permissions = null)
    {
        return new UserModel
        {
            Id = id,
            UserName = "test-user",
            Email = "test@example.com",
            Permissions = permissions ?? [],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static PermissionUserModel CreateAssociation(
        string userId,
        string permissionId)
    {
        return new PermissionUserModel
        {
            Id = Guid.NewGuid().ToString(),
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = null,
            UserId = userId,
            PermissionId = permissionId,
            IsActive = true
        };
    }
}