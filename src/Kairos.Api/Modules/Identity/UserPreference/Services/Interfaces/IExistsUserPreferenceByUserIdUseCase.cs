using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.UserPreference.Services.Interfaces;

public interface IExistsUserPreferenceByUserIdUseCase
    : IUseCase<string, Result<bool>>
{
}