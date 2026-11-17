using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Identity.Auth.DTOs.Commands;
using Kairos.Api.Modules.Identity.Auth.DTOs.Response;

namespace Kairos.Api.Modules.Identity.Auth.Services.Interfaces;

public interface IExternalLoginUseCase
    : IUseCase<ExternalLoginCommand, Result<Tokens>> { }