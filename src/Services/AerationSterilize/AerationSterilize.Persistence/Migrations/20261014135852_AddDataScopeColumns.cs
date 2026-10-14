using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AerationSterilize.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataScopeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Product",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Product",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccessLevel",
                table: "Permissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "AppUsers",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "AppUsers");
        }
    }
}
