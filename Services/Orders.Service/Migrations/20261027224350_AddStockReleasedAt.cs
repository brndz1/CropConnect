using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReleasedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StockReleasedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_StockReleasedAt",
                table: "Orders",
                columns: new[] { "Status", "StockReleasedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_StockReleasedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StockReleasedAt",
                table: "Orders");
        }
    }
}
