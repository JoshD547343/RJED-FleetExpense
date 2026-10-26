using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RJED_FleetExpense.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLockoutStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LockoutStage",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockoutStage",
                table: "AspNetUsers");
        }
    }
}
