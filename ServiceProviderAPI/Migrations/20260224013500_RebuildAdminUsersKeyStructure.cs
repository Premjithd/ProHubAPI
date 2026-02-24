using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceProviderAPI.Migrations
{
    /// <inheritdoc />
    public partial class RebuildAdminUsersKeyStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration documents the manual schema changes made to AdminUsers table:
            // 1. Dropped composite primary key (ProId, UserId)
            // 2. Made ProId and UserId nullable
            // 3. Added Id column as new primary key with IDENTITY
            // 4. Re-created foreign key constraints with SET NULL on delete
            
            // Note: These changes were applied directly to the database via SQL
            // This migration ensures EF Core is aware of the new schema structure
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback would require reversing the above changes
            // Not typically needed for production databases
        }
    }
}
