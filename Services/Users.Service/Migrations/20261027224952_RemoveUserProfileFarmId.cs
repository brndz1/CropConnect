using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Users.Service.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserProfileFarmId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmId",
                table: "UserProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FarmId",
                table: "UserProfiles",
                type: "uuid",
                nullable: true);
        }
    }
}
