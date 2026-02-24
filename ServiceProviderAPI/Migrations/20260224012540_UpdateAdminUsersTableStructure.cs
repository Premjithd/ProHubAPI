using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminUsersTableStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing foreign keys to AdminUsers if they exist
            migrationBuilder.DropForeignKey(
                name: "FK_AdminUsers_Pros_ProId",
                table: "AdminUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AdminUsers_Users_UserId",
                table: "AdminUsers");

            // Drop the old primary key and any indexes
            migrationBuilder.DropPrimaryKey(
                name: "PK_AdminUsers",
                table: "AdminUsers");

            // Drop old columns that don't match AdminUser model
            // Keep only: Id, ProId, UserId, UpdatedAt, CreatedAt
            migrationBuilder.DropColumn(
                name: "ProName",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "BusinessName",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AdminUsers");

            // Add new columns required by AdminUser model
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "AdminUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "AdminUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "AdminUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "AdminUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "AdminUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                table: "AdminUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneVerified",
                table: "AdminUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Add back the primary key on Id column
            migrationBuilder.AddPrimaryKey(
                name: "PK_AdminUsers",
                table: "AdminUsers",
                column: "Id");

            // Add back the foreign keys with proper naming
            migrationBuilder.AddForeignKey(
                name: "FK_AdminUsers_Pros_ProId",
                table: "AdminUsers",
                column: "ProId",
                principalTable: "Pros",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AdminUsers_Users_UserId",
                table: "AdminUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_AdminUsers_Pros_ProId",
                table: "AdminUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AdminUsers_Users_UserId",
                table: "AdminUsers");

            // Drop primary key
            migrationBuilder.DropPrimaryKey(
                name: "PK_AdminUsers",
                table: "AdminUsers");

            // Drop new columns
            migrationBuilder.DropColumn(
                name: "Email",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "IsPhoneVerified",
                table: "AdminUsers");

            // Re-add old columns
            migrationBuilder.AddColumn<string>(
                name: "ProName",
                table: "AdminUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessName",
                table: "AdminUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AdminUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // Re-create the primary key
            migrationBuilder.AddPrimaryKey(
                name: "PK_AdminUsers",
                table: "AdminUsers",
                column: "Id");

            // Re-create foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_AdminUsers_Pros_ProId",
                table: "AdminUsers",
                column: "ProId",
                principalTable: "Pros",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AdminUsers_Users_UserId",
                table: "AdminUsers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);        }
    }
}