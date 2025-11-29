using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoMed_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddDateOfBirthGenderToUserProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm cột DateOfBirth
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "UserProfiles",
                type: "date",
                nullable: true);

            // Thêm cột Gender
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "UserProfiles",
                type: "char(1)",
                nullable: true);

            // Thêm check constraint cho Gender
            migrationBuilder.Sql(
                @"ALTER TABLE [UserProfiles] 
                  ADD CONSTRAINT [CK_UserProfiles_Gender] 
                  CHECK ([Gender] IN ('M','F') OR [Gender] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Xóa check constraint
            migrationBuilder.Sql("ALTER TABLE [UserProfiles] DROP CONSTRAINT [CK_UserProfiles_Gender]");

            // Xóa các cột
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "UserProfiles");
        }
    }
}
