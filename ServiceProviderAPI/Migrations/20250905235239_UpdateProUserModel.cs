using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProUserModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ProUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ProUsers");
        }
    }
}
