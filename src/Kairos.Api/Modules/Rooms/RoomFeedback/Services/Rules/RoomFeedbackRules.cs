using Kairos.Api.Config.UnitOfWork;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Services.Rules;

internal static class RoomFeedbackRules
{
    public const string ManagePermission = "rooms.feedbacks.manage";

    public static Task<bool> CanManageAsync(
        IUnitOfWork uow,
        string userId)
    {
        return uow.UserRepository.ExistsPermission(ManagePermission, userId);
    }
}