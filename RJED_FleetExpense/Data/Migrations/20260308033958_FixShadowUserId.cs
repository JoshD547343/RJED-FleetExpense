using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RJED_FleetExpense.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixShadowUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_UserId1",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_UserId1",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "Expenses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_UserId1",
                table: "Expenses",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_UserId1",
                table: "Expenses",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "UserId");
        }
    }
}