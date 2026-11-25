using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyKho2221.Migrations
{
    /// <inheritdoc />
    public partial class AddImportReceiptWarehouseBinRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ImportReceipts_WarehouseBinId",
                table: "ImportReceipts",
                column: "WarehouseBinId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImportReceipts_WarehouseBins_WarehouseBinId",
                table: "ImportReceipts",
                column: "WarehouseBinId",
                principalTable: "WarehouseBins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImportReceipts_WarehouseBins_WarehouseBinId",
                table: "ImportReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ImportReceipts_WarehouseBinId",
                table: "ImportReceipts");
        }
    }
}
