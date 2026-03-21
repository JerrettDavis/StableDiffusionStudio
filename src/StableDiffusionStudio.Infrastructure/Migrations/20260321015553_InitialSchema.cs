using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StableDiffusionStudio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GenerationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GenerationPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AssociatedModelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModelFamilyFilter = table.Column<string>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    PositivePromptTemplate = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    NegativePrompt = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    Sampler = table.Column<string>(type: "TEXT", nullable: false),
                    Scheduler = table.Column<string>(type: "TEXT", nullable: false),
                    Steps = table.Column<int>(type: "INTEGER", nullable: false),
                    CfgScale = table.Column<double>(type: "REAL", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    BatchSize = table.Column<int>(type: "INTEGER", nullable: false),
                    ClipSkip = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Data = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ResultData = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Checkpoint"),
                    ModelFamily = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Checksum = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    PreviewImagePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CompatibilityHints = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DetectedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastVerifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PositivePrompt = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    NegativePrompt = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    UsedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GenerationJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Seed = table.Column<long>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    GenerationTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ContentRating = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "Unknown"),
                    NsfwScore = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    IsRevealed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedImages_GenerationJobs_GenerationJobId",
                        column: x => x.GenerationJobId,
                        principalTable: "GenerationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedImages_GenerationJobId",
                table: "GeneratedImages",
                column: "GenerationJobId");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_CreatedAt",
                table: "GenerationJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_ProjectId",
                table: "GenerationJobs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_Status",
                table: "GenerationJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationPresets_AssociatedModelId",
                table: "GenerationPresets",
                column: "AssociatedModelId");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationPresets_IsDefault",
                table: "GenerationPresets",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationPresets_ModelFamilyFilter",
                table: "GenerationPresets",
                column: "ModelFamilyFilter");

            migrationBuilder.CreateIndex(
                name: "IX_JobRecords_CorrelationId",
                table: "JobRecords",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRecords_CreatedAt",
                table: "JobRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobRecords_Status",
                table: "JobRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobRecords_Type",
                table: "JobRecords",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ModelRecords_FilePath",
                table: "ModelRecords",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelRecords_Format",
                table: "ModelRecords",
                column: "Format");

            migrationBuilder.CreateIndex(
                name: "IX_ModelRecords_ModelFamily",
                table: "ModelRecords",
                column: "ModelFamily");

            migrationBuilder.CreateIndex(
                name: "IX_ModelRecords_Source",
                table: "ModelRecords",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_ModelRecords_Status",
                table: "ModelRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ModelRecords_Type",
                table: "ModelRecords",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedAt",
                table: "Projects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_IsPinned",
                table: "Projects",
                column: "IsPinned");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status",
                table: "Projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PromptHistories_UsedAt",
                table: "PromptHistories",
                column: "UsedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneratedImages");

            migrationBuilder.DropTable(
                name: "GenerationPresets");

            migrationBuilder.DropTable(
                name: "JobRecords");

            migrationBuilder.DropTable(
                name: "ModelRecords");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "PromptHistories");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "GenerationJobs");
        }
    }
}
