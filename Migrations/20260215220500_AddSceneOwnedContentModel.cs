using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260215220500_AddSceneOwnedContentModel")]
    public partial class AddSceneOwnedContentModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SceneAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SceneNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    AnchorFrom = table.Column<int>(type: "INTEGER", nullable: false),
                    AnchorTo = table.Column<int>(type: "INTEGER", nullable: false),
                    AnchorText = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SceneAnnotations_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SceneCards",
                columns: table => new
                {
                    SceneNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NarrativePurpose = table.Column<string>(type: "TEXT", nullable: true),
                    EmotionalBeat = table.Column<string>(type: "TEXT", nullable: true),
                    KeyEvents = table.Column<string>(type: "TEXT", nullable: true),
                    OpenQuestions = table.Column<string>(type: "TEXT", nullable: true),
                    PovCharacterId = table.Column<string>(type: "TEXT", nullable: true),
                    PlaceId = table.Column<string>(type: "TEXT", nullable: true),
                    TimelineEventId = table.Column<string>(type: "TEXT", nullable: true),
                    TimeRef = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ReferencesJson = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneCards", x => x.SceneNodeId);
                    table.ForeignKey(
                        name: "FK_SceneCards_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SceneContents",
                columns: table => new
                {
                    SceneNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentJson = table.Column<string>(type: "TEXT", nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneContents", x => x.SceneNodeId);
                    table.ForeignKey(
                        name: "FK_SceneContents_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SceneNotes",
                columns: table => new
                {
                    SceneNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NotesText = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneNotes", x => x.SceneNodeId);
                    table.ForeignKey(
                        name: "FK_SceneNotes_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SceneQualityIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SceneNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", nullable: false),
                    IssueKey = table.Column<string>(type: "TEXT", nullable: false),
                    RuleId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Suggestion = table.Column<string>(type: "TEXT", nullable: true),
                    AnchorText = table.Column<string>(type: "TEXT", nullable: true),
                    StartOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    EndOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneQualityIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SceneQualityIssues_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SceneVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SceneNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    ContentCompressed = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ContentTextHash = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<int>(type: "INTEGER", nullable: false),
                    WordCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SceneVersions_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SceneAnnotations_CreatedAt",
                table: "SceneAnnotations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SceneAnnotations_Kind",
                table: "SceneAnnotations",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_SceneAnnotations_SceneNodeId",
                table: "SceneAnnotations",
                column: "SceneNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_SceneAnnotations_Status",
                table: "SceneAnnotations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SceneQualityIssues_ContentHash",
                table: "SceneQualityIssues",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_SceneQualityIssues_IssueKey",
                table: "SceneQualityIssues",
                column: "IssueKey");

            migrationBuilder.CreateIndex(
                name: "IX_SceneQualityIssues_SceneNodeId",
                table: "SceneQualityIssues",
                column: "SceneNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_SceneQualityIssues_Scope",
                table: "SceneQualityIssues",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_SceneVersions_CreatedAt",
                table: "SceneVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SceneVersions_SceneNodeId",
                table: "SceneVersions",
                column: "SceneNodeId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SceneAnnotations");

            migrationBuilder.DropTable(
                name: "SceneCards");

            migrationBuilder.DropTable(
                name: "SceneContents");

            migrationBuilder.DropTable(
                name: "SceneNotes");

            migrationBuilder.DropTable(
                name: "SceneQualityIssues");

            migrationBuilder.DropTable(
                name: "SceneVersions");
        }
    }
}
