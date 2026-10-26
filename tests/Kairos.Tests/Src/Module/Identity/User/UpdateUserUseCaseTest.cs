using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.DTOs.Commands;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Identity.Users.Services.Providers;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace Kairos.Tests.Module.Identity.User;

public class UpdateUserUseCaseTest
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly UpdateUserUseCase _sut;

    public UpdateUserUseCaseTest()
    {
        _uowMock
            .Setup(uow => uow.UserRepository)
            .Returns(_userRepositoryMock.Object);

        _sut = new UpdateUserUseCase(_uowMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllFieldsAreValid_ShouldUpdateUser()
    {
        // Arrange
        UserModel user = CreateUser();

        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                FullName = "  New Full Name  ",
                UserName = "  newusername  ",
                Email = "  new@example.com  ",
                PhoneNumber = "+12025550174"
            });

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync("user-1"))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository => repository.Update(It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.True(result.Value);

        Assert.Equal("New Full Name", user.FullName);
        Assert.Equal("newusername", user.UserName);
        Assert.Equal("new@example.com", user.Email);
        Assert.Equal("+12025550174", user.PhoneNumber);

        Assert.False(user.EmailConfirmed);
        Assert.False(user.PhoneNumberConfirmed);

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync("user-1"),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyFullNameIsProvided_ShouldPreserveOtherFields()
    {
        // Arrange
        UserModel user = CreateUser();

        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                FullName = "Updated Name"
            });

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync("user-1"))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository => repository.Update(It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        Assert.Equal("Updated Name", user.FullName);
        Assert.Equal("oldusername", user.UserName);
        Assert.Equal("old@example.com", user.Email);
        Assert.Equal("+12025550123", user.PhoneNumber);

        Assert.True(user.EmailConfirmed);
        Assert.True(user.PhoneNumberConfirmed);

        _userRepositoryMock.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailDiffersOnlyByCase_ShouldPreserveEmailConfirmation()
    {
        // Arrange
        UserModel user = CreateUser();

        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                Email = "OLD@example.com"
            });

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync("user-1"))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository => repository.Update(It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("old@example.com", user.Email);
        Assert.True(user.EmailConfirmed);

        _userRepositoryMock.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPhoneNumberIsUnchanged_ShouldPreservePhoneConfirmation()
    {
        // Arrange
        UserModel user = CreateUser();

        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                PhoneNumber = "+12025550123"
            });

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync("user-1"))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository => repository.Update(It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("+12025550123", user.PhoneNumber);
        Assert.True(user.PhoneNumberConfirmed);

        _userRepositoryMock.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                FullName = "Updated Name"
            });

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync("user-1"))
            .ReturnsAsync((UserModel?)null);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync("user-1"),
            Times.Once);

        _userRepositoryMock.Verify(
            repository => repository.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoFieldsAreProvided_ShouldReturnBadRequest()
    {
        // Arrange
        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO());

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<string>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository => repository.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsMissing_ShouldReturnBadRequest()
    {
        // Arrange
        UpdateUserCommand command = new(
            "",
            new UpdateUserDTO
            {
                FullName = "Updated Name"
            });

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<string>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository => repository.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData(" ")]
    public async Task ExecuteAsync_WhenUserNameIsInvalid_ShouldReturnBadRequest(
        string userName)
    {
        // Arrange
        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                UserName = userName
            });

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<string>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository => repository.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("invalid@")]
    public async Task ExecuteAsync_WhenEmailIsInvalid_ShouldReturnBadRequest(
        string email)
    {
        // Arrange
        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                Email = email
            });

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<string>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository => repository.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFullNameExceedsMaximumLength_ShouldReturnBadRequest()
    {
        // Arrange
        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                FullName = new string('a', 151)
            });

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdentityReturnsDuplicateUserName_ShouldReturnConflict()
    {
        // Arrange
        UserModel user = CreateUser();

        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                UserName = "newusername"
            });

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync("user-1"))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository => repository.Update(It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError
                {
                    Code = "DuplicateUserName",
                    Description = "Username is already taken."
                }));

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);

        _userRepositoryMock.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdentityReturnsDuplicateEmail_ShouldReturnConflict()
    {
        // Arrange
        UserModel user = CreateUser();

        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                Email = "new@example.com"
            });

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync("user-1"))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository => repository.Update(It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError
                {
                    Code = "DuplicateEmail",
                    Description = "Email is already registered."
                }));

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);

        _userRepositoryMock.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdentityUpdateFails_ShouldReturnBadRequest()
    {
        // Arrange
        UserModel user = CreateUser();

        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                FullName = "Updated Name"
            });

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync("user-1"))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository => repository.Update(It.IsAny<UserModel>()))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError
                {
                    Code = "GeneralError",
                    Description = "The user could not be updated."
                }));

        // Act
        Result<bool> result = await _sut.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);

        _userRepositoryMock.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();

        UpdateUserCommand command = new(
            "user-1",
            new UpdateUserDTO
            {
                FullName = "Updated Name"
            });

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ExecuteAsync(command, cts.Token));

        // Assert
        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<string>()),
            Times.Never);

        _userRepositoryMock.Verify(
            repository => repository.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    private static UserModel CreateUser()
    {
        return new UserModel
        {
            Id = "user-1",
            UserName = "oldusername",
            Email = "old@example.com",
            FullName = "Old Full Name",
            PhoneNumber = "+12025550123",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}