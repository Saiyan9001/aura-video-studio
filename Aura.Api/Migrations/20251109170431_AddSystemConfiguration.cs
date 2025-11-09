using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    ValueType = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsSensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "system_configuration",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    is_setup_complete = table.Column<bool>(type: "INTEGER", nullable: false),
                    ffmpeg_path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    output_directory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configuration", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "system_configuration",
                columns: new[] { "id", "created_at", "ffmpeg_path", "is_setup_complete", "output_directory", "updated_at" },
                values: new object[] { 1, new DateTime(2025, 11, 9, 17, 4, 31, 236, DateTimeKind.Utc).AddTicks(5323), null, false, "/home/runner/AuraVideoStudio/Output", new DateTime(2025, 11, 9, 17, 4, 31, 236, DateTimeKind.Utc).AddTicks(5324) });

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_Category",
                table: "Configurations",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_Category_IsActive",
                table: "Configurations",
                columns: new[] { "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_Category_UpdatedAt",
                table: "Configurations",
                columns: new[] { "Category", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_IsActive",
                table: "Configurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_IsSensitive",
                table: "Configurations",
                column: "IsSensitive");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_UpdatedAt",
                table: "Configurations",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configurations");

            migrationBuilder.DropTable(
                name: "system_configuration");
        }
    }
}
