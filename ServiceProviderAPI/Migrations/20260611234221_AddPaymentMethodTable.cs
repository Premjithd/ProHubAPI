using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create PaymentMethods table first
            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ProId = table.Column<int>(type: "int", nullable: true),
                    BusinessId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    UpiVpa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankAccountHolderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankIfsc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RazorpayContactId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RazorpayFundAccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentMethods_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentMethods_Pros_ProId",
                        column: x => x.ProId,
                        principalTable: "Pros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentMethods_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_BusinessId",
                table: "PaymentMethods",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_ProId",
                table: "PaymentMethods",
                column: "ProId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_UserId",
                table: "PaymentMethods",
                column: "UserId");

            // 2. Migrate existing User.UpiVpa → PaymentMethods
            migrationBuilder.Sql(@"
                INSERT INTO PaymentMethods (UserId, Type, Label, IsDefault, UpiVpa, CreatedAt)
                SELECT Id, 'UPI', 'UPI', 1, UpiVpa, GETUTCDATE()
                FROM Users
                WHERE UpiVpa IS NOT NULL AND LTRIM(RTRIM(UpiVpa)) <> ''
            ");

            // 3. Migrate existing Pro payout fields → PaymentMethods
            migrationBuilder.Sql(@"
                INSERT INTO PaymentMethods (
                    ProId, Type, Label, IsDefault,
                    UpiVpa, BankAccountHolderName, BankAccountNumber, BankIfsc,
                    RazorpayContactId, RazorpayFundAccountId, CreatedAt)
                SELECT
                    Id,
                    ISNULL(PayoutMethod, 'UPI'),
                    CASE ISNULL(PayoutMethod, 'UPI') WHEN 'UPI' THEN 'UPI' ELSE 'Bank Account' END,
                    1,
                    UpiVpa,
                    BankAccountHolderName,
                    BankAccountNumber,
                    BankIfsc,
                    RazorpayContactId,
                    RazorpayFundAccountId,
                    GETUTCDATE()
                FROM Pros
                WHERE UpiVpa IS NOT NULL
                   OR BankAccountNumber IS NOT NULL
            ");

            // 4. Drop old columns now that data is migrated
            migrationBuilder.DropColumn(name: "UpiVpa", table: "Users");

            migrationBuilder.DropColumn(name: "BankAccountHolderName", table: "Pros");
            migrationBuilder.DropColumn(name: "BankAccountNumber",      table: "Pros");
            migrationBuilder.DropColumn(name: "BankIfsc",               table: "Pros");
            migrationBuilder.DropColumn(name: "PayoutMethod",           table: "Pros");
            migrationBuilder.DropColumn(name: "RazorpayContactId",      table: "Pros");
            migrationBuilder.DropColumn(name: "RazorpayFundAccountId",  table: "Pros");
            migrationBuilder.DropColumn(name: "UpiVpa",                 table: "Pros");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore old columns
            migrationBuilder.AddColumn<string>(
                name: "UpiVpa", table: "Users",
                type: "nvarchar(100)", maxLength: 100, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountHolderName", table: "Pros",
                type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber", table: "Pros",
                type: "nvarchar(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "BankIfsc", table: "Pros",
                type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "PayoutMethod", table: "Pros",
                type: "nvarchar(10)", maxLength: 10, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "RazorpayContactId", table: "Pros",
                type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "RazorpayFundAccountId", table: "Pros",
                type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "UpiVpa", table: "Pros",
                type: "nvarchar(100)", maxLength: 100, nullable: true);

            // Restore User.UpiVpa from PaymentMethods (best-effort, takes the default UPI method)
            migrationBuilder.Sql(@"
                UPDATE u SET u.UpiVpa = pm.UpiVpa
                FROM Users u
                JOIN PaymentMethods pm ON pm.UserId = u.Id AND pm.Type = 'UPI' AND pm.IsDefault = 1
                WHERE pm.UpiVpa IS NOT NULL
            ");

            // Restore Pro payout fields from PaymentMethods
            migrationBuilder.Sql(@"
                UPDATE p SET
                    p.PayoutMethod = pm.Type,
                    p.UpiVpa = pm.UpiVpa,
                    p.BankAccountHolderName = pm.BankAccountHolderName,
                    p.BankAccountNumber = pm.BankAccountNumber,
                    p.BankIfsc = pm.BankIfsc,
                    p.RazorpayContactId = pm.RazorpayContactId,
                    p.RazorpayFundAccountId = pm.RazorpayFundAccountId
                FROM Pros p
                JOIN PaymentMethods pm ON pm.ProId = p.Id AND pm.IsDefault = 1
            ");

            migrationBuilder.DropTable(name: "PaymentMethods");
        }
    }
}
