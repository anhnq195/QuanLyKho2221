
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyKho2221.Migrations
{
    public partial class AddMultiWarehouseAndBatches : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cập nhật cấu trúc bảng Products
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Products",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasExpiryDate",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Tạo bảng Warehouses
            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ManagerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            // Tạo bảng WarehouseAreas
            migrationBuilder.CreateTable(
                name: "WarehouseAreas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseAreas", x => x.Id);

                    table.ForeignKey(
                        name: "FK_WarehouseAreas_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Tạo bảng WarehouseShelves
            migrationBuilder.CreateTable(
                name: "WarehouseShelves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WarehouseAreaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseShelves", x => x.Id);

                    table.ForeignKey(
                        name: "FK_WarehouseShelves_WarehouseAreas_WarehouseAreaId",
                        column: x => x.WarehouseAreaId,
                        principalTable: "WarehouseAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Tạo bảng WarehouseBins
            migrationBuilder.CreateTable(
                name: "WarehouseBins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WarehouseShelfId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseBins", x => x.Id);

                    table.ForeignKey(
                        name: "FK_WarehouseBins_WarehouseShelves_WarehouseShelfId",
                        column: x => x.WarehouseShelfId,
                        principalTable: "WarehouseShelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Tạo bảng ProductBatches
            migrationBuilder.CreateTable(
                name: "ProductBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseBinId = table.Column<int>(type: "int", nullable: false),
                    BatchCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManufactureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBatches", x => x.Id);

                    table.ForeignKey(
                        name: "FK_ProductBatches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);

                    table.ForeignKey(
                        name: "FK_ProductBatches_WarehouseBins_WarehouseBinId",
                        column: x => x.WarehouseBinId,
                        principalTable: "WarehouseBins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Tạo Index
            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductId_BatchCode_WarehouseBinId",
                table: "ProductBatches",
                columns: new[] { "ProductId", "BatchCode", "WarehouseBinId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_WarehouseBinId",
                table: "ProductBatches",
                column: "WarehouseBinId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseAreas_WarehouseId",
                table: "WarehouseAreas",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBins_WarehouseShelfId",
                table: "WarehouseBins",
                column: "WarehouseShelfId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Code",
                table: "Warehouses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseShelves_WarehouseAreaId",
                table: "WarehouseShelves",
                column: "WarehouseAreaId");

            // Tạo kho mặc định và chuyển tồn kho hiện tại thành các lô hàng
            migrationBuilder.Sql(@"
                INSERT INTO Warehouses
                (Code, Name, Address, ManagerName, Description, IsActive, CreatedAt)
                VALUES
                ('KHO01', N'Kho chính', N'Chưa cập nhật',
                 N'Quản lý kho',
                 N'Kho mặc định được tạo khi nâng cấp hệ thống',
                 1, GETDATE());

                DECLARE @WarehouseId INT = SCOPE_IDENTITY();

                INSERT INTO WarehouseAreas
                (Code, Name, WarehouseId)
                VALUES ('KV01', N'Khu vực chính', @WarehouseId);

                DECLARE @AreaId INT = SCOPE_IDENTITY();

                INSERT INTO WarehouseShelves
                (Code, Name, WarehouseAreaId)
                VALUES ('KE01', N'Kệ chính', @AreaId);

                DECLARE @ShelfId INT = SCOPE_IDENTITY();

                INSERT INTO WarehouseBins
                (Code, Name, WarehouseShelfId)
                VALUES ('O01', N'Ô chứa chính', @ShelfId);

                DECLARE @BinId INT = SCOPE_IDENTITY();

                INSERT INTO ProductBatches
                (ProductId, WarehouseBinId, BatchCode,
                 ManufactureDate, ExpiryDate, Quantity, CreatedAt)
                SELECT
                    Id,
                    @BinId,
                    CONCAT('INIT-', Id),
                    NULL,
                    NULL,
                    Quantity,
                    GETDATE()
                FROM Products;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProductBatches");
            migrationBuilder.DropTable(name: "WarehouseBins");
            migrationBuilder.DropTable(name: "WarehouseShelves");
            migrationBuilder.DropTable(name: "WarehouseAreas");
            migrationBuilder.DropTable(name: "Warehouses");

            migrationBuilder.DropColumn(
                name: "HasExpiryDate",
                table: "Products");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
