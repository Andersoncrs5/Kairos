using System.Security.Claims;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

public interface IGetPrincipalFromExpiredTokenUseCase
{
    ClaimsPrincipal Execute(string token);
}