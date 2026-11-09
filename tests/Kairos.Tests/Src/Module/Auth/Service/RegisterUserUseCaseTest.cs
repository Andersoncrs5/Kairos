using FluentAssertions;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.UserPreference.Mapper;
using Kairos.Api.Modules.Identity.UserPreference.Models;
using Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Identity.Users.Services.Interface;
using NSubstitute;
using Xunit;

namespace Kairos.Tests.Module.Auth.Service;

public class RegisterUserUseCaseTest
{
    private readonly IUnitOfWork _uow;
    private readonly ICreateUserUseCase _createUserUseCase;
    private readonly ICreateTokenUseCase _createTokenUseCase;
    private readonly ICreateRefreshTokenUseCase _createRefreshTokenUseCase;
    private readonly IFindUserPreferenceUseCase _findUserPreferenceUseCase;

    private readonly RegisterUserUseCase _sut;

    public RegisterUserUseCaseTest()
    {
        _uow = Substitute.For<IUnitOfWork>();

        _createUserUseCase = Substitute.For<ICreateUserUseCase>();
        _createTokenUseCase = Substitute.For<ICreateTokenUseCase>();
        _createRefreshTokenUseCase = Substitute.For<ICreateRefreshTokenUseCase>();
        _findUserPreferenceUseCase = Substitute.For<IFindUserPreferenceUseCase>();

        _sut = new RegisterUserUseCase(
            _uow,
            _createUserUseCase,
            _createTokenUseCase,
            _createRefreshTokenUseCase,
            _findUserPreferenceUseCase
        );
    }

    // ---------------------------------------------------------------
    // Success
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTokensAndUpdateUser_WhenRegistrationSucceeds()
    {
        // Arrange
        var dto = CreateUserDto();
        var user = CreateUser();

        var tokenResponse = CreateTokenResponse();

        var preference = CreateUserPreference(user.Id);

        const string refreshToken = "refresh-token";

        SetupSuccessfulRegistration(
            dto,
            user,
            tokenResponse,
            preference,
            refreshToken
        );

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        result.Value.Token.Should().Be(tokenResponse.Token);
        result.Value.ExpiredToken.Should().Be(tokenResponse.Expired);
        result.Value.RefreshToken.Should().Be(refreshToken);

        result.Value.User.Should().BeEquivalentTo(UserMapper.ToDto(user));
        result.Value.Permissions.Should().BeEquivalentTo(user.Permissions);

        result.Value.Preference.Should()
            .BeEquivalentTo(UserPreferenceMapper.ToDto(preference));

        user.RefreshToken.Should().Be(refreshToken);

        await _uow.UserRepository.Received(1).Update(user);

        await _createUserUseCase.Received(1)
            .ExecuteAsync(dto, Arg.Any<CancellationToken>());

        await _createTokenUseCase.Received(1)
            .ExecuteAsync(user);

        _createRefreshTokenUseCase.Received(1).Execute();

        await _findUserPreferenceUseCase.Received(1)
            .ExecuteAsync(user.Id, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // User creation failure
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenUserCreationFails()
    {
        // Arrange
        var dto = CreateUserDto();

        var userResult = Result<UserModel>.Failure(
            ["User creation failed"],
            400
        );

        _createUserUseCase
            .ExecuteAsync(dto, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(userResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(userResult.StatusCode);
        result.Errors.Should().BeEquivalentTo(userResult.Errors);

        await _createTokenUseCase.DidNotReceive()
            .ExecuteAsync(Arg.Any<UserModel>());

        _createRefreshTokenUseCase.DidNotReceive().Execute();

        await _findUserPreferenceUseCase.DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );

        await _uow.UserRepository.DidNotReceive()
            .Update(Arg.Any<UserModel>());
    }

    // ---------------------------------------------------------------
    // Access token failure
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenAccessTokenCreationFails()
    {
        // Arrange
        var dto = CreateUserDto();
        var user = CreateUser();

        var tokenResult = Result<TokenResponse>.Failure(
            ["Token creation failed"],
            500
        );

        _createUserUseCase
            .ExecuteAsync(dto, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<UserModel>.Success(user)));

        _createTokenUseCase
            .ExecuteAsync(user)
            .Returns(Task.FromResult(tokenResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(tokenResult.StatusCode);
        result.Errors.Should().BeEquivalentTo(tokenResult.Errors);

        _createRefreshTokenUseCase.DidNotReceive().Execute();

        await _findUserPreferenceUseCase.DidNotReceive()
            .ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );

        await _uow.UserRepository.DidNotReceive()
            .Update(Arg.Any<UserModel>());
    }

    // ---------------------------------------------------------------
    // User preference failure
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenUserPreferenceLookupFails()
    {
        // Arrange
        var dto = CreateUserDto();
        var user = CreateUser();

        var tokenResponse = CreateTokenResponse();

        var preferenceResult = Result<UserPreferenceModel>.Failure(
            ["User preference lookup failed"],
            500
        );

        _createUserUseCase
            .ExecuteAsync(dto, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<UserModel>.Success(user)));

        _createTokenUseCase
            .ExecuteAsync(user)
            .Returns(Task.FromResult(Result<TokenResponse>.Success(tokenResponse)));

        _createRefreshTokenUseCase
            .Execute()
            .Returns("refresh-token");

        _findUserPreferenceUseCase
            .ExecuteAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(preferenceResult));

        // Act
        var result = await _sut.ExecuteAsync(dto);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(preferenceResult.StatusCode);
        result.Errors.Should().BeEquivalentTo(preferenceResult.Errors);

        _createRefreshTokenUseCase.Received(1).Execute();

        await _uow.UserRepository.Received(1).Update(user);

        await _findUserPreferenceUseCase.Received(1)
            .ExecuteAsync(user.Id, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShouldPassCancellationTokenToDependencies()
    {
        // Arrange
        var dto = CreateUserDto();
        var user = CreateUser();

        var tokenResponse = CreateTokenResponse();
        var preference = CreateUserPreference(user.Id);

        using var cts = new CancellationTokenSource();

        SetupSuccessfulRegistration(
            dto,
            user,
            tokenResponse,
            preference,
            "refresh-token"
        );

        // Act
        var result = await _sut.ExecuteAsync(dto, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _createUserUseCase.Received(1)
            .ExecuteAsync(dto, cts.Token);

        await _findUserPreferenceUseCase.Received(1)
            .ExecuteAsync(user.Id, cts.Token);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void SetupSuccessfulRegistration(
        CreateUserDTO dto,
        UserModel user,
        TokenResponse tokenResponse,
        UserPreferenceModel preference,
        string refreshToken)
    {
        _createUserUseCase
            .ExecuteAsync(dto, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<UserModel>.Success(user)));

        _createTokenUseCase
            .ExecuteAsync(user)
            .Returns(Task.FromResult(Result<TokenResponse>.Success(tokenResponse)));

        _createRefreshTokenUseCase
            .Execute()
            .Returns(refreshToken);

        _findUserPreferenceUseCase
            .ExecuteAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<UserPreferenceModel>.Success(preference)));
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
            FullName = "Test User",
            Permissions = [],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static TokenResponse CreateTokenResponse()
    {
        return new TokenResponse
        {
            Token = "access-token",
            Expired = DateTimeOffset.UtcNow.AddMinutes(15)
        };
    }

    private static UserPreferenceModel CreateUserPreference(string userId)
    {
        return new UserPreferenceModel
        {
            Id = "preference-123",
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}