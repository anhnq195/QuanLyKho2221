using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyKho2221.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCheckDetails_ProductBatches_ProductBatchId",
                table: "InventoryCheckDetails");

            migrationBuilder.CreateTable(
                name: "WarehouseTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductBatchId = table.Column<int>(type: "int", nullable: false),
                    SourceBinId = table.Column<int>(type: "int", nullable: false),
                    DestinationBinId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_WarehouseBins_DestinationBinId",
                        column: x => x.DestinationBinId,
                        principalTable: "WarehouseBins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_WarehouseBins_SourceBinId",
                        column: x => x.SourceBinId,
                        principalTable: "WarehouseBins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_DestinationBinId",
                table: "WarehouseTransfers",
                column: "DestinationBinId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_ProductBatchId",
                table: "WarehouseTransfers",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_SourceBinId",
                table: "WarehouseTransfers",
                column: "SourceBinId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCheckDetails_ProductBatches_ProductBatchId",
                table: "InventoryCheckDetails",
                column: "ProductBatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCheckDetails_ProductBatches_ProductBatchId",
                table: "InventoryCheckDetails");

            migrationBuilder.DropTable(
                name: "WarehouseTransfers");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCheckDetails_ProductBatches_ProductBatchId",
                table: "InventoryCheckDetails",
                column: "ProductBatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id");
        }
    }
}
