using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWizardProjectManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentWizardStep",
                table: "ProjectStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContentBlobs",
                columns: table => new
                {
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastReferencedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReferenceCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBlobs", x => x.ContentHash);
                });

            migrationBuilder.CreateTable(
                name: "ProjectVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    VersionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BriefJson = table.Column<string>(type: "TEXT", nullable: true),
                    PlanSpecJson = table.Column<string>(type: "TEXT", nullable: true),
                    VoiceSpecJson = table.Column<string>(type: "TEXT", nullable: true),
                    RenderSpecJson = table.Column<string>(type: "TEXT", nullable: true),
                    TimelineJson = table.Column<string>(type: "TEXT", nullable: true),
                    BriefHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PlanHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    VoiceHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RenderHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TimelineHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    StorageSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    IsMarkedImportant = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectVersions_ProjectStates_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ProjectStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlobs_ContentType",
                table: "ContentBlobs",
                column: "ContentType");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlobs_LastReferencedAt",
                table: "ContentBlobs",
                column: "LastReferencedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlobs_ReferenceCount",
                table: "ContentBlobs",
                column: "ReferenceCount");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_CreatedAt",
                table: "ProjectVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_IsDeleted",
                table: "ProjectVersions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_IsDeleted_DeletedAt",
                table: "ProjectVersions",
                columns: new[] { "IsDeleted", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_IsMarkedImportant",
                table: "ProjectVersions",
                column: "IsMarkedImportant");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_ProjectId",
                table: "ProjectVersions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_ProjectId_CreatedAt",
                table: "ProjectVersions",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_ProjectId_VersionNumber",
                table: "ProjectVersions",
                columns: new[] { "ProjectId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_VersionType",
                table: "ProjectVersions",
                column: "VersionType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentBlobs");

            migrationBuilder.DropTable(
                name: "ProjectVersions");

            migrationBuilder.DropColumn(
                name: "CurrentWizardStep",
                table: "ProjectStates");
        }
    }
}
