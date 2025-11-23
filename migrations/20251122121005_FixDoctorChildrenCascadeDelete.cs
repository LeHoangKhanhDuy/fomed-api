using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoMed_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class FixDoctorChildrenCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_DoctorWeeklySlots_Doctors_DoctorId",
                table: "DoctorWeeklySlots");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorEducations_Doctors_DoctorId",
                table: "DoctorEducations");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorExpertises_Doctors_DoctorId",
                table: "DoctorExpertises");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorAchievements_Doctors_DoctorId",
                table: "DoctorAchievements");

            // Add new foreign keys with CASCADE DELETE
            migrationBuilder.AddForeignKey(
                name: "FK_DoctorWeeklySlots_Doctors_DoctorId",
                table: "DoctorWeeklySlots",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorEducations_Doctors_DoctorId",
                table: "DoctorEducations",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorExpertises_Doctors_DoctorId",
                table: "DoctorExpertises",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorAchievements_Doctors_DoctorId",
                table: "DoctorAchievements",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to old foreign keys without CASCADE
            migrationBuilder.DropForeignKey(
                name: "FK_DoctorWeeklySlots_Doctors_DoctorId",
                table: "DoctorWeeklySlots");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorEducations_Doctors_DoctorId",
                table: "DoctorEducations");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorExpertises_Doctors_DoctorId",
                table: "DoctorExpertises");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorAchievements_Doctors_DoctorId",
                table: "DoctorAchievements");

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorWeeklySlots_Doctors_DoctorId",
                table: "DoctorWeeklySlots",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorEducations_Doctors_DoctorId",
                table: "DoctorEducations",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorExpertises_Doctors_DoctorId",
                table: "DoctorExpertises",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorAchievements_Doctors_DoctorId",
                table: "DoctorAchievements",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId");
        }
    }
}
