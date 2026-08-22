using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Corvida.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameUsersToAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Users_AssignedUserId",
                table: "Tasks");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Agents");

            migrationBuilder.Sql(
                "ALTER TABLE \"Agents\" RENAME CONSTRAINT \"PK_Users\" TO \"PK_Agents\";");

            migrationBuilder.RenameColumn(
                name: "AssignedUserId",
                table: "Tasks",
                newName: "AssignedAgentId");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_AssignedUserId",
                table: "Tasks",
                newName: "IX_Tasks_AssignedAgentId");

            migrationBuilder.RenameColumn(
                name: "UserIdsJson",
                table: "Boards",
                newName: "AgentIdsJson");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Agents_AssignedAgentId",
                table: "Tasks",
                column: "AssignedAgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Agents_AssignedAgentId",
                table: "Tasks");

            migrationBuilder.RenameColumn(
                name: "AgentIdsJson",
                table: "Boards",
                newName: "UserIdsJson");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_AssignedAgentId",
                table: "Tasks",
                newName: "IX_Tasks_AssignedUserId");

            migrationBuilder.RenameColumn(
                name: "AssignedAgentId",
                table: "Tasks",
                newName: "AssignedUserId");

            migrationBuilder.Sql(
                "ALTER TABLE \"Agents\" RENAME CONSTRAINT \"PK_Agents\" TO \"PK_Users\";");

            migrationBuilder.RenameTable(
                name: "Agents",
                newName: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Users_AssignedUserId",
                table: "Tasks",
                column: "AssignedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
