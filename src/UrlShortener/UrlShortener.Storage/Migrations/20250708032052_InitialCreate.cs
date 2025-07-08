using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrlShortener.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "UrlShortener");

            migrationBuilder.CreateTable(
                name: "Links",
                schema: "UrlShortener",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ShortCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Links", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Links_CreationDate",
                schema: "UrlShortener",
                table: "Links",
                column: "CreationDate");

            migrationBuilder.CreateIndex(
                name: "IX_Links_ExpirationDate",
                schema: "UrlShortener",
                table: "Links",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_Links_IsActive",
                schema: "UrlShortener",
                table: "Links",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Links_OriginalUrl_IsActive",
                schema: "UrlShortener",
                table: "Links",
                columns: new[] { "OriginalUrl", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Links_ShortCode",
                schema: "UrlShortener",
                table: "Links",
                column: "ShortCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Links",
                schema: "UrlShortener");
        }
    }
}
