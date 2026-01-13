using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCategoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ServiceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCategories", x => x.Id);
                });

            // Seed initial categories
            migrationBuilder.InsertData(
                table: "ServiceCategories",
                columns: new[] { "Name", "Icon", "ServiceCount", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { "Cleaning", "🧹", 234, true, new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { "Plumbing", "🔧", 189, true, new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { "Electrical", "⚡", 156, true, new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { "Painting", "🎨", 201, true, new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { "Landscaping", "🌿", 178, true, new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { "Handyman", "🔨", 267, true, new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceCategories");
        }
    }
}
