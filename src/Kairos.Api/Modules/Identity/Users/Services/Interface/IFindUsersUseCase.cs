using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Identity.Users.DTOs.Request;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.Users.Services.Interface;

public interface IFindUsersUseCase: IUseCase<FilterUserDto, Page<UserModel>>
{
    
}