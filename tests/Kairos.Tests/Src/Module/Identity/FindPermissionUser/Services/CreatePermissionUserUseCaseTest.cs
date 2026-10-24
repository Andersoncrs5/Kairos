using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Identity.FindPermissionUser.Services;

public sealed class CreatePermissionUserUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IPermissionUserRepository _permissionUserRepository;
    private readonly IFindUserByIdUseCase _findUserByIdUseCase;
    private readonly CreatePermissionUserUseCase _sut;

    public CreatePermissionUserUseCaseTest()
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

        _sut = new CreatePermissionUserUseCase(
            _unitOfWork,
            _findUserByIdUseCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsNull_ReturnsValidationFailure()
    {
        // Act
        var result = await _sut.ExecuteAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("Request body is required.");

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
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
        var dto = CreateDto(
            userId!,
            Guid.NewGuid().ToString());

        // Act
        var result = await _sut.ExecuteAsync(dto);

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
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
        // Arrange
        var dto = CreateDto("user-test-id", permissionId!);

        // Act
        var result = await _sut.ExecuteAsync(dto);

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task ExecuteAsync_WhenPermissionIdIsNotUuid_ReturnsValidationFailure(
        string permissionId)
    {
        // Arrange
        var dto = CreateDto("user-test-id", permissionId);

        // Act
        var result = await _sut.ExecuteAsync(dto);

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var permissionId = Guid.NewGuid().ToString();
        var dto = CreateDto("user-test-id", permissionId);

        _permissionRepository
            .GetByIdAsync(permissionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PermissionsModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(dto);

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionIsInactive_ReturnsConflict()
    {
        // Arrange
        var permissionId = Guid.NewGuid().ToString();
        var dto = CreateDto("user-test-id", permissionId);
        var permission = CreatePermission(
            permissionId,
            isActive: false);

        _permissionRepository
            .GetByIdAsync(permissionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "Inactive permissions cannot be assigned to users.");

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionCodeIsEmpty_ReturnsFailure()
    {
        // Arrange
        var permissionId = Guid.NewGuid().ToString();
        var dto = CreateDto("user-test-id", permissionId);
        var permission = CreatePermission(
            permissionId,
            code: "   ");

        _permissionRepository
            .GetByIdAsync(permissionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        // Act
        var result = await _sut.ExecuteAsync(dto);

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserLookupReturnsNotFound_PreservesFailure()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId);

        _permissionRepository
            .GetByIdAsync(permissionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        _findUserByIdUseCase
            .ExecuteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    Result<UserModel>.NotFound("User not found.")));

        // Act
        var result = await _sut.ExecuteAsync(dto);

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserLookupReturnsOtherFailure_PreservesStatusAndErrors()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId);

        _permissionRepository
            .GetByIdAsync(permissionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        _findUserByIdUseCase
            .ExecuteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    Result<UserModel>.Forbidden("User lookup forbidden.")));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(403);
        result.Errors.Should().Contain("User lookup forbidden.");

        await _permissionUserRepository
            .DidNotReceive()
            .GetByUserIdAndPermissionIdAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserLookupSucceedsWithNullValue_ReturnsNotFound()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId);

        _permissionRepository
            .GetByIdAsync(permissionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PermissionsModel?>(permission));

        _findUserByIdUseCase
            .ExecuteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    Result<UserModel>.Success(null!)));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Errors.Should().Contain("User not found.");

        await _permissionUserRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssociationAlreadyExists_ReturnsConflict()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId);
        var user = CreateUser(userId);
        var association = CreateAssociation(userId, permissionId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(
            userId,
            permissionId,
            association);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Errors.Should().Contain(
            "This permission is already assigned to the user.");

        user.Permissions.Should().BeEmpty();

        await _permissionUserRepository
            .Received(1)
            .GetByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                Arg.Any<CancellationToken>());

        await _permissionUserRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_CreatesAssociationAndAddsPermissionToUser()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var dto = CreateDto(
            $"  {userId}  ",
            $"  {permissionId}  ");

        var permission = CreatePermission(
            permissionId,
            code: "users.read");

        var user = CreateUser(userId);

        SetupPermission(permission, ct);
        SetupUser(user, ct);
        SetupAssociation(userId, permissionId, null, ct);

        _permissionUserRepository
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                ct)
            .Returns(Task.CompletedTask);

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.ExecuteAsync(dto, ct);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert: resultado
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value.Should().NotBeNull();

        var association = result.Value!;

        // Assert: associação
        association.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(association.Id, out _).Should().BeTrue();
        association.UserId.Should().Be(userId);
        association.PermissionId.Should().Be(permissionId);
        association.IsActive.Should().BeTrue();

        association.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        association.CreatedAt.Should().BeOnOrBefore(afterExecution);
        association.UpdatedAt.Should().BeNull();

        // Assert: permissões do usuário
        user.Permissions.Should().ContainSingle()
            .Which.Should().Be("users.read");

        // Assert: consulta da permissão
        await _permissionRepository
            .Received(1)
            .GetByIdAsync(permissionId, ct);

        // Assert: busca do usuário com ID normalizado
        await _findUserByIdUseCase
            .Received(1)
            .ExecuteAsync(userId, ct);

        // Assert: verificação de duplicidade
        await _permissionUserRepository
            .Received(1)
            .GetByUserIdAndPermissionIdAsync(
                userId,
                permissionId,
                ct);

        // Assert: associação registrada uma única vez
        await _permissionUserRepository
            .Received(1)
            .AddAsync(
                Arg.Is<PermissionUserModel>(entity =>
                    entity.Id == association.Id &&
                    entity.UserId == userId &&
                    entity.PermissionId == permissionId &&
                    entity.IsActive),
                ct);

        // O Use Case não confirma a transação.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserAlreadyHasPermission_DoesNotDuplicatePermissionCode()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId, "users.read");
        var user = CreateUser(userId, ["users.read"]);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, null);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Permissions.Should().ContainSingle()
            .Which.Should().Be("users.read");

        await _permissionUserRepository
            .Received(1)
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdContainsWhitespace_SearchesUsingTrimmedId()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto($"  {userId}  ", permissionId);
        var permission = CreatePermission(permissionId);
        var user = CreateUser(userId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, null);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();

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
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionIdIsUppercase_NormalizesUuid()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionGuid = Guid.NewGuid();

        var permissionId = permissionGuid.ToString();
        var uppercasePermissionId = permissionGuid.ToString().ToUpperInvariant();

        var dto = CreateDto(userId, uppercasePermissionId);
        var permission = CreatePermission(permissionId);
        var user = CreateUser(userId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, null);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.PermissionId.Should().Be(permissionId);

        await _permissionRepository
            .Received(1)
            .GetByIdAsync(
                permissionId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionLookupThrows_PropagatesException()
    {
        // Arrange
        var permissionId = Guid.NewGuid().ToString();
        var dto = CreateDto("user-test-id", permissionId);

        _permissionRepository
            .GetByIdAsync(permissionId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<PermissionsModel?>(
                    new InvalidOperationException(
                        "Permission lookup failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(dto);

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserLookupThrows_PropagatesException()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId);

        SetupPermission(permission);

        _findUserByIdUseCase
            .ExecuteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<Result<UserModel>>(
                    new InvalidOperationException(
                        "User lookup failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(dto);

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssociationLookupThrows_PropagatesException()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId);
        var user = CreateUser(userId);

        SetupPermission(permission);
        SetupUser(user);

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
        Func<Task> act = () => _sut.ExecuteAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Association lookup failed.");

        user.Permissions.Should().BeEmpty();

        await _permissionUserRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAddAssociationThrows_PropagatesException()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId);
        var user = CreateUser(userId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, null);

        _permissionUserRepository
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException(
                    new InvalidOperationException(
                        "Association creation failed.")));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(dto);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Association creation failed.");

        // O código só é adicionado depois que AddAsync termina.
        user.Permissions.Should().BeEmpty();

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsAlreadyRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var dto = CreateDto(
            "user-test-id",
            Guid.NewGuid().ToString());

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(dto, cts.Token);

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
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidTokenIsProvided_ForwardsTokenToDependencies()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId);
        var user = CreateUser(userId);

        SetupPermission(permission, ct);
        SetupUser(user, ct);
        SetupAssociation(userId, permissionId, null, ct);

        _permissionUserRepository
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                ct)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(dto, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();

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

        await _permissionUserRepository
            .Received(1)
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                ct);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_DoesNotCommitTransaction()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        var dto = CreateDto(userId, permissionId);
        var permission = CreatePermission(permissionId);
        var user = CreateUser(userId);

        SetupPermission(permission);
        SetupUser(user);
        SetupAssociation(userId, permissionId, null);

        _permissionUserRepository
            .AddAsync(
                Arg.Any<PermissionUserModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    private static CreatePermissionUserDTO CreateDto(
        string userId,
        string permissionId)
    {
        return new CreatePermissionUserDTO
        {
            UserId = userId,
            PermissionId = permissionId
        };
    }

    private static PermissionsModel CreatePermission(
        string id,
        string code = "users.read",
        bool isActive = true)
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
            IsActive = isActive
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
}