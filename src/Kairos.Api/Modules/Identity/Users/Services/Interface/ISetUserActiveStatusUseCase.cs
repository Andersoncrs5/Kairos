using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Users.DTOs.Commands;

namespace Kairos.Api.Modules.Identity.Users.Services.Interface;

public interface ISetUserActiveStatusUseCase
    : IUseCase<SetUserActiveStatusCommand, Result<bool>>
{
}