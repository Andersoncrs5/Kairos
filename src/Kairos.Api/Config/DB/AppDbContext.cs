using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
    
    public DbSet<EquipmentModel> Equipments => Set<EquipmentModel>();
    
    public DbSet<RoomEquipmentModel> RoomEquipments => Set<RoomEquipmentModel>();
    
    public DbSet<RoomOperatingHourModel> RoomOperatingHours => Set<RoomOperatingHourModel>();
    
    public DbSet<ReservationAttendeeModel> ReservationAttendees => Set<ReservationAttendeeModel>();
    
    public DbSet<RoomFavoriteModel> RoomFavorites => Set<RoomFavoriteModel>();
    
    public DbSet<RoomFeedbackModel> RoomFeedbacks => Set<RoomFeedbackModel>();

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

        // Configuração global para o controle de versão/concorrência otimista
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseModel).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(BaseModel.Version))
                    .IsConcurrencyToken();
            }
        }

        // Precisão padrão para propriedades decimais
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }

        // Comportamento global de exclusão restrita (pode ser sobrescrito individualmente se necessário)
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // Converte automaticamente nomes de tabelas, colunas e chaves para snake_case
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName(StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema()));
                if (!string.IsNullOrEmpty(columnName))
                {
                    property.SetColumnName(ToSnakeCase(columnName));
                }
            }

            foreach (var key in entity.GetKeys())
            {
                var keyName = key.GetName();
                if (!string.IsNullOrEmpty(keyName))
                {
                    key.SetName(ToSnakeCase(keyName));
                }
            }

            foreach (IMutableForeignKey foreignKey in entity.GetForeignKeys())
            {
                var fkName = foreignKey.GetConstraintName();
                if (!string.IsNullOrEmpty(fkName))
                {
                    foreignKey.SetConstraintName(ToSnakeCase(fkName));
                }
            }

            foreach (var index in entity.GetIndexes())
            {
                var indexName = index.GetDatabaseName();
                if (!string.IsNullOrEmpty(indexName))
                {
                    index.SetDatabaseName(ToSnakeCase(indexName));
                }
            }
        }

        // Configurações das tabelas do Identity
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

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) { return input; }

        return string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
    }
}