using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTypeAndApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountType",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberCode",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_MemberCode",
                table: "AspNetUsers",
                column: "MemberCode",
                unique: true);

            // Tài khoản Admin có sẵn đăng nhập bằng tư cách "Giảng viên / Quản trị".
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers" SET "AccountType" = 1
                WHERE "Id" IN (
                    SELECT ur."UserId" FROM "AspNetUserRoles" AS ur
                    INNER JOIN "AspNetRoles" AS r ON r."Id" = ur."RoleId"
                    WHERE r."NormalizedName" = 'ADMIN');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_MemberCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MemberCode",
                table: "AspNetUsers");
        }
    }
}
