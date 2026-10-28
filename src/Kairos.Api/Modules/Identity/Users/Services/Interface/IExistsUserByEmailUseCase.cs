using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Identity.Users.Services.Interface;

public interface IExistsUserByEmailUseCase: IUseCase<string, Result<bool>> { }