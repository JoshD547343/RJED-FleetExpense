using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RJED_FleetExpense.Data.Migrations
{
    /// <inheritdoc />
    public partial class Expenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Commented out — Trip_Details may not exist yet ──
            // migrationBuilder.AlterColumn<decimal>(
            //     name: "FuelLiters",
            //     table: "Trip_Details",
            //     type: "decimal(7,2)",
            //     nullable: true,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(4,2)",
            //     oldNullable: true);

            // migrationBuilder.AlterColumn<decimal>(
            //     name: "DistanceKm",
            //     table: "Trip_Details",
            //     type: "decimal(7,2)",
            //     nullable: true,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(4,2)",
            //     oldNullable: true);

            // ── Commented out — Reimbursements may not exist yet ──
            // migrationBuilder.AlterColumn<decimal>(
            //     name: "AmountPaid",
            //     table: "Reimbursements",
            //     type: "decimal(10,2)",
            //     nullable: false,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(5,2)");

            migrationBuilder.AlterColumn<string>(
                name: "PlateNumber",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ExpenseType",
                table: "Expenses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Expenses",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            // ── Commented out — Budgets may not exist yet ──
            // migrationBuilder.AlterColumn<decimal>(
            //     name: "TotalAmount",
            //     table: "Budgets",
            //     type: "decimal(10,2)",
            //     nullable: false,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(6,2)");

            // migrationBuilder.AlterColumn<decimal>(
            //     name: "RemainingAmount",
            //     table: "Budgets",
            //     type: "decimal(10,2)",
            //     nullable: false,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(5,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.AlterColumn<decimal>(
            //     name: "FuelLiters",
            //     table: "Trip_Details",
            //     type: "decimal(4,2)",
            //     nullable: true,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(7,2)",
            //     oldNullable: true);

            // migrationBuilder.AlterColumn<decimal>(
            //     name: "DistanceKm",
            //     table: "Trip_Details",
            //     type: "decimal(4,2)",
            //     nullable: true,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(7,2)",
            //     oldNullable: true);

            // migrationBuilder.AlterColumn<decimal>(
            //     name: "AmountPaid",
            //     table: "Reimbursements",
            //     type: "decimal(5,2)",
            //     nullable: false,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(10,2)");

            migrationBuilder.AlterColumn<string>(
                name: "PlateNumber",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ExpenseType",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Expenses",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            // migrationBuilder.AlterColumn<decimal>(
            //     name: "TotalAmount",
            //     table: "Budgets",
            //     type: "decimal(6,2)",
            //     nullable: false,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(10,2)");

            // migrationBuilder.AlterColumn<decimal>(
            //     name: "RemainingAmount",
            //     table: "Budgets",
            //     type: "decimal(5,2)",
            //     nullable: false,
            //     oldClrType: typeof(decimal),
            //     oldType: "decimal(10,2)");
        }
    }
}