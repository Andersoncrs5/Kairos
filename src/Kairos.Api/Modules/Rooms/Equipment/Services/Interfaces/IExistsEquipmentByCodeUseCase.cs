using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

public interface IExistsEquipmentByCodeUseCase : IUseCase<string, Result<bool>> { }