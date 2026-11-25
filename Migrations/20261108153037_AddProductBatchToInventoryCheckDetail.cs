using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyKho2221.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBatchToInventoryCheckDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductBatchId",
                table: "InventoryCheckDetails",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCheckDetails_ProductBatchId",
                table: "InventoryCheckDetails",
                column: "ProductBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCheckDetails_ProductBatches_ProductBatchId",
                table: "InventoryCheckDetails",
                column: "ProductBatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCheckDetails_ProductBatches_ProductBatchId",
                table: "InventoryCheckDetails");

            migrationBuilder.DropIndex(
                name: "IX_InventoryCheckDetails_ProductBatchId",
                table: "InventoryCheckDetails");

            migrationBuilder.DropColumn(
                name: "ProductBatchId",
                table: "InventoryCheckDetails");
        }
    }
}
