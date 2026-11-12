using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;

namespace Kairos.Api.Modules.Rooms.MaintenanceTicket.Models;

public class MaintenanceTicketModel : BaseModel
{
    public required string RoomId { get; set; }
    public RoomModel? Room { get; set; }

    public required string ReportedBy { get; set; }
    public UserModel? Reporter { get; set; }

    public required string Title { get; set; }
    public required string Description { get; set; }
    
    public MaintenanceCategoryEnum Category { get; set; } = MaintenanceCategoryEnum.Other;
    public MaintenancePriorityEnum Priority { get; set; } = MaintenancePriorityEnum.Normal;
    public MaintenanceTicketStatus Status { get; set; } = MaintenanceTicketStatus.Open;

    public DateTimeOffset? DueAt { get; set; }
    
    public string? ResolvedBy { get; set; }
    public UserModel? Resolver { get; set; }
    
    public required string BlockoutId { get; set; }
    public RoomBlockoutModel? Blockout { get; set; }
}