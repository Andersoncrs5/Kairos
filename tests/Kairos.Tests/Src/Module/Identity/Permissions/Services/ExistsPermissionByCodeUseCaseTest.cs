using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Services.Provider;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.Permissions.Services;

public sealed class ExistsPermissionByCodeUseCaseTest
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionRepository _permissionRepository;
    private readonly ExistsPermissionByCodeUseCase _sut;

    public ExistsPermissionByCodeUseCaseTest()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _permissionRepository = Substitute.For<IPermissionRepository>();

        _unitOfWork.PermissionRepository
            .Returns(_permissionRepository);

        _sut = new ExistsPermissionByCodeUseCase(_unitOfWork);
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
        result.Errors.Should().Contain("Permission code is required.");

        await _permissionRepository
            .DidNotReceive()
            .ExistsByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var code = new string('a', 101);
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(code, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        result.Errors.Should().Contain(
            "Permission code cannot exceed 100 characters.");

        await _permissionRepository
            .DidNotReceive()
            .ExistsByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeExists_ReturnsTrue()
    {
        // Arrange
        const string code = "users.read";

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _permissionRepository
            .ExistsByCodeAsync(code, cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(code, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeTrue();
        result.Errors.Should().BeEmpty();

        await _permissionRepository
            .Received(1)
            .ExistsByCodeAsync(code, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeDoesNotExist_ReturnsFalse()
    {
        // Arrange
        const string code = "users.unknown";

        var cancellationToken = CancellationToken.None;

        _permissionRepository
            .ExistsByCodeAsync(code, cancellationToken)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(code, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Value.Should().BeFalse();
        result.Errors.Should().BeEmpty();

        await _permissionRepository
            .Received(1)
            .ExistsByCodeAsync(code, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeHasSurroundingWhitespace_TrimsBeforeQuery()
    {
        // Arrange
        const string inputCode = "  users.read  ";
        const string normalizedCode = "users.read";

        var cancellationToken = CancellationToken.None;

        _permissionRepository
            .ExistsByCodeAsync(normalizedCode, cancellationToken)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(
            inputCode,
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _permissionRepository
            .Received(1)
            .ExistsByCodeAsync(normalizedCode, cancellationToken);

        await _permissionRepository
            .DidNotReceive()
            .ExistsByCodeAsync(inputCode, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTrimmedCodeExceedsMaximumLength_ReturnsValidationFailure()
    {
        // Arrange
        var code = $" {new string('a', 101)} ";
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _sut.ExecuteAsync(code, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(400);

        await _permissionRepository
            .DidNotReceive()
            .ExistsByCodeAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
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
            .ExistsByCodeAsync(code, cancellationToken)
            .Returns(Task.FromException<bool>(exception));

        // Act
        var act = () => _sut.ExecuteAsync(code, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Permission lookup failed.");

        await _permissionRepository
            .Received(1)
            .ExistsByCodeAsync(code, cancellationToken);

        await _unitOfWork
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryIsCancelled_PropagatesCancellation()
    {
        // Arrange
        const string code = "users.read";

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cancellationToken = cts.Token;

        _permissionRepository
            .ExistsByCodeAsync(code, cancellationToken)
            .Returns(Task.FromCanceled<bool>(cancellationToken));

        // Act
        var act = () => _sut.ExecuteAsync(code, cancellationToken);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _permissionRepository
            .Received(1)
            .ExistsByCodeAsync(code, cancellationToken);
    }
}