using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendee.Models;
using Kairos.Api.Modules.Reservations.ReservationHistory.Models;
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

    public DbSet<ReservationHistoryModel> ReservationHistory =>
        Set<ReservationHistoryModel>();

    public DbSet<ReservationAttendeeModel> ReservationAttendees =>
        Set<ReservationAttendeeModel>();
    
     public override int SaveChanges()
    {
        SetAuditDates();
        return base.SaveChanges();
    }
    
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditDates();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditDates()
    {
        var entries = ChangeTracker.Entries<BaseModel>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else if (entry.State == EntityState.Added)
            {
                 entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                 entry.Entity.Version++;
            }
        }
    }
 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        
        modelBuilder.Entity<UserModel>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(250).IsRequired(false);
        });

        modelBuilder.Entity<UserModel>().ToTable("app_users");
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("app_user_claims");
        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("app_user_roles");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("app_user_logins");
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("app_role_claims");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("app_user_tokens");
    }
}