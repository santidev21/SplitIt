using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSettledAndSettledAtToExpenseShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSettled",
                table: "ExpenseShare",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAt",
                table: "ExpenseShare",
                type: "datetime",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSettled",
                table: "ExpenseShare");

            migrationBuilder.DropColumn(
                name: "SettledAt",
                table: "ExpenseShare");
        }
    }
}
