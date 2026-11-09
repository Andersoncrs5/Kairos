using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Services.Interfaces;

public interface IDeleteRoomFeedbackUseCase
    : IUseCase<DeleteRoomFeedbackCommand, Result<bool>>
{
}