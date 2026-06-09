using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    public partial class SeedCommissionDefaults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = DateTime.UtcNow;
            var defaults = new[]
            {
                ("commission.user_percent", "10"),
                ("commission.pro_percent",  "10"),
                ("commission.gst_percent",  "18"),
                ("commission.min_fee",      "10"),
                ("commission.max_percent",  "20"),
            };

            foreach (var (key, value) in defaults)
            {
                // Only insert if not already present so re-runs are safe
                migrationBuilder.Sql($"""
                    IF NOT EXISTS (SELECT 1 FROM AppSettings WHERE [Key] = '{key}')
                        INSERT INTO AppSettings ([Key], [Value], UpdatedAt) VALUES ('{key}', '{value}', GETUTCDATE())
                    """);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM AppSettings WHERE [Key] LIKE 'commission.%'");
        }
    }
}
