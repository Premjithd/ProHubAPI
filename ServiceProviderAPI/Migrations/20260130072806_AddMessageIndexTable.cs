using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageIndexTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MessageIndexId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MessageIndexes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId1 = table.Column<int>(type: "int", nullable: false),
                    UserType1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId2 = table.Column<int>(type: "int", nullable: false),
                    UserType2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageIndexes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_MessageIndexId",
                table: "Messages",
                column: "MessageIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageIndex_UserPair",
                table: "MessageIndexes",
                columns: new[] { "UserId1", "UserType1", "UserId2", "UserType2" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_MessageIndexes_MessageIndexId",
                table: "Messages",
                column: "MessageIndexId",
                principalTable: "MessageIndexes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_MessageIndexes_MessageIndexId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "MessageIndexes");

            migrationBuilder.DropIndex(
                name: "IX_Messages_MessageIndexId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MessageIndexId",
                table: "Messages");
        }
    }
}
