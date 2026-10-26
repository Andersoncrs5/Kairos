using Kairos.Api.Common.Enums;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Permissions.Models;

namespace Kairos.Api.Modules.Identity.Permissions.Seed;

public class PermissionSeeder(IUnitOfWork uow) : IPermissionSeeder
{
    public static IReadOnlyList<PermissionsModel> Permissions { get; } =
        Array.AsReadOnly<PermissionsModel>(
        [
            CreatePermission(
                "identity.users.create",
                "Create users",
                PermissionModuleEnum.Identity,
                "Allows creating users."),
            CreatePermission(
                "identity.users.read",
                "Read users",
                PermissionModuleEnum.Identity,
                "Allows retrieving a user by ID."),
            CreatePermission(
                "identity.users.list",
                "List users",
                PermissionModuleEnum.Identity,
                "Allows listing users with filters and pagination."),
            CreatePermission(
                "identity.users.update",
                "Update users",
                PermissionModuleEnum.Identity,
                "Allows updating user information."),
            CreatePermission(
                "identity.users.delete",
                "Delete users",
                PermissionModuleEnum.Identity,
                "Allows deleting users."),

            // ----------------------------------------------------------
            // Identity - Permissions
            // ----------------------------------------------------------
            CreatePermission(
                "identity.permissions.create",
                "Create permissions",
                PermissionModuleEnum.Identity,
                "Allows creating permissions."),
            CreatePermission(
                "identity.permissions.read",
                "Read permissions",
                PermissionModuleEnum.Identity,
                "Allows retrieving a permission by ID."),
            CreatePermission(
                "identity.permissions.list",
                "List permissions",
                PermissionModuleEnum.Identity,
                "Allows listing permissions."),
            CreatePermission(
                "identity.permissions.update",
                "Update permissions",
                PermissionModuleEnum.Identity,
                "Allows updating permissions."),
            CreatePermission(
                "identity.permissions.delete",
                "Delete permissions",
                PermissionModuleEnum.Identity,
                "Allows deleting permissions."),

            // ----------------------------------------------------------
            // Identity - User permissions
            // ----------------------------------------------------------
            CreatePermission(
                "identity.permission-users.create",
                "Grant user permissions",
                PermissionModuleEnum.Identity,
                "Allows assigning permissions to users."),
            CreatePermission(
                "identity.permission-users.read",
                "Read user permissions",
                PermissionModuleEnum.Identity,
                "Allows retrieving permissions assigned to a user."),
            CreatePermission(
                "identity.permission-users.list",
                "List user permissions",
                PermissionModuleEnum.Identity,
                "Allows listing user permission assignments."),
            CreatePermission(
                "identity.permission-users.update",
                "Update user permissions",
                PermissionModuleEnum.Identity,
                "Allows updating permissions assigned to a user."),
            CreatePermission(
                "identity.permission-users.delete",
                "Revoke user permissions",
                PermissionModuleEnum.Identity,
                "Allows removing permissions from users."),

            // ----------------------------------------------------------
            // Categories
            // ----------------------------------------------------------
            CreatePermission(
                "categories.create",
                "Create categories",
                PermissionModuleEnum.Categories,
                "Allows creating categories."),
            CreatePermission(
                "categories.read",
                "Read categories",
                PermissionModuleEnum.Categories,
                "Allows retrieving a category by ID."),
            CreatePermission(
                "categories.list",
                "List categories",
                PermissionModuleEnum.Categories,
                "Allows listing categories with filters and pagination."),
            CreatePermission(
                "categories.update",
                "Update categories",
                PermissionModuleEnum.Categories,
                "Allows updating category information."),
            CreatePermission(
                "categories.delete",
                "Delete categories",
                PermissionModuleEnum.Categories,
                "Allows deleting categories."),

            // ----------------------------------------------------------
            // Rooms
            // ----------------------------------------------------------
            CreatePermission(
                "rooms.create",
                "Create rooms",
                PermissionModuleEnum.Rooms,
                "Allows creating rooms."),
            CreatePermission(
                "rooms.read",
                "Read rooms",
                PermissionModuleEnum.Rooms,
                "Allows retrieving a room by ID."),
            CreatePermission(
                "rooms.list",
                "List rooms",
                PermissionModuleEnum.Rooms,
                "Allows listing rooms."),
            CreatePermission(
                "rooms.update",
                "Update rooms",
                PermissionModuleEnum.Rooms,
                "Allows updating room information."),
            CreatePermission(
                "rooms.delete",
                "Delete rooms",
                PermissionModuleEnum.Rooms,
                "Allows deleting rooms."),

            // ----------------------------------------------------------
            // Room types
            // ----------------------------------------------------------
            CreatePermission(
                "room-types.create",
                "Create room types",
                PermissionModuleEnum.RoomTypes,
                "Allows creating room types."),
            CreatePermission(
                "room-types.read",
                "Read room types",
                PermissionModuleEnum.RoomTypes,
                "Allows retrieving a room type by ID."),
            CreatePermission(
                "room-types.list",
                "List room types",
                PermissionModuleEnum.RoomTypes,
                "Allows listing room types."),
            CreatePermission(
                "room-types.update",
                "Update room types",
                PermissionModuleEnum.RoomTypes,
                "Allows updating room type information."),
            CreatePermission(
                "room-types.delete",
                "Delete room types",
                PermissionModuleEnum.RoomTypes,
                "Allows deleting room types."),

            // ----------------------------------------------------------
            // Room blockouts
            // ----------------------------------------------------------
            CreatePermission(
                "rooms.blockouts.create",
                "Create room blockouts",
                PermissionModuleEnum.Rooms,
                "Allows blocking a room for a time range."),
            CreatePermission(
                "rooms.blockouts.read",
                "Read room blockouts",
                PermissionModuleEnum.Rooms,
                "Allows retrieving a room blockout by ID."),
            CreatePermission(
                "rooms.blockouts.list",
                "List room blockouts",
                PermissionModuleEnum.Rooms,
                "Allows listing room blockouts."),
            CreatePermission(
                "rooms.blockouts.update",
                "Update room blockouts",
                PermissionModuleEnum.Rooms,
                "Allows updating room blockout details."),
            CreatePermission(
                "rooms.blockouts.delete",
                "Delete room blockouts",
                PermissionModuleEnum.Rooms,
                "Allows removing room blockouts."),
            CreatePermission(
                "rooms.availability.read",
                "Read room availability",
                PermissionModuleEnum.Rooms,
                "Allows checking room availability for a time range."),

            // ----------------------------------------------------------
            // Reservations
            // A reservation should normally be cancelled rather than deleted,
            // so the history of the reservation can be preserved.
            // ----------------------------------------------------------
            CreatePermission(
                "reservations.create",
                "Create reservations",
                PermissionModuleEnum.Reservations,
                "Allows creating reservations."),
            CreatePermission(
                "reservations.read",
                "Read reservations",
                PermissionModuleEnum.Reservations,
                "Allows retrieving a reservation by ID."),
            CreatePermission(
                "reservations.list",
                "List reservations",
                PermissionModuleEnum.Reservations,
                "Allows listing reservations with filters and pagination."),
            CreatePermission(
                "reservations.update",
                "Update reservations",
                PermissionModuleEnum.Reservations,
                "Allows updating reservation details."),
            CreatePermission(
                "reservations.cancel",
                "Cancel reservations",
                PermissionModuleEnum.Reservations,
                "Allows cancelling reservations."),

            // ----------------------------------------------------------
            // Reservation attendees
            // ----------------------------------------------------------
            CreatePermission(
                "reservations.attendees.create",
                "Add reservation attendees",
                PermissionModuleEnum.Reservations,
                "Allows adding attendees to a reservation."),
            CreatePermission(
                "reservations.attendees.read",
                "Read reservation attendees",
                PermissionModuleEnum.Reservations,
                "Allows retrieving a reservation attendee by ID."),
            CreatePermission(
                "reservations.attendees.list",
                "List reservation attendees",
                PermissionModuleEnum.Reservations,
                "Allows listing attendees of a reservation."),
            CreatePermission(
                "reservations.attendees.update",
                "Update reservation attendees",
                PermissionModuleEnum.Reservations,
                "Allows updating attendee information and status."),
            CreatePermission(
                "reservations.attendees.delete",
                "Remove reservation attendees",
                PermissionModuleEnum.Reservations,
                "Allows removing attendees from a reservation."),
            CreatePermission(
                "reservations.attendees.respond",
                "Respond to reservation invitations",
                PermissionModuleEnum.Reservations,
                "Allows an attendee to accept or decline an invitation."),

            // ----------------------------------------------------------
            // Reservation history (read-only audit data)
            // ----------------------------------------------------------
            CreatePermission(
                "reservations.history.read",
                "Read reservation history",
                PermissionModuleEnum.Reservations,
                "Allows retrieving the history of a reservation."),
            CreatePermission(
                "reservations.history.list",
                "List reservation history",
                PermissionModuleEnum.Reservations,
                "Allows listing reservation history entries.")
        ]);

    private static PermissionsModel CreatePermission(
        string code,
        string name,
        PermissionModuleEnum module,
        string description)
    {
        return new PermissionsModel
        {
            Id = Guid.NewGuid().ToString(),
            Code = code,
            Name = name,
            Module = module,
            IsSystem = true,
            Description = description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task Execute(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await uow.ExecuteTransactionAsync(
            async ct =>
            {
                List<PermissionsModel> permissionsToAdd = [];

                foreach (PermissionsModel permission in Permissions)
                {
                    ct.ThrowIfCancellationRequested();

                    bool exists = await uow.PermissionRepository
                        .ExistsByCodeAsync(permission.Code, ct);

                    if (!exists)
                    {
                        permissionsToAdd.Add(permission);
                    }
                }

                if (permissionsToAdd.Count == 0)
                {
                    return;
                }

                await uow.PermissionRepository.AddAllAsync(permissionsToAdd, ct);
            },
            cancellationToken);
    }
}
