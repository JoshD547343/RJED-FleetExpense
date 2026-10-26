using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace RJED_FleetExpense.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Expenses column changes (only alter, no AddColumn for already-existing cols) ──
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
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
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Expenses",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2);

            // ── Add columns only if they don't already exist ──
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'ProjectId')
                    ALTER TABLE [Expenses] ADD [ProjectId] int NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'ReceiptImageUrl')
                    ALTER TABLE [Expenses] ADD [ReceiptImageUrl] nvarchar(255) NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'VehicleId')
                    ALTER TABLE [Expenses] ADD [VehicleId] int NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'CategoryId')
                    ALTER TABLE [Expenses] ADD [CategoryId] int NULL;
            ");

            // ── Create domain tables (safe: only if not exists) ──
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Departments')
                CREATE TABLE [Departments] (
                    [DepartmentId] int IDENTITY(1,1) NOT NULL,
                    [DepartmentName] nvarchar(50) NOT NULL,
                    [CostCenterCode] nvarchar(20) NULL,
                    CONSTRAINT [PK_Departments] PRIMARY KEY ([DepartmentId])
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Expense_Categories')
                CREATE TABLE [Expense_Categories] (
                    [CategoryId] int IDENTITY(1,1) NOT NULL,
                    [CategoryName] nvarchar(50) NOT NULL,
                    [RequiresReceipt] bit NOT NULL,
                    CONSTRAINT [PK_Expense_Categories] PRIMARY KEY ([CategoryId])
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Vehicles')
                CREATE TABLE [Vehicles] (
                    [VehicleId] int IDENTITY(1,1) NOT NULL,
                    [PlateNumber] nvarchar(20) NOT NULL,
                    [Type] nvarchar(50) NULL,
                    [Status] nvarchar(20) NOT NULL,
                    [AssignedDriverId] int NULL,
                    CONSTRAINT [PK_Vehicles] PRIMARY KEY ([VehicleId])
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Projects')
                CREATE TABLE [Projects] (
                    [ProjectId] int IDENTITY(1,1) NOT NULL,
                    [ProjectName] nvarchar(50) NOT NULL,
                    [ProjectCode] nvarchar(25) NULL,
                    [DepartmentId] int NOT NULL,
                    [StartDate] datetime2 NULL,
                    [EndDate] datetime2 NULL,
                    CONSTRAINT [PK_Projects] PRIMARY KEY ([ProjectId]),
                    CONSTRAINT [FK_Projects_Departments_DepartmentId] FOREIGN KEY ([DepartmentId])
                        REFERENCES [Departments] ([DepartmentId]) ON DELETE NO ACTION
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Budgets')
                CREATE TABLE [Budgets] (
                    [BudgetId] int IDENTITY(1,1) NOT NULL,
                    [DepartmentId] int NOT NULL,
                    [TotalAmount] decimal(10,2) NOT NULL,
                    [RemainingAmount] decimal(10,2) NOT NULL,
                    [StartDate] datetime2 NOT NULL,
                    [EndDate] datetime2 NOT NULL,
                    CONSTRAINT [PK_Budgets] PRIMARY KEY ([BudgetId]),
                    CONSTRAINT [FK_Budgets_Departments_DepartmentId] FOREIGN KEY ([DepartmentId])
                        REFERENCES [Departments] ([DepartmentId]) ON DELETE NO ACTION
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users')
                CREATE TABLE [Users] (
                    [UserId] int IDENTITY(1,1) NOT NULL,
                    [LastName] nvarchar(25) NOT NULL,
                    [FirstName] nvarchar(25) NOT NULL,
                    [Email] nvarchar(50) NOT NULL,
                    [Password] nvarchar(25) NOT NULL,
                    [Role] nvarchar(25) NOT NULL,
                    [DepartmentId] int NOT NULL,
                    [Status] nvarchar(10) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
                    CONSTRAINT [FK_Users_Departments_DepartmentId] FOREIGN KEY ([DepartmentId])
                        REFERENCES [Departments] ([DepartmentId]) ON DELETE NO ACTION
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Trip_Details')
                CREATE TABLE [Trip_Details] (
                    [TripId] int IDENTITY(1,1) NOT NULL,
                    [ExpenseId] int NOT NULL,
                    [StartLocation] nvarchar(100) NULL,
                    [EndLocation] nvarchar(100) NULL,
                    [DistanceKm] decimal(4,2) NULL,
                    [FuelLiters] decimal(4,2) NULL,
                    CONSTRAINT [PK_Trip_Details] PRIMARY KEY ([TripId]),
                    CONSTRAINT [FK_Trip_Details_Expenses_ExpenseId] FOREIGN KEY ([ExpenseId])
                        REFERENCES [Expenses] ([Id]) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Approval_Logs')
                CREATE TABLE [Approval_Logs] (
                    [ApprovalLogId] int IDENTITY(1,1) NOT NULL,
                    [ExpenseId] int NOT NULL,
                    [ReviewerId] int NOT NULL,
                    [Action] nvarchar(20) NOT NULL,
                    [Comments] nvarchar(50) NULL,
                    [ActionDate] datetime2 NOT NULL,
                    CONSTRAINT [PK_Approval_Logs] PRIMARY KEY ([ApprovalLogId]),
                    CONSTRAINT [FK_Approval_Logs_Expenses_ExpenseId] FOREIGN KEY ([ExpenseId])
                        REFERENCES [Expenses] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Approval_Logs_Users_ReviewerId] FOREIGN KEY ([ReviewerId])
                        REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Audit_Logs')
                CREATE TABLE [Audit_Logs] (
                    [AuditLogId] int IDENTITY(1,1) NOT NULL,
                    [UserId] int NOT NULL,
                    [ActionType] nvarchar(25) NULL,
                    [TableAffected] nvarchar(20) NULL,
                    [Details] nvarchar(50) NULL,
                    [Timestamp] datetime2 NOT NULL,
                    CONSTRAINT [PK_Audit_Logs] PRIMARY KEY ([AuditLogId]),
                    CONSTRAINT [FK_Audit_Logs_Users_UserId] FOREIGN KEY ([UserId])
                        REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Notifications')
                CREATE TABLE [Notifications] (
                    [NotificationId] int IDENTITY(1,1) NOT NULL,
                    [UserId] int NOT NULL,
                    [Title] nvarchar(50) NULL,
                    [Message] nvarchar(100) NULL,
                    [ReadStatus] nvarchar(10) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationId]),
                    CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId])
                        REFERENCES [Users] ([UserId]) ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reimbursements')
                CREATE TABLE [Reimbursements] (
                    [ReimbursementId] int IDENTITY(1,1) NOT NULL,
                    [ExpenseId] int NOT NULL,
                    [ProcessedById] int NOT NULL,
                    [TransactionRef] nvarchar(50) NULL,
                    [AmountPaid] decimal(5,2) NOT NULL,
                    [PaymentDate] datetime2 NOT NULL,
                    CONSTRAINT [PK_Reimbursements] PRIMARY KEY ([ReimbursementId]),
                    CONSTRAINT [FK_Reimbursements_Expenses_ExpenseId] FOREIGN KEY ([ExpenseId])
                        REFERENCES [Expenses] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Reimbursements_Users_ProcessedById] FOREIGN KEY ([ProcessedById])
                        REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
                );
            ");

            // ── Indexes commented out — add manually in SSMS if needed ──
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_CategoryId') CREATE INDEX [IX_Expenses_CategoryId] ON [Expenses] ([CategoryId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_ProjectId') CREATE INDEX [IX_Expenses_ProjectId] ON [Expenses] ([ProjectId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_VehicleId') CREATE INDEX [IX_Expenses_VehicleId] ON [Expenses] ([VehicleId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Approval_Logs_ExpenseId') CREATE INDEX [IX_Approval_Logs_ExpenseId] ON [Approval_Logs] ([ExpenseId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Approval_Logs_ReviewerId') CREATE INDEX [IX_Approval_Logs_ReviewerId] ON [Approval_Logs] ([ReviewerId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Audit_Logs_UserId') CREATE INDEX [IX_Audit_Logs_UserId] ON [Audit_Logs] ([UserId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Budgets_DepartmentId') CREATE INDEX [IX_Budgets_DepartmentId] ON [Budgets] ([DepartmentId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_UserId') CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Projects_DepartmentId') CREATE INDEX [IX_Projects_DepartmentId] ON [Projects] ([DepartmentId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reimbursements_ExpenseId') CREATE UNIQUE INDEX [IX_Reimbursements_ExpenseId] ON [Reimbursements] ([ExpenseId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reimbursements_ProcessedById') CREATE INDEX [IX_Reimbursements_ProcessedById] ON [Reimbursements] ([ProcessedById]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Trip_Details_ExpenseId') CREATE UNIQUE INDEX [IX_Trip_Details_ExpenseId] ON [Trip_Details] ([ExpenseId]);");
            // migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_DepartmentId') CREATE INDEX [IX_Users_DepartmentId] ON [Users] ([DepartmentId]);");

            // ── Foreign keys on Expenses commented out — add manually in SSMS if needed ──
            // migrationBuilder.Sql(@"
            //     IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expenses_Expense_Categories_CategoryId')
            //         ALTER TABLE [Expenses] ADD CONSTRAINT [FK_Expenses_Expense_Categories_CategoryId]
            //             FOREIGN KEY ([CategoryId]) REFERENCES [Expense_Categories] ([CategoryId]) ON DELETE NO ACTION;
            // ");

            // migrationBuilder.Sql(@"
            //     IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expenses_Projects_ProjectId')
            //         ALTER TABLE [Expenses] ADD CONSTRAINT [FK_Expenses_Projects_ProjectId]
            //             FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]) ON DELETE NO ACTION;
            // ");

            // migrationBuilder.Sql(@"
            //     IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expenses_Vehicles_VehicleId')
            //         ALTER TABLE [Expenses] ADD CONSTRAINT [FK_Expenses_Vehicles_VehicleId]
            //             FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE NO ACTION;
            // ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expenses_Vehicles_VehicleId') ALTER TABLE [Expenses] DROP CONSTRAINT [FK_Expenses_Vehicles_VehicleId];");
            // migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expenses_Projects_ProjectId') ALTER TABLE [Expenses] DROP CONSTRAINT [FK_Expenses_Projects_ProjectId];");
            // migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expenses_Expense_Categories_CategoryId') ALTER TABLE [Expenses] DROP CONSTRAINT [FK_Expenses_Expense_Categories_CategoryId];");

            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Approval_Logs') DROP TABLE [Approval_Logs];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Audit_Logs') DROP TABLE [Audit_Logs];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Notifications') DROP TABLE [Notifications];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reimbursements') DROP TABLE [Reimbursements];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Trip_Details') DROP TABLE [Trip_Details];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Budgets') DROP TABLE [Budgets];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Projects') DROP TABLE [Projects];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users') DROP TABLE [Users];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Vehicles') DROP TABLE [Vehicles];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Expense_Categories') DROP TABLE [Expense_Categories];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Departments') DROP TABLE [Departments];");

            // migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_CategoryId') DROP INDEX [IX_Expenses_CategoryId] ON [Expenses];");
            // migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_ProjectId') DROP INDEX [IX_Expenses_ProjectId] ON [Expenses];");
            // migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_VehicleId') DROP INDEX [IX_Expenses_VehicleId] ON [Expenses];");

            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'CategoryId') ALTER TABLE [Expenses] DROP COLUMN [CategoryId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'ProjectId') ALTER TABLE [Expenses] DROP COLUMN [ProjectId];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'ReceiptImageUrl') ALTER TABLE [Expenses] DROP COLUMN [ReceiptImageUrl];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expenses') AND name = 'VehicleId') ALTER TABLE [Expenses] DROP COLUMN [VehicleId];");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Expenses",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");
        }
    }
}