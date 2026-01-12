using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressFieldsToPro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Pros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Pros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HouseNameNumber",
                table: "Pros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Pros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street1",
                table: "Pros",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street2",
                table: "Pros",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipPostalCode",
                table: "Pros",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Pros");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Pros");

            migrationBuilder.DropColumn(
                name: "HouseNameNumber",
                table: "Pros");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Pros");

            migrationBuilder.DropColumn(
                name: "Street1",
                table: "Pros");

            migrationBuilder.DropColumn(
                name: "Street2",
                table: "Pros");

            migrationBuilder.DropColumn(
                name: "ZipPostalCode",
                table: "Pros");
        }
    }
}
