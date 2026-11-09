using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.UserPreference.Models;

namespace Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;

public interface IResetUserPreferenceUseCase
    : IUseCase<string, Result<UserPreferenceModel>>
{
}