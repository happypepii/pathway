using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pathway.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanGuidance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "key_points",
                table: "tasks",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "milestones",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outcome",
                table: "milestones",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resources_json",
                table: "milestones",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_key_points",
                table: "tasks",
                sql: "cardinality(key_points) <= 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_milestones_resources",
                table: "milestones",
                sql: "jsonb_typeof(resources_json) = 'array' AND jsonb_array_length(resources_json) <= 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_key_points",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_milestones_resources",
                table: "milestones");

            migrationBuilder.DropColumn(
                name: "key_points",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "description",
                table: "milestones");

            migrationBuilder.DropColumn(
                name: "outcome",
                table: "milestones");

            migrationBuilder.DropColumn(
                name: "resources_json",
                table: "milestones");
        }
    }
}
