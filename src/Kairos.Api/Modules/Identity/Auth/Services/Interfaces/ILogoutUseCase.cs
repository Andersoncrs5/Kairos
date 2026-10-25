using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

public interface ILogoutUseCase : IUseCase<string, Result<bool>> { }