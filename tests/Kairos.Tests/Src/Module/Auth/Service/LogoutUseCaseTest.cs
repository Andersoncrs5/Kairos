using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace Kairos.Tests.Module.Auth.Service;

public class LogoutUseCaseTest
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();

    private readonly LogoutUseCase _sut;

    public LogoutUseCaseTest()
    {
        _unitOfWorkMock
            .SetupGet(uow => uow.UserRepository)
            .Returns(_userRepositoryMock.Object);

        _sut = new LogoutUseCase(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        const string userId = "user-123";

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(userId))
            .ReturnsAsync((UserModel?)null);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("User not found");

        _userRepositoryMock.Verify(
            repository => repository.Update(It.IsAny<UserModel>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_AndClearsRefreshToken()
    {
        // Arrange
        const string userId = "user-123";

        UserModel user = new()
        {
            Id = userId,
            RefreshToken = "refresh-token",
            RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository => repository.Update(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        Result<bool> result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeTrue();

        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();

        _userRepositoryMock.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenUserUpdateFails()
    {
        // Arrange
        const string userId = "user-123";
        const string errorMessage = "Database update failed";

        UserModel user = new()
        {
            Id = userId,
            RefreshToken = "refresh-token",
            RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(repository => repository.Update(user))
            .ReturnsAsync(
                IdentityResult.Failed(
                    new IdentityError
                    {
                        Description = errorMessage
                    }));

        // Act
        Result<bool> result = await _sut.ExecuteAsync(userId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(errorMessage);

        _userRepositoryMock.Verify(
            repository => repository.Update(user),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancellationIsRequested()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Func<Task> act = () => _sut.ExecuteAsync(
            "user-123",
            cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        _userRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<string>()),
            Times.Never);
    }
}