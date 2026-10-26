using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RJED_FleetExpense.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RejectedDate",
                table: "Expenses",
                newName: "SupervisorRejectedDate");

            migrationBuilder.RenameColumn(
                name: "ApprovedDate",
                table: "Expenses",
                newName: "SupervisorApprovedDate");

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartmentApprovedDate",
                table: "Expenses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartmentRejectedDate",
                table: "Expenses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartmentRemarks",
                table: "Expenses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartmentStatus",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupervisorStatus",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepartmentApprovedDate",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "DepartmentRejectedDate",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "DepartmentRemarks",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "DepartmentStatus",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "SupervisorStatus",
                table: "Expenses");

            migrationBuilder.RenameColumn(
                name: "SupervisorRejectedDate",
                table: "Expenses",
                newName: "RejectedDate");

            migrationBuilder.RenameColumn(
                name: "SupervisorApprovedDate",
                table: "Expenses",
                newName: "ApprovedDate");
        }
    }
}
