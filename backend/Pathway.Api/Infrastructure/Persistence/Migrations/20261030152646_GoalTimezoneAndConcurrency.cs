using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pathway.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoalTimezoneAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                table: "goals",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "UTC");

            migrationBuilder.AddColumn<Guid>(
                name: "version",
                table: "goals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "time_zone_id",
                table: "goals");

            migrationBuilder.DropColumn(
                name: "version",
                table: "goals");
        }
    }
}
