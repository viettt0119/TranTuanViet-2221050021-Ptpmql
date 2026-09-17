using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations
{
    /// <inheritdoc />
    public partial class RemovePhoneNumberFromEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Employees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Employees",
                type: "TEXT",
                nullable: true);
        }
    }
}
