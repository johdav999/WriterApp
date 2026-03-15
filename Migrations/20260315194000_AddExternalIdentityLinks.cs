using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp.Migrations
{
    public partial class AddExternalIdentityLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalIdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Issuer = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ObjectIdentifier = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    EmailAtLinkTime = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentityLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_EmailAtLinkTime",
                table: "ExternalIdentityLinks",
                column: "EmailAtLinkTime");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_Provider_Issuer_Subject_ObjectIdentifier",
                table: "ExternalIdentityLinks",
                columns: new[] { "Provider", "Issuer", "Subject", "ObjectIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_UserId",
                table: "ExternalIdentityLinks",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalIdentityLinks");
        }
    }
}
