using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Users.Services.Interface;

public interface IFindUserByIdUseCase: IUseCase<string, Result<UserModel>> { }