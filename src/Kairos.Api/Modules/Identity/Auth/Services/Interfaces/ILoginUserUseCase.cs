using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Auth.DTOs.Request;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

public interface ILoginUserUseCase: IUseCase<LoginUserDTO, Result<Tokens>> { }