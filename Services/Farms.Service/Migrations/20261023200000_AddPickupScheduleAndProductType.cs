using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farms.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupScheduleAndProductType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Product");

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableUntil",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PickupSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CloseTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupSchedules", x => x.Id);
                    table.CheckConstraint("CK_PickupSchedules_CloseTime_After_OpenTime", "\"CloseTime\" > \"OpenTime\"");
                    table.ForeignKey(
                        name: "FK_PickupSchedules_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Type",
                table: "Products",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_PickupSchedules_FarmId",
                table: "PickupSchedules",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_PickupSchedules_FarmId_DayOfWeek",
                table: "PickupSchedules",
                columns: new[] { "FarmId", "DayOfWeek" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickupSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Products_Type",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AvailableUntil",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Products");
        }
    }
}
