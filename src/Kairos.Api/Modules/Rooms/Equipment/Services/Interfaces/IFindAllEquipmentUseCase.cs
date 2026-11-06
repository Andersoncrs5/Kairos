using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.Model;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

public interface IFindAllEquipmentUseCase : IUseCase<FilterEquipmentDto, Page<EquipmentModel>>
{
}