using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RJED_FleetExpense.Data.Migrations
{
    public partial class AuditLog_SafeFix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only fix the column type and add the correct FK
            // Avoid dropping constraints that may not exist

            migrationBuilder.AlterColumn<string>(
                name: "user_id",
                table: "audit_logs",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Add index if not exists
            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            // Add the correct foreign key (EF will skip if already exists in some cases)
            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_AspNetUsers_user_id",
                table: "audit_logs",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_AspNetUsers_user_id",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs");

            migrationBuilder.AlterColumn<int>(
                name: "user_id",
                table: "audit_logs",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}