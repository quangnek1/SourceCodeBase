using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AerationSterilize.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPermissionAccessLevelDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddDataScopeColumns filled existing rows with 0 (AccessLevel.None), which removes the permission.
            // Those rows were granted before data scope existed, so they mean full access (AccessLevel.All = 3).
            // An unchecked permission is stored as "no row", so no row legitimately holds 0.
            migrationBuilder.Sql("UPDATE [Permissions] SET [AccessLevel] = 3 WHERE [AccessLevel] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data fix only: the previous values cannot be told apart, so there is nothing to undo.
        }
    }
}
