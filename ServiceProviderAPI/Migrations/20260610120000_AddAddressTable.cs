using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    public partial class AddAddressTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AddressType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    HouseNameNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Street1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Street2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ZipPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "Pros",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceAddressId",
                table: "Jobs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AddressId",
                table: "Users",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Pros_AddressId",
                table: "Pros",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ServiceAddressId",
                table: "Jobs",
                column: "ServiceAddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Addresses_AddressId",
                table: "Users",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pros_Addresses_AddressId",
                table: "Pros",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_Addresses_ServiceAddressId",
                table: "Jobs",
                column: "ServiceAddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Drop old inline address columns from Users
            migrationBuilder.DropColumn(name: "City",            table: "Users");
            migrationBuilder.DropColumn(name: "Country",         table: "Users");
            migrationBuilder.DropColumn(name: "District",        table: "Users");
            migrationBuilder.DropColumn(name: "HouseNameNumber", table: "Users");
            migrationBuilder.DropColumn(name: "State",           table: "Users");
            migrationBuilder.DropColumn(name: "Street1",         table: "Users");
            migrationBuilder.DropColumn(name: "Street2",         table: "Users");
            migrationBuilder.DropColumn(name: "ZipPostalCode",   table: "Users");
            migrationBuilder.DropColumn(name: "Latitude",        table: "Users");
            migrationBuilder.DropColumn(name: "Longitude",       table: "Users");

            // Drop old inline address columns from Pros
            migrationBuilder.DropColumn(name: "City",            table: "Pros");
            migrationBuilder.DropColumn(name: "Country",         table: "Pros");
            migrationBuilder.DropColumn(name: "District",        table: "Pros");
            migrationBuilder.DropColumn(name: "HouseNameNumber", table: "Pros");
            migrationBuilder.DropColumn(name: "State",           table: "Pros");
            migrationBuilder.DropColumn(name: "Street1",         table: "Pros");
            migrationBuilder.DropColumn(name: "Street2",         table: "Pros");
            migrationBuilder.DropColumn(name: "ZipPostalCode",   table: "Pros");
            migrationBuilder.DropColumn(name: "Latitude",        table: "Pros");
            migrationBuilder.DropColumn(name: "Longitude",       table: "Pros");

            // Drop old inline service address columns from Jobs
            migrationBuilder.DropColumn(name: "ServiceAddressCity",     table: "Jobs");
            migrationBuilder.DropColumn(name: "ServiceAddressCountry",  table: "Jobs");
            migrationBuilder.DropColumn(name: "ServiceAddressDistrict", table: "Jobs");
            migrationBuilder.DropColumn(name: "ServiceAddressHouse",    table: "Jobs");
            migrationBuilder.DropColumn(name: "ServiceAddressPIN",      table: "Jobs");
            migrationBuilder.DropColumn(name: "ServiceAddressState",    table: "Jobs");
            migrationBuilder.DropColumn(name: "ServiceAddressStreet1",  table: "Jobs");
            migrationBuilder.DropColumn(name: "ServiceAddressStreet2",  table: "Jobs");
            migrationBuilder.DropColumn(name: "Latitude",               table: "Jobs");
            migrationBuilder.DropColumn(name: "Longitude",              table: "Jobs");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_Addresses_ServiceAddressId",
                table: "Jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_Pros_Addresses_AddressId",
                table: "Pros");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Addresses_AddressId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_ServiceAddressId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Pros_AddressId",
                table: "Pros");

            migrationBuilder.DropIndex(
                name: "IX_Users_AddressId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ServiceAddressId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "Pros");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Addresses");
        }
    }
}
