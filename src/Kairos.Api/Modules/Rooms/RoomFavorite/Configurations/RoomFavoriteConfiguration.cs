using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Configurations;

public sealed class RoomFavoriteConfiguration
    : IEntityTypeConfiguration<RoomFavoriteModel>
{
    public void Configure(EntityTypeBuilder<RoomFavoriteModel> builder)
    {
        builder.ToTable("room_favorites");

        // Primary key
        builder.HasKey(favorite => favorite.Id);

        builder.Property(favorite => favorite.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // BaseModel
        builder.Property(favorite => favorite.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(favorite => favorite.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(favorite => favorite.UpdatedAt)
            .HasColumnName("updated_at");

        // User (o favorito não faz sentido sem o usuário)
        builder.Property(favorite => favorite.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(favorite => favorite.User)
            .WithMany()
            .HasForeignKey(favorite => favorite.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Room (o favorito não deve impedir a exclusão de uma sala)
        builder.Property(favorite => favorite.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.HasOne(favorite => favorite.Room)
            .WithMany()
            .HasForeignKey(favorite => favorite.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // Um usuário só favorita a mesma sala uma vez.
        // O índice também atende "minhas salas favoritas" (começa por user_id).
        builder.HasIndex(favorite => new
            {
                favorite.UserId,
                favorite.RoomId
            })
            .IsUnique()
            .HasDatabaseName("ux_room_favorites_user_room");

        // "Quantas pessoas favoritaram esta sala" e FK para a sala
        builder.HasIndex(favorite => favorite.RoomId)
            .HasDatabaseName("ix_room_favorites_room_id");
    }
}