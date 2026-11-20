using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

/// <summary>
/// Issues the access token, refresh token and profile for a user that has
/// already been authenticated by some other means (passkey, social login...).
/// </summary>
public interface ICreateSessionUseCase
    : IUseCase<UserModel, Result<Tokens>> { }