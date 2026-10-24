using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Services.Providers;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.User;

public sealed class ExistsUserByIdUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly IUserRepository _userRepository;
    private readonly ExistsUserByIdUseCase _sut;

    public ExistsUserByIdUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _userRepository = Substitute.For<IUserRepository>();

        _uow.UserRepository.Returns(_userRepository);

        _sut = new ExistsUserByIdUseCase(_uow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task ExecuteAsync_WhenIdIsNullOrWhitespace_ReturnsValidationFailure(
        string? id)
    {
        // Act
        var result = await _sut.ExecuteAsync(id!);

        // Assert
        result.IsFailure.Should().BeTrue();

        await _userRepository
            .DidNotReceive()
            .ExistsByIdAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserExists_ReturnsSuccessTrue()
    {
        // Arrange
        const string userId = "user-123";

        _userRepository
            .ExistsByIdAsync(userId)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _userRepository
            .Received(1)
            .ExistsByIdAsync(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ReturnsSuccessFalse()
    {
        // Arrange
        const string userId = "user-123";

        _userRepository
            .ExistsByIdAsync(userId)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();

        await _userRepository
            .Received(1)
            .ExistsByIdAsync(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdContainsSurroundingWhitespace_TrimsIdBeforeQuery()
    {
        // Arrange
        const string userId = "user-123";
        const string inputId = "  user-123  ";

        _userRepository
            .ExistsByIdAsync(userId)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(inputId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _userRepository
            .Received(1)
            .ExistsByIdAsync(userId);

        await _userRepository
            .DidNotReceive()
            .ExistsByIdAsync(inputId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsNotUuid_DoesNotRejectIdFormat()
    {
        // Arrange
        const string userId = "custom-user:123/abc";

        _userRepository
            .ExistsByIdAsync(userId)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        await _userRepository
            .Received(1)
            .ExistsByIdAsync(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdHasDifferentCasing_PreservesOriginalCasing()
    {
        // Arrange
        const string userId = "User-AbC-123";

        _userRepository
            .ExistsByIdAsync(userId)
            .Returns(Task.FromResult(false));

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();

        await _userRepository
            .Received(1)
            .ExistsByIdAsync(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(
            "user-123",
            cancellationTokenSource.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _userRepository
            .DidNotReceive()
            .ExistsByIdAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        const string userId = "user-123";
        const string errorMessage = "Database unavailable.";

        _userRepository
            .ExistsByIdAsync(userId)
            .Returns(
                Task.FromException<bool>(
                    new InvalidOperationException(errorMessage)));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(userId);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(errorMessage);

        await _userRepository
            .Received(1)
            .ExistsByIdAsync(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrowsOperationCanceledException_PropagatesCancellation()
    {
        // Arrange
        const string userId = "user-123";

        _userRepository
            .ExistsByIdAsync(userId)
            .Returns(
                Task.FromException<bool>(
                    new OperationCanceledException()));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(userId);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenQuerySucceeds_DoesNotCommitChanges()
    {
        // Arrange
        const string userId = "user-123";

        _userRepository
            .ExistsByIdAsync(userId)
            .Returns(Task.FromResult(true));

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _uow
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsInvalid_DoesNotCommitChanges()
    {
        // Act
        var result = await _sut.ExecuteAsync(" ");

        // Assert
        result.IsFailure.Should().BeTrue();

        await _uow
            .DidNotReceive()
            .CommitAsync(Arg.Any<CancellationToken>());
    }
}