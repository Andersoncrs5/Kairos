using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.FindPermissionUser.Services;

public sealed class ExistsPermissionUserByUserIdAndPermissionIdUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionUserRepository _repository;
    private readonly ExistsPermissionUserByUserIdAndPermissionIdUseCase _sut;

    public ExistsPermissionUserByUserIdAndPermissionIdUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _repository = Substitute.For<IPermissionUserRepository>();

        _unitOfWork.PermissionUserRepository.Returns(_repository);

        _sut = new ExistsPermissionUserByUserIdAndPermissionIdUseCase(
            _unitOfWork);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenUserIdIsInvalid_ReturnsValidationFailure(
        string? userId)
    {
        // Arrange
        var permissionId = Guid.NewGuid().ToString();

        // Act
        var result = await _sut.ExecuteAsync(
            userId!,
            permissionId,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("UserId is required.");

        await _repository
            .DidNotReceive()
            .HasActivePermissionAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
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
    public async Task ExecuteAsync_WhenPermissionIdIsInvalid_ReturnsValidationFailure(
        string? permissionId)
    {
        // Arrange
        const string userId = "user-test-id";

        // Act
        var result = await _sut.ExecuteAsync(
            userId,
            permissionId!,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().Contain("PermissionId is required.");

        await _repository
            .DidNotReceive()
            .HasActivePermissionAsync(
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
        // Arrange
        const string userId = "user-test-id";

        // Act
        var result = await _sut.ExecuteAsync(
            userId,
            permissionId,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "PermissionId must be a valid UUID.");

        await _repository
            .DidNotReceive()
            .HasActivePermissionAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserHasActivePermission_ReturnsTrue()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _repository
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            userId,
            permissionId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();
        result.Errors.Should().BeEmpty();

        await _repository
            .Received(1)
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotHaveActivePermission_ReturnsFalse()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;

        _repository
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(
            userId,
            permissionId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeFalse();
        result.Errors.Should().BeEmpty();

        await _repository
            .Received(1)
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdsContainWhitespace_NormalizesIds()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionGuid = Guid.NewGuid();
        var permissionId = permissionGuid.ToString();

        var inputUserId = $"  {userId}  ";
        var inputPermissionId = $"  {permissionGuid:B}  ";
        var cancellationToken = CancellationToken.None;

        _repository
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            inputUserId,
            inputPermissionId,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _repository
            .Received(1)
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken);

        await _repository
            .DidNotReceive()
            .HasActivePermissionAsync(
                inputUserId,
                inputPermissionId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();
        var cancellationToken = CancellationToken.None;

        var exception = new InvalidOperationException(
            "Active permission lookup failed.");

        _repository
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken)
            .Returns(Task.FromException<bool>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(
            userId,
            permissionId,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Active permission lookup failed.");

        await _repository
            .Received(1)
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryOperationIsCancelled_PropagatesCancellation()
    {
        // Arrange
        const string userId = "user-test-id";
        var permissionId = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cancellationToken = cts.Token;

        _repository
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken)
            .Returns(Task.FromCanceled<bool>(cancellationToken));

        // Act
        var act = () => _sut.ExecuteAsync(
            userId,
            permissionId,
            cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _repository
            .Received(1)
            .HasActivePermissionAsync(
                userId,
                permissionId,
                cancellationToken);
    }
}