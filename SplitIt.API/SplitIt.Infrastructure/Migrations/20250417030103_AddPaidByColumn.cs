using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidByColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaidById",
                table: "Expense",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Expense_PaidById",
                table: "Expense",
                column: "PaidById");

            migrationBuilder.AddForeignKey(
                name: "FK_Expense_Users_PaidById",
                table: "Expense",
                column: "PaidById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expense_Users_PaidById",
                table: "Expense");

            migrationBuilder.DropIndex(
                name: "IX_Expense_PaidById",
                table: "Expense");

            migrationBuilder.DropColumn(
                name: "PaidById",
                table: "Expense");
        }
    }
}
