using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

public interface ICancelRoomBlockoutUseCase: IUseCase<CancelRoomBlockoutCommand,  Result<RoomBlockoutModel>> { }