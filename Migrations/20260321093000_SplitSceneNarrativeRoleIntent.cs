using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    public partial class SplitSceneNarrativeRoleIntent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NarrativeIntent",
                table: "SceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrativeRole",
                table: "SceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrativeIntent",
                table: "SectionSceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrativeRole",
                table: "SectionSceneCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE SceneCards
                SET
                    NarrativeRole = CASE LOWER(TRIM(COALESCE(NarrativePurpose, '')))
                        WHEN 'setup' THEN 'Setup'
                        WHEN 'inciting incident' THEN 'Inciting Incident'
                        WHEN 'rising action' THEN 'Rising Action'
                        WHEN 'complication' THEN 'Complication'
                        WHEN 'revelation' THEN 'Revelation'
                        WHEN 'relationship beat' THEN 'Relationship Beat'
                        WHEN 'reversal' THEN 'Reversal'
                        WHEN 'decision' THEN 'Decision'
                        WHEN 'climax' THEN 'Climax'
                        WHEN 'aftermath' THEN 'Aftermath'
                        ELSE NULL
                    END,
                    NarrativeIntent = CASE
                        WHEN LOWER(TRIM(COALESCE(NarrativePurpose, ''))) IN (
                            'setup',
                            'inciting incident',
                            'rising action',
                            'complication',
                            'revelation',
                            'relationship beat',
                            'reversal',
                            'decision',
                            'climax',
                            'aftermath')
                        THEN NULL
                        WHEN TRIM(COALESCE(NarrativePurpose, '')) = '' THEN NULL
                        ELSE TRIM(NarrativePurpose)
                    END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE SectionSceneCards
                SET
                    NarrativeRole = CASE LOWER(TRIM(COALESCE(NarrativePurpose, '')))
                        WHEN 'setup' THEN 'Setup'
                        WHEN 'inciting incident' THEN 'Inciting Incident'
                        WHEN 'rising action' THEN 'Rising Action'
                        WHEN 'complication' THEN 'Complication'
                        WHEN 'revelation' THEN 'Revelation'
                        WHEN 'relationship beat' THEN 'Relationship Beat'
                        WHEN 'reversal' THEN 'Reversal'
                        WHEN 'decision' THEN 'Decision'
                        WHEN 'climax' THEN 'Climax'
                        WHEN 'aftermath' THEN 'Aftermath'
                        ELSE NULL
                    END,
                    NarrativeIntent = CASE
                        WHEN LOWER(TRIM(COALESCE(NarrativePurpose, ''))) IN (
                            'setup',
                            'inciting incident',
                            'rising action',
                            'complication',
                            'revelation',
                            'relationship beat',
                            'reversal',
                            'decision',
                            'climax',
                            'aftermath')
                        THEN NULL
                        WHEN TRIM(COALESCE(NarrativePurpose, '')) = '' THEN NULL
                        ELSE TRIM(NarrativePurpose)
                    END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NarrativeIntent",
                table: "SceneCards");

            migrationBuilder.DropColumn(
                name: "NarrativeRole",
                table: "SceneCards");

            migrationBuilder.DropColumn(
                name: "NarrativeIntent",
                table: "SectionSceneCards");

            migrationBuilder.DropColumn(
                name: "NarrativeRole",
                table: "SectionSceneCards");
        }
    }
}
