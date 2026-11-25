using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Corvida.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DependsOnJson",
                table: "Tasks",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<double>(
                name: "EstimateMostLikely",
                table: "Tasks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimateOptimistic",
                table: "Tasks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimatePessimistic",
                table: "Tasks",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DependsOnJson",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EstimateMostLikely",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EstimateOptimistic",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EstimatePessimistic",
                table: "Tasks");
        }
    }
}
