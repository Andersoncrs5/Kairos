using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Equipment.Model;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

public interface IUpdateEquipmentUseCase
    : IUseCase<UpdateEquipmentCommand, Result<EquipmentModel>>
{
}