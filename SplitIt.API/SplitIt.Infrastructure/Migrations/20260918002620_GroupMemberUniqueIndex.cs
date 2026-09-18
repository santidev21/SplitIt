using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GroupMemberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupMembers_GroupId",
                table: "GroupMembers");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_GroupId_UserId",
                table: "GroupMembers",
                columns: new[] { "GroupId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupMembers_GroupId_UserId",
                table: "GroupMembers");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_GroupId",
                table: "GroupMembers",
                column: "GroupId");
        }
    }
}
