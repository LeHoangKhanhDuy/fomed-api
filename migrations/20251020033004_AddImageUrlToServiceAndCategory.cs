using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoMed_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUrlToServiceAndCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LabTests_Code",
                table: "LabTests");

            migrationBuilder.DropIndex(
                name: "IX_LabResults_EncLabTestId",
                table: "LabResults");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Services",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "ServiceCategories",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "Patients",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllergyText",
                table: "Patients",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientCode",
                table: "Patients",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "LabTests",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "LabTests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "LabTests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ResultStatus",
                table: "LabResults",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "normal",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ResultAt",
                table: "LabResults",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Encounters",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "EncounterPrescriptions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErxCode",
                table: "EncounterPrescriptions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErxStatus",
                table: "EncounterPrescriptions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryAt",
                table: "EncounterPrescriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Warning",
                table: "EncounterPrescriptions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "EncounterLabTests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ordered",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "EncounterLabTests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "LicenseNo",
                table: "Doctors",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_Employees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabOrders",
                columns: table => new
                {
                    LabOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: true),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    SampleTakenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResultAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SampleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "processing"),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Warning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrders", x => x.LabOrderId);
                    table.ForeignKey(
                        name: "FK_LabOrders_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "DoctorId");
                    table.ForeignKey(
                        name: "FK_LabOrders_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabOrders_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabOrderItems",
                columns: table => new
                {
                    LabOrderItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabOrderId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    TestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResultValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceMin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReferenceMax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReferenceText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsHigh = table.Column<bool>(type: "bit", nullable: false),
                    IsLow = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrderItems", x => x.LabOrderItemId);
                    table.ForeignKey(
                        name: "FK_LabOrderItems_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "LabOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PatientCode",
                table: "Patients",
                column: "PatientCode",
                unique: true,
                filter: "[PatientCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_Code",
                table: "LabTests",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_EncLabTestId_ResultAt",
                table: "LabResults",
                columns: new[] { "EncLabTestId", "ResultAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderItems_Order",
                table: "LabOrderItems",
                columns: new[] { "LabOrderId", "DisplayOrder", "LabOrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_Code",
                table: "LabOrders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_DoctorId",
                table: "LabOrders",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_PatientId",
                table: "LabOrders",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_ServiceId",
                table: "LabOrders",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "LabOrderItems");

            migrationBuilder.DropTable(
                name: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_Patients_PatientCode",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_LabTests_Code",
                table: "LabTests");

            migrationBuilder.DropIndex(
                name: "IX_LabResults_EncLabTestId_ResultAt",
                table: "LabResults");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "ServiceCategories");

            migrationBuilder.DropColumn(
                name: "AllergyText",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PatientCode",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "EncounterPrescriptions");

            migrationBuilder.DropColumn(
                name: "ErxCode",
                table: "EncounterPrescriptions");

            migrationBuilder.DropColumn(
                name: "ErxStatus",
                table: "EncounterPrescriptions");

            migrationBuilder.DropColumn(
                name: "ExpiryAt",
                table: "EncounterPrescriptions");

            migrationBuilder.DropColumn(
                name: "Warning",
                table: "EncounterPrescriptions");

            migrationBuilder.DropColumn(
                name: "LicenseNo",
                table: "Doctors");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Patients",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "LabTests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "LabTests",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "LabTests",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ResultStatus",
                table: "LabResults",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "normal");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ResultAt",
                table: "LabResults",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "EncounterLabTests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "ordered");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "EncounterLabTests",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_Code",
                table: "LabTests",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_EncLabTestId",
                table: "LabResults",
                column: "EncLabTestId");
        }
    }
}
