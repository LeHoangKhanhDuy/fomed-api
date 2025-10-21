using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoMed_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_VisitDate_VisitTime",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "UX_App_Doctor_Date_Queue",
                table: "Appointments",
                columns: new[] { "DoctorId", "VisitDate", "QueueNo" },
                unique: true,
                filter: "[QueueNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_App_Doctor_Date_Time",
                table: "Appointments",
                columns: new[] { "DoctorId", "VisitDate", "VisitTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_App_Doctor_Date_Queue",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "UX_App_Doctor_Date_Time",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_VisitDate_VisitTime",
                table: "Appointments",
                columns: new[] { "DoctorId", "VisitDate", "VisitTime" },
                unique: true,
                filter: "[Status] IN ('waiting','booked','done')");
        }
    }
}
