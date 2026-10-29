using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pathway.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anonymous_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anonymous_sessions", x => x.id);
                    table.CheckConstraint("ck_anonymous_sessions_token_hash", "token_hash ~ '^[0-9a-fA-F]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    available_hours_per_week = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goals", x => x.id);
                    table.CheckConstraint("ck_goals_capacity", "available_hours_per_week BETWEEN 1 AND 168");
                    table.CheckConstraint("ck_goals_description", "char_length(description) <= 2000");
                    table.CheckConstraint("ck_goals_status", "status IN ('Draft', 'Active', 'Completed', 'Archived')");
                    table.CheckConstraint("ck_goals_title", "char_length(title) >= 1 AND title !~ '^[[:space:]]|[[:space:]]$'");
                    table.ForeignKey(
                        name: "FK_goals_anonymous_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "anonymous_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_anonymous_sessions_expires_at",
                table: "anonymous_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_anonymous_sessions_token_hash",
                table: "anonymous_sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goals_session_id",
                table: "goals",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "goals");

            migrationBuilder.DropTable(
                name: "anonymous_sessions");
        }
    }
}
