using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToJobNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make ProId nullable
            migrationBuilder.AlterColumn<int>(
                name: "ProId",
                table: "JobNotifications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Add UserId column
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "JobNotifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobNotifications_UserId",
                table: "JobNotifications",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobNotifications_Users_UserId",
                table: "JobNotifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobNotifications_Users_UserId",
                table: "JobNotifications");

            migrationBuilder.DropIndex(
                name: "IX_JobNotifications_UserId",
                table: "JobNotifications");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "JobNotifications");

            migrationBuilder.AlterColumn<int>(
                name: "ProId",
                table: "JobNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
