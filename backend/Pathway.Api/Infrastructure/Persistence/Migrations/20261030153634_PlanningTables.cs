using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pathway.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanningTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "milestones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milestones", x => x.id);
                    table.CheckConstraint("ck_milestones_title", "char_length(title) BETWEEN 1 AND 200");
                    table.ForeignKey(
                        name: "FK_milestones_goals_goal_id",
                        column: x => x.goal_id,
                        principalTable: "goals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planning_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    answer = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planning_answers", x => x.id);
                    table.CheckConstraint("ck_answers_length", "char_length(answer) <= 2000");
                    table.ForeignKey(
                        name: "FK_planning_answers_goals_goal_id",
                        column: x => x.goal_id,
                        principalTable: "goals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: false),
                    scheduled_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    scheduled_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                    table.CheckConstraint("ck_tasks_dates", "scheduled_end_date >= scheduled_start_date");
                    table.CheckConstraint("ck_tasks_effort", "estimated_minutes > 0 AND estimated_minutes % 60 = 0");
                    table.CheckConstraint("ck_tasks_title", "char_length(title) BETWEEN 1 AND 200");
                    table.ForeignKey(
                        name: "FK_tasks_milestones_milestone_id",
                        column: x => x.milestone_id,
                        principalTable: "milestones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_milestones_goal_id_sort_order",
                table: "milestones",
                columns: new[] { "goal_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planning_answers_goal_id_question_key",
                table: "planning_answers",
                columns: new[] { "goal_id", "question_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_milestone_id_sort_order",
                table: "tasks",
                columns: new[] { "milestone_id", "sort_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planning_answers");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "milestones");
        }
    }
}
