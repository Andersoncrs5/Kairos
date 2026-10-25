using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;
using NSubstitute;

namespace Kairos.Tests.Module.Auth.Service;

public class RegisterUserUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly ICreateUserUseCase _createUserUseCase;
    private readonly ICreateTokenUseCase _createTokenUseCase;
    private readonly ICreateRefreshTokenUseCase _createRefreshTokenUseCase;
    private readonly RegisterUserUseCase _sut;

    public RegisterUserUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();
        _createUserUseCase = Substitute.For<ICreateUserUseCase>();
        _createTokenUseCase = Substitute.For<ICreateTokenUseCase>();
        _createRefreshTokenUseCase =
            Substitute.For<ICreateRefreshTokenUseCase>();

        _sut = new RegisterUserUseCase(
            _uow,
            _createUserUseCase,
            _createTokenUseCase,
            _createRefreshTokenUseCase);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTokens_AndUpdateUser_WhenRegistrationSucceeds()
    {
        // Arrange
        var dto = CreateUserDto();
        var user = CreateUser();

        var tokenResponse = new TokenResponse
        {
            Token = "access-token",
            Expired = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        const string refreshToken = "refresh-token";

        _createUserUseCase
            .ExecuteAsync(dto, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<UserModel>.Success(user)));

        _createTokenUseCase
            .ExecuteAsync(user)
            .Returns(Task.FromResult(
                Result<TokenResponse>.Success(tokenResponse)));

        _createRefreshTokenUseCase
            .Execute()
            .Returns(refreshToken);

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value.Token.Should().Be(tokenResponse.Token);
        result.Value.ExpiredToken.Should().Be(tokenResponse.Expired);
        result.Value.RefreshToken.Should().Be(refreshToken);

        user.RefreshToken.Should().Be(refreshToken);

        await _uow.UserRepository
            .Received(1)
            .Update(user);

        _createRefreshTokenUseCase
            .Received(1)
            .Execute();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenUserCreationFails()
    {
        // Arrange
        var dto = CreateUserDto();

        var userResult = Result<UserModel>.Failure(
            ["User creation failed"],
            400);

        _createUserUseCase
            .ExecuteAsync(dto, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(userResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(userResult.StatusCode);

        await _createTokenUseCase
            .DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        _createRefreshTokenUseCase
            .DidNotReceive()
            .Execute();

        await _uow.UserRepository
            .DidNotReceive()
            .Update(Arg.Any<UserModel>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenAccessTokenCreationFails()
    {
        // Arrange
        var dto = CreateUserDto();
        var user = CreateUser();

        var tokenResult = Result<TokenResponse>.Failure(
            ["Token creation failed"],
            500);

        _createUserUseCase
            .ExecuteAsync(dto, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<UserModel>.Success(user)));

        _createTokenUseCase
            .ExecuteAsync(user)
            .Returns(Task.FromResult(tokenResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(tokenResult.StatusCode);

        _createRefreshTokenUseCase
            .DidNotReceive()
            .Execute();

        await _uow.UserRepository
            .DidNotReceive()
            .Update(Arg.Any<UserModel>());
    }

    private static CreateUserDTO CreateUserDto()
    {
        return new CreateUserDTO
        {
            FullName = "Test User",
            UserName = "testuser",
            Email = "test@example.com",
            PhoneNumber = null,
            Password = "TestPassword123!"
        };
    }

    private static UserModel CreateUser()
    {
        return new UserModel
        {
            Id = "user-123",
            UserName = "testuser",
            Email = "test@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}