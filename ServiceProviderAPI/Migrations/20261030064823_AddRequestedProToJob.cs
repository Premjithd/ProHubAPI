using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestedProToJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequestedProId",
                table: "Jobs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_RequestedProId",
                table: "Jobs",
                column: "RequestedProId");

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_Pros_RequestedProId",
                table: "Jobs",
                column: "RequestedProId",
                principalTable: "Pros",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_Pros_RequestedProId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_RequestedProId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "RequestedProId",
                table: "Jobs");
        }
    }
}
