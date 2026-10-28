using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farms.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Farms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Farms",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Farms");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Farms");
        }
    }
}
