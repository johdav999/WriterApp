using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260224213000_AddProjectGoalsProgressFix")]
    public partial class AddProjectGoalsProgressFix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    CREATE TABLE IF NOT EXISTS "ProjectGoals" (
                        "ProjectId" TEXT NOT NULL CONSTRAINT "PK_ProjectGoals" PRIMARY KEY,
                        "DailyTargetWords" INTEGER NOT NULL,
                        "WeeklyTargetWords" INTEGER NOT NULL,
                        "Timezone" TEXT NOT NULL,
                        "UpdatedUtc" TEXT NOT NULL,
                        CONSTRAINT "FK_ProjectGoals_Projects_ProjectId"
                            FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE CASCADE
                    );
                    """);

                migrationBuilder.Sql(
                    """
                    CREATE TABLE IF NOT EXISTS "ProjectProgressDaily" (
                        "ProjectId" TEXT NOT NULL,
                        "Date" TEXT NOT NULL,
                        "WordsDelta" INTEGER NOT NULL,
                        "UpdatedUtc" TEXT NOT NULL,
                        CONSTRAINT "PK_ProjectProgressDaily" PRIMARY KEY ("ProjectId", "Date"),
                        CONSTRAINT "FK_ProjectProgressDaily_Projects_ProjectId"
                            FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE CASCADE
                    );
                    """);

                migrationBuilder.Sql(
                    """
                    CREATE INDEX IF NOT EXISTS "IX_ProjectGoals_ProjectId"
                    ON "ProjectGoals" ("ProjectId");
                    """);

                migrationBuilder.Sql(
                    """
                    CREATE INDEX IF NOT EXISTS "IX_ProjectProgressDaily_ProjectId"
                    ON "ProjectProgressDaily" ("ProjectId");
                    """);

                migrationBuilder.Sql(
                    """
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProjectProgressDaily_ProjectId_Date"
                    ON "ProjectProgressDaily" ("ProjectId", "Date");
                    """);
                return;
            }

            migrationBuilder.CreateTable(
                name: "ProjectGoals",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyTargetWords = table.Column<int>(type: "int", nullable: false),
                    WeeklyTargetWords = table.Column<int>(type: "int", nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                name: "ProjectProgressDaily",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    WordsDelta = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGoals_ProjectId",
                table: "ProjectGoals",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProgressDaily_ProjectId",
                table: "ProjectProgressDaily",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProgressDaily_ProjectId_Date",
                table: "ProjectProgressDaily",
                columns: new[] { "ProjectId", "Date" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ProjectProgressDaily_ProjectId_Date\";");
                migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ProjectProgressDaily_ProjectId\";");
                migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ProjectGoals_ProjectId\";");
                migrationBuilder.Sql("DROP TABLE IF EXISTS \"ProjectProgressDaily\";");
                migrationBuilder.Sql("DROP TABLE IF EXISTS \"ProjectGoals\";");
                return;
            }

            migrationBuilder.DropTable(name: "ProjectProgressDaily");
            migrationBuilder.DropTable(name: "ProjectGoals");
        }
    }
}
