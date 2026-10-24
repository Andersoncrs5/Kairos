using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentAssertions;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Kairos.Tests.Module.Identity.Auth.Service;

public class CreateTokenUseCaseTest
{
    private readonly JwtOptions _jwtOptions;
    private readonly CreateTokenUseCase _sut;

    public CreateTokenUseCaseTest()
    {
        _jwtOptions = new JwtOptions
        {
            SecretKey = "test-secret-key-that-is-long-enough-for-hs256",
            ValidIssuer = "Kairos",
            ValidAudience = "Kairos.Client",
            TokenValidityInMinutes = 15,
            RefreshTokenValidityInMinutes = 1
        };

        _sut = new CreateTokenUseCase(
            Options.Create(_jwtOptions));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSignedToken_WithExpectedClaims()
    {
        // Arrange
        var user = CreateUser();

        // Act
        var result = await _sut.ExecuteAsync(user);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var response = result.Value;

        response.Token.Should().NotBeNullOrWhiteSpace();
        response.Expired.Should().BeAfter(DateTimeOffset.UtcNow);

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.ValidIssuer,

            ValidateAudience = true,
            ValidAudience = _jwtOptions.ValidAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(
            response.Token,
            validationParameters,
            out var validatedToken);

        validatedToken.Should().BeOfType<JwtSecurityToken>();

        principal.FindFirst(JwtRegisteredClaimNames.Sub)!
            .Value.Should().Be(user.Id);

        principal.FindFirst(JwtRegisteredClaimNames.Email)!
            .Value.Should().Be(user.Email);

        principal.FindFirst(JwtRegisteredClaimNames.Name)!
            .Value.Should().Be(user.UserName);

        principal.FindFirst(JwtRegisteredClaimNames.Jti)!
            .Value.Should().NotBeNullOrWhiteSpace();

        principal.FindFirst(JwtRegisteredClaimNames.Sid)!
            .Value.Should().Be(user.Id);

        principal.FindAll("permission")
            .Select(claim => claim.Value)
            .Should()
            .BeEquivalentTo(user.Permissions);

        validatedToken.ValidTo.Should()
            .BeCloseTo(
                response.Expired.UtcDateTime,
                TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateUniqueJti_ForEachToken()
    {
        // Arrange
        var user = CreateUser();

        // Act
        var firstResult = await _sut.ExecuteAsync(user);
        var secondResult = await _sut.ExecuteAsync(user);

        // Assert
        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        var firstToken = handler.ReadJwtToken(
            firstResult.Value.Token);

        var secondToken = handler.ReadJwtToken(
            secondResult.Value.Token);

        firstToken.Id.Should().NotBe(secondToken.Id);
    }

    private static UserModel CreateUser()
    {
        return new UserModel
        {
            Id = "user-123",
            UserName = "testuser",
            Email = "test@example.com",
            Permissions =
            [
                "movies.read",
                "movies.create",
                "movies.delete"
            ],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}