using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kairos.Api.Config.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationRelationsAndColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_reservations_user_id_idempotency_key",
                table: "reservations",
                newName: "ux_reservations_user_idempotency_key");

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelled_by",
                table: "reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_in_at",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_out_at",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "reservations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservations_cancelled_by",
                table: "reservations",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_category_id",
                table: "reservations",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_room_period",
                table: "reservations",
                columns: new[] { "room_id", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_user_starts_at",
                table: "reservations",
                columns: new[] { "user_id", "starts_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_reservations_app_users_cancelled_by",
                table: "reservations",
                column: "cancelled_by",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_reservations_app_users_user_id",
                table: "reservations",
                column: "user_id",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_reservations_categories_category_id",
                table: "reservations",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_reservations_rooms_room_id",
                table: "reservations",
                column: "room_id",
                principalTable: "rooms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reservations_app_users_cancelled_by",
                table: "reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_reservations_app_users_user_id",
                table: "reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_reservations_categories_category_id",
                table: "reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_reservations_rooms_room_id",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_cancelled_by",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_category_id",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservations_room_period",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservations_user_starts_at",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "checked_in_at",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "checked_out_at",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "description",
                table: "reservations");

            migrationBuilder.RenameIndex(
                name: "ux_reservations_user_idempotency_key",
                table: "reservations",
                newName: "IX_reservations_user_id_idempotency_key");
        }
    }
}
