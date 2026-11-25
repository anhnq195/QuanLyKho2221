using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyKho2221.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorNumberToWarehouseBins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FloorNumber",
                table: "WarehouseBins",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FloorNumber",
                table: "WarehouseBins");
        }
    }
}
