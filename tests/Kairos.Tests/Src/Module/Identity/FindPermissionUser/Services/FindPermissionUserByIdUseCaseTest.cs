using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.FindPermissionUser.Services;

public sealed class FindPermissionUserByIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionUserRepository _permissionUserRepository;
    private readonly FindPermissionUserByIdUseCase _sut;

    public FindPermissionUserByIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _permissionUserRepository =
            Substitute.For<IPermissionUserRepository>();

        _unitOfWork.PermissionUserRepository
            .Returns(_permissionUserRepository);

        _sut = new FindPermissionUserByIdUseCase(_unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsNullOrWhitespace_ReturnsValidationFailure(
        string? id)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(id!, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Value.Should().BeNull();
        result.Errors.Should().Contain("Id is required.");

        await _permissionUserRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid-uuid")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure(
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

        result.Errors.Should().Contain(
            "Id must be a valid UUID.");

        await _permissionUserRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _permissionUserRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionUserModel?>(null));

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(404);
        result.Value.Should().BeNull();

        result.Errors.Should().Contain(
            "Permission user not found.");

        await _permissionUserRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionUserExists_ReturnsPermissionUser()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var permissionUser = CreatePermissionUser(id);

        _permissionUserRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromResult<PermissionUserModel?>(permissionUser));

        // Act
        var result = await _sut.ExecuteAsync(id, cancellationToken);

        // Assert: resultado
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeSameAs(permissionUser);

        // Assert: dados da associação
        result.Value!.Id.Should().Be(id);
        result.Value.UserId.Should().Be("user-test-id");
        result.Value.PermissionId.Should().Be("permission-test-id");
        result.Value.IsActive.Should().BeTrue();
        result.Value.Version.Should().Be(1);

        // Assert: consulta executada exatamente uma vez
        await _permissionUserRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        // A operação é somente de leitura.
        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsValidWithDifferentGuidFormat_UsesCanonicalId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var inputId = guid.ToString("D").ToUpperInvariant();
        var normalizedId = guid.ToString();

        var cancellationToken = CancellationToken.None;
        var permissionUser = CreatePermissionUser(normalizedId);

        _permissionUserRepository
            .GetByIdAsync(normalizedId, cancellationToken)
            .Returns(Task.FromResult<PermissionUserModel?>(permissionUser));

        // Act
        var result = await _sut.ExecuteAsync(inputId, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(permissionUser);

        await _permissionUserRepository
            .Received(1)
            .GetByIdAsync(normalizedId, cancellationToken);

        await _permissionUserRepository
            .DidNotReceive()
            .GetByIdAsync(inputId, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Permission user lookup failed.");

        _permissionUserRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromException<PermissionUserModel?>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission user lookup failed.");

        await _permissionUserRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryOperationIsCancelled_PropagatesCancellation()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cancellationToken = cts.Token;

        _permissionUserRepository
            .GetByIdAsync(id, cancellationToken)
            .Returns(Task.FromCanceled<PermissionUserModel?>(
                cancellationToken));

        // Act
        var act = () => _sut.ExecuteAsync(id, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _permissionUserRepository
            .Received(1)
            .GetByIdAsync(id, cancellationToken);
    }

    private static PermissionUserModel CreatePermissionUser(string id)
    {
        return new PermissionUserModel
        {
            Id = id,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = null,
            UserId = "user-test-id",
            PermissionId = "permission-test-id",
            IsActive = true
        };
    }
}