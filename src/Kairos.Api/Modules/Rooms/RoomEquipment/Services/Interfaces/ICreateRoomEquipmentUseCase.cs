using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

public interface ICreateRoomEquipmentUseCase: IUseCase<AddEquipmentToRoomCommand, Result<RoomEquipmentModel>> { }