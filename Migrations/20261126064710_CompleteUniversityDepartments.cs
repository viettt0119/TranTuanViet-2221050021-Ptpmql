using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations
{
    /// <summary>
    /// Bổ sung đủ các khoa của Trường Đại học Mỏ - Địa chất và sửa tên khoa seed cũ.
    /// Viết bằng SQL thay cho InsertData/UpdateData để chạy an toàn trên CSDL đang có dữ liệu:
    /// - khoa đã tồn tại (người dùng tự thêm hoặc nhập Excel) thì bỏ qua, không lỗi trùng khoá;
    /// - chỉ đổi tên khoa nào còn giữ đúng tên seed cũ, không ghi đè tên người dùng đã sửa.
    /// </summary>
    public partial class CompleteUniversityDepartments : Migration
    {
        private static readonly (string Code, string Name)[] NewDepartments =
        [
            ("CD", "Cơ - Điện"),
            ("DK", "Dầu khí và Năng lượng"),
            ("KHCB", "Khoa học cơ bản"),
            ("LLCT", "Lý luận chính trị"),
            ("MT", "Môi trường"),
            ("TD", "Trắc địa - Bản đồ và Quản lý đất đai")
        ];

        private static readonly (string Code, string OldName, string NewName)[] Renames =
        [
            ("DC", "Địa chất", "Khoa học và Kỹ thuật Địa chất"),
            ("KT", "Kinh tế", "Kinh tế - Quản trị kinh doanh"),
            ("MO", "Khoa học mỏ", "Mỏ")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (code, oldName, newName) in Renames)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"Departments\" SET \"DepartmentName\" = '{newName}' " +
                    $"WHERE \"DepartmentCode\" = '{code}' AND \"DepartmentName\" = '{oldName}';");
            }

            foreach (var (code, name) in NewDepartments)
            {
                migrationBuilder.Sql(
                    $"INSERT OR IGNORE INTO \"Departments\" (\"DepartmentCode\", \"DepartmentName\") VALUES ('{code}', '{name}');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (code, _) in NewDepartments)
            {
                migrationBuilder.Sql(
                    $"DELETE FROM \"Departments\" WHERE \"DepartmentCode\" = '{code}' " +
                    $"AND NOT EXISTS (SELECT 1 FROM \"Students\" WHERE \"DepartmentCode\" = '{code}');");
            }

            foreach (var (code, oldName, newName) in Renames)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"Departments\" SET \"DepartmentName\" = '{oldName}' " +
                    $"WHERE \"DepartmentCode\" = '{code}' AND \"DepartmentName\" = '{newName}';");
            }
        }
    }
}
