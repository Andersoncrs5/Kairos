using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kairos.Api.Config.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "rooms",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<decimal>(
                name: "area_square_meters",
                table: "rooms",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "rooms",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "rooms",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "floor",
                table: "rooms",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_accessible",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_reservable",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_rooms_code",
                table: "rooms",
                column: "code",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_rooms_area_square_meters",
                table: "rooms",
                sql: "area_square_meters IS NULL OR area_square_meters > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rooms_code",
                table: "rooms");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rooms_area_square_meters",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "area_square_meters",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "code",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "description",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "floor",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "is_accessible",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "is_reservable",
                table: "rooms");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "rooms",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
