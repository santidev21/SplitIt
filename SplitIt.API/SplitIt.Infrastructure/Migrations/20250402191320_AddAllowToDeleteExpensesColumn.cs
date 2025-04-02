using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowToDeleteExpensesColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowToDeleteExpenses",
                table: "Groups",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowToDeleteExpenses",
                table: "Groups");
        }
    }
}
