using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260208150000_AddProjectGoalsProgress")]
    public partial class AddProjectGoalsProgress : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectGoals",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DailyTargetWords = table.Column<int>(type: "INTEGER", nullable: false),
                    WeeklyTargetWords = table.Column<int>(type: "INTEGER", nullable: false),
                    Timezone = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectGoals", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectGoals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    TargetWords = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMilestones_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectProgressDaily",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    WordsDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectProgressDaily", x => new { x.ProjectId, x.Date });
                    table.ForeignKey(
                        name: "FK_ProjectProgressDaily_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectProgressEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventKey = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    WordsDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectProgressEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectProgressEvents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    -- SQLite clean-db safety: do not require Projects table presence to create this table.
                    CREATE TABLE IF NOT EXISTS "WritingSessions" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_WritingSessions" PRIMARY KEY,
                        "ProjectId" TEXT NOT NULL,
                        "StartedUtc" TEXT NOT NULL,
                        "EndedUtc" TEXT NULL,
                        "DurationSeconds" INTEGER NOT NULL,
                        "WordsDelta" INTEGER NOT NULL,
                        "StartWordCount" INTEGER NOT NULL,
                        "Notes" TEXT NULL
                    );
                    """);
            }
            else
            {
                migrationBuilder.CreateTable(
                    name: "WritingSessions",
                    columns: table => new
                    {
                        Id = table.Column<Guid>(type: "TEXT", nullable: false),
                        ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                        StartedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                        EndedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                        DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                        WordsDelta = table.Column<int>(type: "INTEGER", nullable: false),
                        StartWordCount = table.Column<int>(type: "INTEGER", nullable: false),
                        Notes = table.Column<string>(type: "TEXT", nullable: true)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_WritingSessions", x => x.Id);
                        table.ForeignKey(
                            name: "FK_WritingSessions_Projects_ProjectId",
                            column: x => x.ProjectId,
                            principalTable: "Projects",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });
            }

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_ProjectId",
                table: "ProjectMilestones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_Status",
                table: "ProjectMilestones",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProgressEvents_ProjectId",
                table: "ProjectProgressEvents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProgressEvents_ProjectId_EventKey",
                table: "ProjectProgressEvents",
                columns: new[] { "ProjectId", "EventKey" },
                unique: true);

            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    CREATE INDEX IF NOT EXISTS "IX_WritingSessions_ProjectId_StartedUtc"
                    ON "WritingSessions" ("ProjectId", "StartedUtc");
                    """);
            }
            else
            {
                migrationBuilder.CreateIndex(
                    name: "IX_WritingSessions_ProjectId_StartedUtc",
                    table: "WritingSessions",
                    columns: new[] { "ProjectId", "StartedUtc" });
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectGoals");

            migrationBuilder.DropTable(
                name: "ProjectMilestones");

            migrationBuilder.DropTable(
                name: "ProjectProgressDaily");

            migrationBuilder.DropTable(
                name: "ProjectProgressEvents");

            migrationBuilder.DropTable(
                name: "WritingSessions");
        }
    }
}
