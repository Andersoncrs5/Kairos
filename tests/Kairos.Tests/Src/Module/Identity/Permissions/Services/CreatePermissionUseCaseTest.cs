using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.Permissions.Services;

public sealed class CreatePermissionUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionRepository _permissionRepository;
    private readonly CreatePermissionUseCase _sut;

    public CreatePermissionUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _permissionRepository = Substitute.For<IPermissionRepository>();

        _unitOfWork.PermissionRepository
            .Returns(_permissionRepository);

        _sut = new CreatePermissionUseCase(_unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDtoIsValid_ReturnsCreatedPermission()
    {
        // Arrange
        var dto = new CreatePermissionDto
        {
            Code = " users.read ",
            Name = "Read users",
            Description = "Allows reading users"
        };

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var beforeExecution = DateTimeOffset.UtcNow;

        _permissionRepository
            .ExistsByCodeAsync("users.read", cancellationToken)
            .Returns(Task.FromResult(false));

        _permissionRepository
            .AddAsync(
                Arg.Any<PermissionsModel>(),
                cancellationToken)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(dto, cancellationToken);

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert: resultado
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value.Should().NotBeNull();

        var permission = result.Value!;

        // Assert: identificador
        permission.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(permission.Id, out _).Should().BeTrue();

        // Assert: propriedades
        permission.Code.Should().Be("users.read");
        permission.Name.Should().Be(dto.Name);
        permission.Description.Should().Be(dto.Description);
        permission.Module.Should().Be(dto.Module);
        permission.IsSystem.Should().BeFalse();
        permission.IsActive.Should().BeTrue();

        // Auditoria/versionamento são responsabilidade do AppDbContext.
        permission.CreatedAt.Should().BeOnOrAfter(beforeExecution);
        permission.CreatedAt.Should().BeOnOrBefore(afterExecution);
        permission.UpdatedAt.Should().BeNull();

        // Assert: consulta de duplicidade exatamente uma vez.
        await _permissionRepository
            .Received(1)
            .ExistsByCodeAsync("users.read", cancellationToken);

        // Assert: entidade adicionada exatamente uma vez.
        await _permissionRepository
            .Received(1)
            .AddAsync(
                Arg.Is<PermissionsModel>(p =>
                    p.Id == permission.Id &&
                    p.Code == "users.read" &&
                    p.Name == "Read users" &&
                    p.Description == "Allows reading users" &&
                    p.Module == dto.Module &&
                    !p.IsSystem &&
                    p.IsActive),
                cancellationToken);

        // Assert: não consulta outros códigos.
        await _permissionRepository
            .DidNotReceive()
            .ExistsByCodeAsync(
                Arg.Is<string>(code => code != "users.read"),
                Arg.Any<CancellationToken>());

        // O commit será realizado pelo serviço orquestrador.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenCodeIsInvalid_ReturnsValidationFailure(
        string? code)
    {
        // Arrange
        var dto = new CreatePermissionDto
        {
            Code = code!,
            Name = "Read users"
        };

        // Act
        var result = await _sut.ExecuteAsync(
            dto,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Value.Should().BeNull();

        result.Errors.Should().Contain(
            "Permission code is required.");

        await _permissionRepository
            .DidNotReceive()
            .ExistsByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PermissionsModel>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenNameIsInvalid_ReturnsValidationFailure(
        string? name)
    {
        // Arrange
        var dto = new CreatePermissionDto
        {
            Code = "users.read",
            Name = name!
        };

        // Act
        var result = await _sut.ExecuteAsync(
            dto,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Value.Should().BeNull();

        result.Errors.Should().Contain(
            "Permission name is required.");

        // A validação ocorre antes do acesso ao repositório.
        await _permissionRepository
            .DidNotReceive()
            .ExistsByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _permissionRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PermissionsModel>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var dto = new CreatePermissionDto
        {
            Code = " users.read ",
            Name = "Read users"
        };

        var cancellationToken = CancellationToken.None;

        _permissionRepository
            .ExistsByCodeAsync("users.read", cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(dto, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(409);
        result.Value.Should().BeNull();

        result.Errors.Should().Contain(
            "Permission with code 'users.read' already exists.");

        await _permissionRepository
            .Received(1)
            .ExistsByCodeAsync("users.read", cancellationToken);

        await _permissionRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PermissionsModel>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExistenceCheckFails_PropagatesException()
    {
        // Arrange
        var dto = new CreatePermissionDto
        {
            Code = "users.read",
            Name = "Read users"
        };

        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Permission lookup failed.");

        _permissionRepository
            .ExistsByCodeAsync(dto.Code, cancellationToken)
            .Returns(Task.FromException<bool>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(dto, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission lookup failed.");

        await _permissionRepository
            .Received(1)
            .ExistsByCodeAsync(dto.Code, cancellationToken);

        await _permissionRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<PermissionsModel>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAddFails_PropagatesException()
    {
        // Arrange
        var dto = new CreatePermissionDto
        {
            Code = "users.read",
            Name = "Read users"
        };

        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Permission persistence failed.");

        _permissionRepository
            .ExistsByCodeAsync(dto.Code, cancellationToken)
            .Returns(Task.FromResult(false));

        _permissionRepository
            .AddAsync(
                Arg.Any<PermissionsModel>(),
                cancellationToken)
            .Returns(Task.FromException(exception));

        // Act
        var act = () => _sut.ExecuteAsync(dto, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission persistence failed.");

        await _permissionRepository
            .Received(1)
            .ExistsByCodeAsync(dto.Code, cancellationToken);

        await _permissionRepository
            .Received(1)
            .AddAsync(
                Arg.Is<PermissionsModel>(p =>
                    p.Code == dto.Code &&
                    p.Name == dto.Name &&
                    !p.IsSystem &&
                    !string.IsNullOrWhiteSpace(p.Id)),
                cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }
}