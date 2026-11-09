using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Response;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Services.Interfaces;

public interface IGetRoomFeedbackSummaryUseCase
    : IUseCase<GetRoomFeedbackSummaryCommand, Result<RoomFeedbackSummaryDto>>
{
}