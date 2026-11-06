using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.Equipment.Model;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

public interface IFindEquipmentByIdUseCase : IUseCase<string, Result<EquipmentModel>>
{
}