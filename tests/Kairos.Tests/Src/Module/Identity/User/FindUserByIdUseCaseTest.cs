using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Services.Providers;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Identity.User;

public sealed class FindUserByIdUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly IUserRepository _userRepository;
    private readonly FindUserByIdUseCase _sut;

    public FindUserByIdUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _userRepository = Substitute.For<IUserRepository>();

        _uow.UserRepository.Returns(_userRepository);

        _sut = new FindUserByIdUseCase(_uow);
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
            .GetByIdAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserExists_ReturnsSuccessWithUser()
    {
        // Arrange
        var user = CreateUser("user-123");

        _userRepository
            .GetByIdAsync(user.Id)
            .Returns(user);

        // Act
        var result = await _sut.ExecuteAsync(user.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(user);

        await _userRepository
            .Received(1)
            .GetByIdAsync(user.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string userId = "user-not-found";

        _userRepository
            .GetByIdAsync(userId)
            .Returns((UserModel?)null);

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsFailure.Should().BeTrue();

        await _userRepository
            .Received(1)
            .GetByIdAsync(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdContainsSurroundingWhitespace_TrimsIdBeforeQuery()
    {
        // Arrange
        const string normalizedId = "user-123";
        const string inputId = "  user-123  ";

        var user = CreateUser(normalizedId);

        _userRepository
            .GetByIdAsync(normalizedId)
            .Returns(user);

        // Act
        var result = await _sut.ExecuteAsync(inputId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(user);

        await _userRepository
            .Received(1)
            .GetByIdAsync(normalizedId);

        await _userRepository
            .DidNotReceive()
            .GetByIdAsync(inputId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdIsNotUuid_DoesNotRejectIdFormat()
    {
        // Arrange
        const string userId = "custom-user:123/abc";

        var user = CreateUser(userId);

        _userRepository
            .GetByIdAsync(userId)
            .Returns(user);

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(user);

        await _userRepository
            .Received(1)
            .GetByIdAsync(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdHasMixedCasing_PreservesOriginalCasing()
    {
        // Arrange
        const string userId = "User-AbC-123";

        var user = CreateUser(userId);

        _userRepository
            .GetByIdAsync(userId)
            .Returns(user);

        // Act
        var result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(user);

        await _userRepository
            .Received(1)
            .GetByIdAsync(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsAlreadyRequested_ThrowsOperationCanceledException()
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
            .GetByIdAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        const string userId = "user-123";
        const string errorMessage = "Database unavailable.";

        _userRepository
            .GetByIdAsync(userId)
            .Returns(
                Task.FromException<UserModel?>(
                    new InvalidOperationException(errorMessage)));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(userId);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(errorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrowsOperationCanceledException_PropagatesCancellation()
    {
        // Arrange
        const string userId = "user-123";

        _userRepository
            .GetByIdAsync(userId)
            .Returns(
                Task.FromException<UserModel?>(
                    new OperationCanceledException()));

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(userId);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserExists_DoesNotCommitChanges()
    {
        // Arrange
        var user = CreateUser("user-123");

        _userRepository
            .GetByIdAsync(user.Id)
            .Returns(user);

        // Act
        var result = await _sut.ExecuteAsync(user.Id);

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

    private static UserModel CreateUser(string id)
    {
        return new UserModel
        {
            Id = id,
            UserName = "test-user",
            Email = "test@example.com",
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}