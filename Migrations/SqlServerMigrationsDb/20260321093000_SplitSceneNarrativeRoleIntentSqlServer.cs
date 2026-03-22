using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    public partial class SplitSceneNarrativeRoleIntentSqlServer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NarrativeIntent",
                table: "SceneCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrativeRole",
                table: "SceneCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrativeIntent",
                table: "SectionSceneCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrativeRole",
                table: "SectionSceneCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [dbo].[SceneCards]
                SET
                    [NarrativeRole] = CASE LOWER(LTRIM(RTRIM(COALESCE([NarrativePurpose], ''))))
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
                    [NarrativeIntent] = CASE
                        WHEN LOWER(LTRIM(RTRIM(COALESCE([NarrativePurpose], '')))) IN (
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
                        WHEN LTRIM(RTRIM(COALESCE([NarrativePurpose], ''))) = '' THEN NULL
                        ELSE LTRIM(RTRIM([NarrativePurpose]))
                    END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [dbo].[SectionSceneCards]
                SET
                    [NarrativeRole] = CASE LOWER(LTRIM(RTRIM(COALESCE([NarrativePurpose], ''))))
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
                    [NarrativeIntent] = CASE
                        WHEN LOWER(LTRIM(RTRIM(COALESCE([NarrativePurpose], '')))) IN (
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
                        WHEN LTRIM(RTRIM(COALESCE([NarrativePurpose], ''))) = '' THEN NULL
                        ELSE LTRIM(RTRIM([NarrativePurpose]))
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
