using FluentAssertions;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Services.Providers;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Kairos.Tests.Module.Identity.User;

public class DeleteUserByIdUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly IUserRepository _userRepository;
    private readonly DeleteUserByIdUseCase _sut;

    public DeleteUserByIdUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _userRepository = Substitute.For<IUserRepository>();

        _uow.UserRepository.Returns(_userRepository);

        _sut = new DeleteUserByIdUseCase(_uow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenIdIsInvalid_ReturnsValidationFailure(
        string? id)
    {
        var result = await _sut.ExecuteAsync(id!);

        result.IsFailure.Should().BeTrue();

        await _userRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        const string userId = "user-123";

        _userRepository
            .GetByIdAsync(userId)
            .Returns((UserModel?)null);

        var result = await _sut.ExecuteAsync(userId);

        result.IsFailure.Should().BeTrue();

        await _userRepository
            .DidNotReceive()
            .Delete(Arg.Any<UserModel>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserExistsAndDeletionSucceeds_ReturnsSuccess()
    {
        var user = CreateUser("user-123");

        _userRepository
            .GetByIdAsync(user.Id)
            .Returns(user);

        _userRepository
            .Delete(user)
            .Returns(IdentityResult.Success);

        var result = await _sut.ExecuteAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(true);

        await _userRepository
            .Received(1)
            .GetByIdAsync(user.Id);

        await _userRepository
            .Received(1)
            .Delete(user);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeletionFails_ReturnsConflict()
    {
        var user = CreateUser("user-123");

        var identityResult = IdentityResult.Failed(
            new IdentityError
            {
                Code = "DeleteFailed",
                Description = "Unable to delete user."
            });

        _userRepository
            .GetByIdAsync(user.Id)
            .Returns(user);

        _userRepository
            .Delete(user)
            .Returns(identityResult);

        var result = await _sut.ExecuteAsync(user.Id);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdContainsWhitespace_TrimsIdBeforeSearching()
    {
        const string userId = "user-123";

        var user = CreateUser(userId);

        _userRepository
            .GetByIdAsync(userId)
            .Returns(user);

        _userRepository
            .Delete(user)
            .Returns(IdentityResult.Success);

        var result = await _sut.ExecuteAsync($"  {userId}  ");

        result.IsSuccess.Should().BeTrue();

        await _userRepository
            .Received(1)
            .GetByIdAsync(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        Func<Task> act = () => _sut.ExecuteAsync(
            "user-123",
            cancellationTokenSource.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await _userRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<string>());
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