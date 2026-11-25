using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyKho2221.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptApprovalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ImportReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "ImportReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchCode",
                table: "ImportReceipts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "ImportReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                table: "ImportReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ImportReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "ImportReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufactureDate",
                table: "ImportReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "ImportReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                table: "ImportReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ImportReceipts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ImportReceipts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<int>(
                name: "WarehouseBinId",
                table: "ImportReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ExportReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "ExportReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "ExportReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                table: "ExportReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExportReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "ExportReceipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                table: "ExportReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ExportReceipts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ExportReceipts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Approved");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "BatchCode",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "WarehouseBinId",
                table: "ImportReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ExportReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "ExportReceipts");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "ExportReceipts");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "ExportReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExportReceipts");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "ExportReceipts");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "ExportReceipts");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ExportReceipts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ExportReceipts");
        }
    }
}
