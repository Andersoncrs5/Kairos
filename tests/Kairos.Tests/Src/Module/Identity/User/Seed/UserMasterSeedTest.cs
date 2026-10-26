using FluentAssertions;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Seed;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Kairos.Tests.Module.Identity.User.Seed;

public sealed class UserMasterSeedTest
{
    private const string UserName = "master";
    private const string Email = "master@example.com";
    private const string FullName = "Master Admin";
    private const string Password = "MasterPassword123!";

    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ILogger<UserMasterSeed>> _loggerMock = new();

    private readonly UserMasterSeed _sut;

    public UserMasterSeedTest()
    {
        _uowMock
            .Setup(uow => uow.UserRepository)
            .Returns(_userRepositoryMock.Object);

        _userRepositoryMock
            .Setup(repository => repository.GetByUsername(UserName))
            .ReturnsAsync((UserModel?)null);

        _userRepositoryMock
            .Setup(repository => repository.GetByEmail(Email))
            .ReturnsAsync((UserModel?)null);

        _userRepositoryMock
            .Setup(repository => repository.Insert(
                It.IsAny<UserModel>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _userRepositoryMock
            .Setup(repository => repository.Update(
                It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Success);

        _uowMock
            .Setup(uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Func<CancellationToken, Task> operation,
                CancellationToken cancellationToken) =>
                operation(cancellationToken));

        _sut = CreateSut(CreateOptions());
    }

    [Fact]
    public async Task Execute_ShouldCreateMasterUser_WhenUserDoesNotExist()
    {
        // Arrange
        UserModel? createdUser = null;
        string? providedPassword = null;

        _userRepositoryMock
            .Setup(repository => repository.Insert(
                It.IsAny<UserModel>(),
                It.IsAny<string>()))
            .Callback<UserModel, string>((user, password) =>
            {
                createdUser = user;
                providedPassword = password;
            })
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _sut.Execute();

        // Assert
        createdUser.Should().NotBeNull();
        createdUser!.UserName.Should().Be(UserName);
        createdUser.Email.Should().Be(Email);
        createdUser.FullName.Should().Be(FullName);
        createdUser.Permissions.Should().Equal(GetExpectedPermissionCodes());
        createdUser.CreatedAt.Should().NotBe(default);

        providedPassword.Should().Be(Password);

        _userRepositoryMock.Verify(
            repository => repository.GetByUsername(UserName),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.GetByEmail(Email),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.Insert(
                It.IsAny<UserModel>(),
                Password),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.Update(
                It.IsAny<UserModel>()),
            Times.Never);

        VerifyTransaction(Times.Once());
    }

    [Fact]
    public async Task Execute_ShouldTrimConfiguredValues_WhenCreatingMasterUser()
    {
        // Arrange
        UserMasterSeed sut = CreateSut(CreateOptions(
            userName: "  master  ",
            email: "  master@example.com  ",
            fullName: "  Master Admin  "));

        UserModel? createdUser = null;

        _userRepositoryMock
            .Setup(repository => repository.Insert(
                It.IsAny<UserModel>(),
                It.IsAny<string>()))
            .Callback<UserModel, string>((user, _) =>
            {
                createdUser = user;
            })
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await sut.Execute();

        // Assert
        createdUser.Should().NotBeNull();
        createdUser!.UserName.Should().Be(UserName);
        createdUser.Email.Should().Be(Email);
        createdUser.FullName.Should().Be(FullName);
    }

    [Fact]
    public async Task Execute_ShouldNotUpdateUser_WhenPermissionsAreAlreadyCorrect()
    {
        // Arrange
        UserModel existingUser = CreateMasterUser(
            permissions: GetExpectedPermissionCodes());

        ConfigureExistingUser(existingUser, existingUser);

        // Act
        await _sut.Execute();

        // Assert
        _userRepositoryMock.Verify(
            repository => repository.Insert(
                It.IsAny<UserModel>(),
                It.IsAny<string>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository => repository.Update(
                It.IsAny<UserModel>()),
            Times.Never);

        VerifyTransaction(Times.Once());
    }

    [Fact]
    public async Task Execute_ShouldUpdateUser_WhenPermissionsDiffer()
    {
        // Arrange
        UserModel existingUser = CreateMasterUser(
            permissions: ["custom.permission"]);

        ConfigureExistingUser(existingUser, existingUser);

        UserModel? updatedUser = null;

        _userRepositoryMock
            .Setup(repository => repository.Update(
                It.IsAny<UserModel>()))
            .Callback<UserModel>(user => updatedUser = user)
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _sut.Execute();

        // Assert
        updatedUser.Should().NotBeNull();
        updatedUser.Should().BeSameAs(existingUser);
        updatedUser!.Permissions.Should().Equal(GetExpectedPermissionCodes());

        _userRepositoryMock.Verify(
            repository => repository.Update(existingUser),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.Insert(
                It.IsAny<UserModel>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldUpdateUser_WhenPermissionsAreNull()
    {
        // Arrange
        UserModel existingUser = CreateMasterUser(
            permissions: null);

        ConfigureExistingUser(existingUser, existingUser);

        // Act
        await _sut.Execute();

        // Assert
        existingUser.Permissions.Should().Equal(GetExpectedPermissionCodes());

        _userRepositoryMock.Verify(
            repository => repository.Update(existingUser),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.Insert(
                It.IsAny<UserModel>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldIgnoreOrderAndCase_WhenComparingPermissions()
    {
        // Arrange
        string[] permissions = GetExpectedPermissionCodes()
            .Reverse()
            .Select(permission => permission.ToUpperInvariant())
            .ToArray();

        UserModel existingUser = CreateMasterUser(
            permissions: permissions);

        ConfigureExistingUser(existingUser, existingUser);

        // Act
        await _sut.Execute();

        // Assert
        _userRepositoryMock.Verify(
            repository => repository.Update(
                It.IsAny<UserModel>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository => repository.Insert(
                It.IsAny<UserModel>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldThrow_WhenUsernameExistsButEmailDoesNot()
    {
        // Arrange
        UserModel existingUser = CreateMasterUser();

        _userRepositoryMock
            .Setup(repository => repository.GetByUsername(UserName))
            .ReturnsAsync(existingUser);

        _userRepositoryMock
            .Setup(repository => repository.GetByEmail(Email))
            .ReturnsAsync((UserModel?)null);

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "The configured master username or email belongs to a different user.");

        VerifyNoUserWrites();
    }

    [Fact]
    public async Task Execute_ShouldThrow_WhenEmailExistsButUsernameDoesNot()
    {
        // Arrange
        UserModel existingUser = CreateMasterUser();

        _userRepositoryMock
            .Setup(repository => repository.GetByUsername(UserName))
            .ReturnsAsync((UserModel?)null);

        _userRepositoryMock
            .Setup(repository => repository.GetByEmail(Email))
            .ReturnsAsync(existingUser);

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "The configured master username or email belongs to a different user.");

        VerifyNoUserWrites();
    }

    [Fact]
    public async Task Execute_ShouldThrow_WhenUsernameAndEmailBelongToDifferentUsers()
    {
        // Arrange
        UserModel userByUsername = CreateMasterUser(id: "user-1");
        UserModel userByEmail = CreateMasterUser(id: "user-2");

        ConfigureExistingUser(userByUsername, userByEmail);

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "The configured master username or email belongs to a different user.");

        VerifyNoUserWrites();
    }

    [Fact]
    public async Task Execute_ShouldThrow_WhenCreatingMasterUserFails()
    {
        // Arrange
        _userRepositoryMock
            .Setup(repository => repository.Insert(
                It.IsAny<UserModel>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError
                {
                    Code = "DuplicateUserName",
                    Description = "Username already exists."
                }));

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Could not create the master user.*DuplicateUserName*");

        _userRepositoryMock.Verify(
            repository => repository.Insert(
                It.IsAny<UserModel>(),
                Password),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.Update(
                It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldThrow_WhenUpdatingMasterUserFails()
    {
        // Arrange
        UserModel existingUser = CreateMasterUser(
            permissions: ["custom.permission"]);

        ConfigureExistingUser(existingUser, existingUser);

        _userRepositoryMock
            .Setup(repository => repository.Update(existingUser))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError
                {
                    Code = "UpdateFailed",
                    Description = "Could not update user."
                }));

        // Act
        Func<Task> act = () => _sut.Execute();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "*Could not update the master user's permissions.*UpdateFailed*");

        _userRepositoryMock.Verify(
            repository => repository.Update(existingUser),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldThrowImmediately_WhenCancellationIsRequested()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Func<Task> act = () => _sut.Execute(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        VerifyTransaction(Times.Never());

        _userRepositoryMock.Verify(
            repository => repository.GetByUsername(It.IsAny<string>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository => repository.GetByEmail(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldPropagateCancellationTokenToTransaction()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        CancellationToken expectedToken = cts.Token;
        CancellationToken receivedToken = default;

        _uowMock
            .Setup(uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Func<CancellationToken, Task> operation,
                CancellationToken token) =>
            {
                receivedToken = token;
                return operation(token);
            });

        // Act
        await _sut.Execute(expectedToken);

        // Assert
        receivedToken.Should().Be(expectedToken);

        VerifyTransaction(Times.Once(), expectedToken);
    }

    private static UserMasterOptions CreateOptions(
        string userName = UserName,
        string email = Email,
        string fullName = FullName,
        string password = Password)
    {
        return new UserMasterOptions
        {
            UserName = userName,
            Email = email,
            FullName = fullName,
            Password = password
        };
    }

    private UserMasterSeed CreateSut(UserMasterOptions options)
    {
        return new UserMasterSeed(
            _uowMock.Object,
            Options.Create(options),
            _loggerMock.Object);
    }

    private void ConfigureExistingUser(
        UserModel userByUsername,
        UserModel userByEmail)
    {
        _userRepositoryMock
            .Setup(repository => repository.GetByUsername(UserName))
            .ReturnsAsync(userByUsername);

        _userRepositoryMock
            .Setup(repository => repository.GetByEmail(Email))
            .ReturnsAsync(userByEmail);
    }

    private static UserModel CreateMasterUser(
        string id = "master-user-id",
        string[]? permissions = null)
    {
        return new UserModel
        {
            Id = id,
            UserName = UserName,
            Email = Email,
            FullName = FullName,
            Permissions = permissions!,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string[] GetExpectedPermissionCodes()
    {
        return PermissionSeeder.Permissions
            .Select(permission => permission.Code)
            .ToArray();
    }

    private void VerifyNoUserWrites()
    {
        _userRepositoryMock.Verify(
            repository => repository.Insert(
                It.IsAny<UserModel>(),
                It.IsAny<string>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository => repository.Update(
                It.IsAny<UserModel>()),
            Times.Never);
    }

    private void VerifyTransaction(
        Times times,
        CancellationToken? expectedToken = null)
    {
        if (expectedToken.HasValue)
        {
            _uowMock.Verify(
                uow => uow.ExecuteTransactionAsync(
                    It.IsAny<Func<CancellationToken, Task>>(),
                    expectedToken.Value),
                times);

            return;
        }

        _uowMock.Verify(
            uow => uow.ExecuteTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            times);
    }
}