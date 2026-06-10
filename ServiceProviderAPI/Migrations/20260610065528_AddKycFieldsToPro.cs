using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddKycFieldsToPro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AadhaarDocumentPath",
                table: "Pros",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanDocumentPath",
                table: "Pros",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycStatus",
                table: "Pros",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTime>(
                name: "KycSubmittedAt",
                table: "Pros",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AadhaarDocumentPath", table: "Pros");
            migrationBuilder.DropColumn(name: "PanDocumentPath", table: "Pros");
            migrationBuilder.DropColumn(name: "KycStatus", table: "Pros");
            migrationBuilder.DropColumn(name: "KycSubmittedAt", table: "Pros");
        }
    }
}
