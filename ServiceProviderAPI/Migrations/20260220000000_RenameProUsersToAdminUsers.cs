using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class RenameProUsersToAdminUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the ProUsers table to AdminUsers
            migrationBuilder.RenameTable(
                name: "ProUsers",
                newName: "AdminUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rename the AdminUsers table back to ProUsers
            migrationBuilder.RenameTable(
                name: "AdminUsers",
                newName: "ProUsers");
        }
    }
}
