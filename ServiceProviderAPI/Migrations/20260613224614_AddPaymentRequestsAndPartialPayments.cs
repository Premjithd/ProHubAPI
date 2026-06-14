using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRequestsAndPartialPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrincipalAmount",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PaymentRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<int>(type: "int", nullable: false),
                    BidId = table.Column<int>(type: "int", nullable: true),
                    ProId = table.Column<int>(type: "int", nullable: false),
                    RequestType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FulfilledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRequests_JobBids_BidId",
                        column: x => x.BidId,
                        principalTable: "JobBids",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentRequests_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentRequests_Pros_ProId",
                        column: x => x.ProId,
                        principalTable: "Pros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_BidId",
                table: "PaymentRequests",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_JobId",
                table: "PaymentRequests",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_ProId",
                table: "PaymentRequests",
                column: "ProId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "PrincipalAmount",
                table: "Payments");
        }
    }
}
