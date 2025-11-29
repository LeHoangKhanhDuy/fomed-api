using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoMed_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class FixPatientCodeStored : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PatientCode",
                table: "Patients",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                computedColumnSql: "'BN' + RIGHT('0000' + CAST([PatientId] AS VARCHAR(4)), 4)",
                stored: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true,
                oldComputedColumnSql: "'BN' + RIGHT('0000' + CAST([PatientId] AS VARCHAR(4)), 4)",
                oldStored: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PatientCode",
                table: "Patients",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                computedColumnSql: "'BN' + RIGHT('0000' + CAST([PatientId] AS VARCHAR(4)), 4)",
                stored: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true,
                oldComputedColumnSql: "'BN' + RIGHT('0000' + CAST([PatientId] AS VARCHAR(4)), 4)",
                oldStored: true);
        }
    }
}
