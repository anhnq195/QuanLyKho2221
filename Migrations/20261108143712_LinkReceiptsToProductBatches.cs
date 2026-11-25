using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyKho2221.Migrations
{
    /// <inheritdoc />
    public partial class LinkReceiptsToProductBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductBatchId",
                table: "ImportReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductBatchId",
                table: "ExportReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportReceipts_ProductBatchId",
                table: "ImportReceipts",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportReceipts_ProductBatchId",
                table: "ExportReceipts",
                column: "ProductBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExportReceipts_ProductBatches_ProductBatchId",
                table: "ExportReceipts",
                column: "ProductBatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ImportReceipts_ProductBatches_ProductBatchId",
                table: "ImportReceipts",
                column: "ProductBatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExportReceipts_ProductBatches_ProductBatchId",
                table: "ExportReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportReceipts_ProductBatches_ProductBatchId",
                table: "ImportReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ImportReceipts_ProductBatchId",
                table: "ImportReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ExportReceipts_ProductBatchId",
                table: "ExportReceipts");

            migrationBuilder.DropColumn(
                name: "ProductBatchId",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "ProductBatchId",
                table: "ExportReceipts");
        }
    }
}
