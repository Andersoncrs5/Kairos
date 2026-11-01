using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Config.DB;

public class AppDbContext(DbContextOptions options)
    : IdentityDbContext<UserModel>(options)
{
    public DbSet<CategoryModel> Categories => Set<CategoryModel>();

    public DbSet<PermissionsModel> Permissions => Set<PermissionsModel>();

    public DbSet<PermissionUserModel> PermissionUsers =>
        Set<PermissionUserModel>();

    public DbSet<RoomModel> Rooms => Set<RoomModel>();

    public DbSet<RoomTypeModel> RoomTypes => Set<RoomTypeModel>();

    public DbSet<RoomBlockoutModel> RoomBlockouts =>
        Set<RoomBlockoutModel>();

    public DbSet<ReservationModel> Reservations =>
        Set<ReservationModel>();

    public DbSet<InboxModel> Inbox => Set<InboxModel>();

    // ---------------------------------------------------------------
    // SaveChanges
    // ---------------------------------------------------------------

    public override int SaveChanges()
        => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SetAuditDates();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
        => SaveChangesAsync(
            acceptAllChangesOnSuccess: true,
            cancellationToken);

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        SetAuditDates();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    // ---------------------------------------------------------------
    // Audit
    // ---------------------------------------------------------------

    private void SetAuditDates()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseModel>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.Version = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.Version++;
            }
        }

        foreach (var entry in ChangeTracker.Entries<UserModel>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.Version = 1;
                entry.Entity.UpdatedAt = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.Version++;
            }
        }
    }

    // ---------------------------------------------------------------
    // Model configuration
    // ---------------------------------------------------------------

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        modelBuilder.Entity<IdentityRole>()
            .ToTable("app_roles");

        modelBuilder.Entity<IdentityUserClaim<string>>()
            .ToTable("app_user_claims");

        modelBuilder.Entity<IdentityUserRole<string>>()
            .ToTable("app_user_roles");

        modelBuilder.Entity<IdentityUserLogin<string>>()
            .ToTable("app_user_logins");

        modelBuilder.Entity<IdentityRoleClaim<string>>()
            .ToTable("app_role_claims");

        modelBuilder.Entity<IdentityUserToken<string>>()
            .ToTable("app_user_tokens");
    }
}