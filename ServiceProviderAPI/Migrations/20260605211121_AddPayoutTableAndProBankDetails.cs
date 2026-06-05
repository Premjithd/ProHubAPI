using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutTableAndProBankDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bank / payout fields on Pro
            migrationBuilder.AddColumn<string>(
                name: "BankAccountHolderName",
                table: "Pros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Pros",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankIfsc",
                table: "Pros",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutMethod",
                table: "Pros",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazorpayContactId",
                table: "Pros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazorpayFundAccountId",
                table: "Pros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpiVpa",
                table: "Pros",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Payouts table
            migrationBuilder.CreateTable(
                name: "Payouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProId = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    JobId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RazorpayPayoutId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RazorpayFundAccountId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payouts_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payouts_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payouts_Pros_ProId",
                        column: x => x.ProId,
                        principalTable: "Pros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_JobId",
                table: "Payouts",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_PaymentId",
                table: "Payouts",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_ProId",
                table: "Payouts",
                column: "ProId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Payouts");

            migrationBuilder.DropColumn(name: "BankAccountHolderName", table: "Pros");
            migrationBuilder.DropColumn(name: "BankAccountNumber",      table: "Pros");
            migrationBuilder.DropColumn(name: "BankIfsc",               table: "Pros");
            migrationBuilder.DropColumn(name: "PayoutMethod",           table: "Pros");
            migrationBuilder.DropColumn(name: "RazorpayContactId",      table: "Pros");
            migrationBuilder.DropColumn(name: "RazorpayFundAccountId",  table: "Pros");
            migrationBuilder.DropColumn(name: "UpiVpa",                 table: "Pros");
        }
    }
}
