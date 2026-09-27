using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    /// <summary>
    /// Synchronizes the SQLite schema and model snapshot before moving to EF Core 10.
    /// Earlier hand-authored migrations created the tables omitted here but did not
    /// update the model snapshot, so this migration only applies the remaining schema
    /// differences.
    /// </summary>
    public partial class UpgradeToNet10 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SectionSceneCards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubplotTagsJson",
                table: "SectionSceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "SectionSceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SceneCards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubplotTagsJson",
                table: "SceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "SceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PlanEntitlements",
                keyColumns: new[] { "Key", "PlanId" },
                keyValues: new object[]
                {
                    "ai.monthly_tokens",
                    new Guid("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5")
                },
                column: "Value",
                value: "250000");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerUserId_UpdatedAtUnixSeconds",
                table: "Documents",
                columns: new[] { "OwnerUserId", "UpdatedAtUnixSeconds" });

            migrationBuilder.DropIndex(
                name: "IX_SearchIndexEntries_Document",
                table: "SearchIndexEntries");

            migrationBuilder.DropIndex(
                name: "IX_SearchIndexEntries_Entity",
                table: "SearchIndexEntries");

            migrationBuilder.CreateIndex(
                name: "IX_SearchIndexEntries_DocumentId",
                table: "SearchIndexEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchIndexEntries_EntityType_EntityId_DocumentId",
                table: "SearchIndexEntries",
                columns: new[] { "EntityType", "EntityId", "DocumentId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_OwnerUserId_UpdatedAtUnixSeconds",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_SearchIndexEntries_DocumentId",
                table: "SearchIndexEntries");

            migrationBuilder.DropIndex(
                name: "IX_SearchIndexEntries_EntityType_EntityId_DocumentId",
                table: "SearchIndexEntries");

            migrationBuilder.CreateIndex(
                name: "IX_SearchIndexEntries_Document",
                table: "SearchIndexEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchIndexEntries_Entity",
                table: "SearchIndexEntries",
                columns: new[] { "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SectionSceneCards");

            migrationBuilder.DropColumn(
                name: "SubplotTagsJson",
                table: "SectionSceneCards");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "SectionSceneCards");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SceneCards");

            migrationBuilder.DropColumn(
                name: "SubplotTagsJson",
                table: "SceneCards");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "SceneCards");

            migrationBuilder.UpdateData(
                table: "PlanEntitlements",
                keyColumns: new[] { "Key", "PlanId" },
                keyValues: new object[]
                {
                    "ai.monthly_tokens",
                    new Guid("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5")
                },
                column: "Value",
                value: "200000");
        }
    }
}
