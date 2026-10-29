using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kairos.Api.Config.DB.Migrations
{
    /// <inheritdoc />
    public partial class SyncPermissionUserModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PermissionsId",
                table: "permissions_users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId1",
                table: "permissions_users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_users_PermissionsId",
                table: "permissions_users",
                column: "PermissionsId");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_users_UserId1",
                table: "permissions_users",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_permissions_users_app_users_UserId1",
                table: "permissions_users",
                column: "UserId1",
                principalTable: "app_users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_permissions_users_permissions_PermissionsId",
                table: "permissions_users",
                column: "PermissionsId",
                principalTable: "permissions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_permissions_users_app_users_UserId1",
                table: "permissions_users");

            migrationBuilder.DropForeignKey(
                name: "FK_permissions_users_permissions_PermissionsId",
                table: "permissions_users");

            migrationBuilder.DropIndex(
                name: "IX_permissions_users_PermissionsId",
                table: "permissions_users");

            migrationBuilder.DropIndex(
                name: "IX_permissions_users_UserId1",
                table: "permissions_users");

            migrationBuilder.DropColumn(
                name: "PermissionsId",
                table: "permissions_users");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "permissions_users");
        }
    }
}
