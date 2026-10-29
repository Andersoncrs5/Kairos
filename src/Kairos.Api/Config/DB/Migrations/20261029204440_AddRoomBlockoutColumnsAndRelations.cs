using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kairos.Api.Config.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomBlockoutColumnsAndRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "room_blockouts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "room_blockouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelled_by",
                table: "room_blockouts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "room_blockouts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "room_blockouts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "room_blockouts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_room_blockouts_cancelled_by",
                table: "room_blockouts",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "IX_room_blockouts_room_id_status_starts_at",
                table: "room_blockouts",
                columns: new[] { "room_id", "status", "starts_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_room_blockouts_app_users_cancelled_by",
                table: "room_blockouts",
                column: "cancelled_by",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_room_blockouts_app_users_cancelled_by",
                table: "room_blockouts");

            migrationBuilder.DropIndex(
                name: "IX_room_blockouts_cancelled_by",
                table: "room_blockouts");

            migrationBuilder.DropIndex(
                name: "IX_room_blockouts_room_id_status_starts_at",
                table: "room_blockouts");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "room_blockouts");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "room_blockouts");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                table: "room_blockouts");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "room_blockouts");

            migrationBuilder.DropColumn(
                name: "status",
                table: "room_blockouts");

            migrationBuilder.DropColumn(
                name: "type",
                table: "room_blockouts");
        }
    }
}
