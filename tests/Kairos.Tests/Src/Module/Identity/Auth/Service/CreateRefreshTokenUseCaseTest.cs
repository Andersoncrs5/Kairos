using FluentAssertions;
using Kairos.Api.Modules.Identity.Auth.Services.Providers;
using Xunit;

namespace Kairos.Tests.Module.Identity.Auth.Service;

public class CreateRefreshTokenUseCaseTest
{
    private readonly CreateRefreshTokenUseCase _sut = new();

    [Fact]
    public void Execute_ShouldReturnNonEmptyToken()
    {
        // Act
        var result = _sut.Execute();

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Execute_ShouldReturnValidBase64Token()
    {
        // Act
        var result = _sut.Execute();

        // Assert
        var decodedToken = Convert.FromBase64String(result);

        decodedToken.Should().HaveCount(128);
    }

    [Fact]
    public void Execute_ShouldGenerateUniqueTokens()
    {
        // Act
        var firstToken = _sut.Execute();
        var secondToken = _sut.Execute();

        // Assert
        firstToken.Should().NotBe(secondToken);
    }
}