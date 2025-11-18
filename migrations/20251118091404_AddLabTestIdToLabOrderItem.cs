using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoMed_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddLabTestIdToLabOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LabTestId",
                table: "LabOrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderItems_LabTestId",
                table: "LabOrderItems",
                column: "LabTestId");

            migrationBuilder.AddForeignKey(
                name: "FK_LabOrderItems_LabTests_LabTestId",
                table: "LabOrderItems",
                column: "LabTestId",
                principalTable: "LabTests",
                principalColumn: "LabTestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabOrderItems_LabTests_LabTestId",
                table: "LabOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_LabOrderItems_LabTestId",
                table: "LabOrderItems");

            migrationBuilder.DropColumn(
                name: "LabTestId",
                table: "LabOrderItems");
        }
    }
}
