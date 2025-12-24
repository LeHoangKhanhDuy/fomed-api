using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoMed_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCategoryTypeAndServiceLabTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoryType",
                table: "ServiceCategories",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "visit");

            migrationBuilder.CreateTable(
                name: "ServiceLabTests",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    LabTestId = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLabTests", x => new { x.ServiceId, x.LabTestId });
                    table.ForeignKey(
                        name: "FK_ServiceLabTests_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "LabTestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceLabTests_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceCategories_Type",
                table: "ServiceCategories",
                sql: "CategoryType IN ('visit','lab','vaccine')");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLabTests_LabTestId",
                table: "ServiceLabTests",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLabTests_Order",
                table: "ServiceLabTests",
                columns: new[] { "ServiceId", "DisplayOrder", "LabTestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceLabTests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceCategories_Type",
                table: "ServiceCategories");

            migrationBuilder.DropColumn(
                name: "CategoryType",
                table: "ServiceCategories");
        }
    }
}
