using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations
{
    /// <inheritdoc />
    public partial class RefactorModulePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionDisplayName",
                table: "ApplicationPermissions",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "ApplicationPermissions",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "ApplicationPermissions",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModuleDisplayName",
                table: "ApplicationPermissions",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ApplicationPermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionDisplayName",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "Module",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "ModuleDisplayName",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ApplicationPermissions");
        }
    }
}
