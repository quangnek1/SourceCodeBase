using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AerationSterilize.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPermissionAccessLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddDataScopeColumns added AccessLevel with default 0 (None), so every permission granted before it
            // silently lost its access. Those rows predate data scopes and meant full access.
            migrationBuilder.Sql("UPDATE [Permissions] SET [AccessLevel] = 3 WHERE [AccessLevel] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
