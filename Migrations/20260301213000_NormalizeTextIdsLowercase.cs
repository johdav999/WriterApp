using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;

#nullable disable

namespace BlazorApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260301213000_NormalizeTextIdsLowercase")]
    public partial class NormalizeTextIdsLowercase : Migration
    {
       protected override void Up(MigrationBuilder migrationBuilder)
{
    
}

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design: lowercasing persisted textual IDs cannot be restored.
        }
    }
}
