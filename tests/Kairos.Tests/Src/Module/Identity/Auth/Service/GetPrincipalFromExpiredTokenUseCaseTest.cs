using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Kairos.Tests.Module.Identity.Auth.Service;

public class GetPrincipalFromExpiredTokenUseCaseTest
{
    private readonly JwtOptions _jwtOptions;
    private readonly GetPrincipalFromExpiredTokenUseCase _sut;

    public GetPrincipalFromExpiredTokenUseCaseTest()
    {
        _jwtOptions = new JwtOptions
        {
            SecretKey = "test-secret-key-that-is-long-enough-for-hs256",
            ValidIssuer = "Kairos",
            ValidAudience = "Kairos.Client",
            TokenValidityInMinutes = 15,
            RefreshTokenValidityInMinutes = 60
        };

        _sut = new GetPrincipalFromExpiredTokenUseCase(
            Options.Create(_jwtOptions));
    }

    [Fact]
    public void Execute_ShouldReturnPrincipal_WhenTokenIsExpiredButValid()
    {
        // Arrange
        var token = CreateToken(
            expires: DateTime.UtcNow.AddMinutes(-5));

        // Act
        var principal = _sut.Execute(token);

        // Assert
        principal.Should().NotBeNull();

        principal.FindFirst(JwtRegisteredClaimNames.Sub)!
            .Value.Should().Be("user-123");

        principal.FindFirst("permission")!
            .Value.Should().Be("movies.read");
    }

    [Fact]
    public void Execute_ShouldThrowSecurityTokenException_WhenSignatureIsInvalid()
    {
        // Arrange
        var token = CreateToken(
            secretKey: "another-secret-key-that-is-long-enough-for-hs256");

        // Act
        var act = () => _sut.Execute(token);

        // Assert
        act.Should()
            .Throw<SecurityTokenException>();
    }

    [Fact]
    public void Execute_ShouldThrowSecurityTokenException_WhenIssuerIsInvalid()
    {
        // Arrange
        var token = CreateToken(issuer: "InvalidIssuer");

        // Act
        var act = () => _sut.Execute(token);

        // Assert
        act.Should()
            .Throw<SecurityTokenException>();
    }

    [Fact]
    public void Execute_ShouldThrowSecurityTokenException_WhenAudienceIsInvalid()
    {
        // Arrange
        var token = CreateToken(audience: "InvalidAudience");

        // Act
        var act = () => _sut.Execute(token);

        // Assert
        act.Should()
            .Throw<SecurityTokenException>();
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Execute_ShouldThrowArgumentException_WhenTokenIsEmptyOrWhitespace(
        string token)
    {
        // Act
        var act = () => _sut.Execute(token);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private string CreateToken(
        string? secretKey = null,
        string? issuer = null,
        string? audience = null,
        string algorithm = SecurityAlgorithms.HmacSha256,
        DateTime? expires = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                secretKey ?? _jwtOptions.SecretKey));

        var credentials = new SigningCredentials(
            key,
            algorithm);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "user-123"),
            new("permission", "movies.read")
        };

        var token = new JwtSecurityToken(
            issuer: issuer ?? _jwtOptions.ValidIssuer,
            audience: audience ?? _jwtOptions.ValidAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: expires ?? DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}