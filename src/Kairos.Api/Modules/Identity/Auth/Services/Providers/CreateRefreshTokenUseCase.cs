using System.Security.Cryptography;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public class CreateRefreshTokenUseCase: ICreateRefreshTokenUseCase
{
    public string Execute()
    {
        var randomNumber = new byte[128];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}