using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessRadiusAndTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceRadiusKm",
                table: "Businesses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    Initials = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "ServiceRadiusKm",
                table: "Businesses");
        }
    }
}
