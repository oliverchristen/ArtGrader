using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGrader.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Exams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    SchoolClass = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExerciseText = table.Column<string>(type: "TEXT", nullable: false),
                    ExerciseFile_OriginalName = table.Column<string>(type: "TEXT", nullable: true),
                    ExerciseFile_RelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    ExerciseFile_ContentType = table.Column<string>(type: "TEXT", nullable: true),
                    RubricFile_OriginalName = table.Column<string>(type: "TEXT", nullable: true),
                    RubricFile_RelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    RubricFile_ContentType = table.Column<string>(type: "TEXT", nullable: true),
                    Settings_PointsUebertroffen = table.Column<int>(type: "INTEGER", nullable: false),
                    Settings_PointsErfuellt = table.Column<int>(type: "INTEGER", nullable: false),
                    Settings_PointsTeilweise = table.Column<int>(type: "INTEGER", nullable: false),
                    Settings_PointsNicht = table.Column<int>(type: "INTEGER", nullable: false),
                    Settings_RoundingStep = table.Column<decimal>(type: "TEXT", nullable: false),
                    Settings_MinGrade = table.Column<decimal>(type: "TEXT", nullable: false),
                    Settings_MaxGrade = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StudentName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    AiDescription = table.Column<string>(type: "TEXT", nullable: false),
                    AiOverallComment = table.Column<string>(type: "TEXT", nullable: false),
                    AiStrengths = table.Column<string>(type: "TEXT", nullable: false),
                    AiNextSteps = table.Column<string>(type: "TEXT", nullable: false),
                    TeacherComment = table.Column<string>(type: "TEXT", nullable: false),
                    FinalGradeOverride = table.Column<decimal>(type: "TEXT", nullable: true),
                    ModelId = table.Column<string>(type: "TEXT", nullable: true),
                    GradedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submissions_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Criteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Weight = table.Column<decimal>(type: "TEXT", nullable: false),
                    AssessedBy = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Criteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Criteria_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalName = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CriterionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    AiJustification = table.Column<string>(type: "TEXT", nullable: false),
                    AiConfidence = table.Column<int>(type: "INTEGER", nullable: true),
                    TeacherLevel = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assessments_Criteria_CriterionId",
                        column: x => x.CriterionId,
                        principalTable: "Criteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assessments_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_CriterionId",
                table: "Assessments",
                column: "CriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_SubmissionId",
                table: "Assessments",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ExamId",
                table: "Categories",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_Criteria_CategoryId",
                table: "Criteria",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_SubmissionId",
                table: "Images",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ExamId",
                table: "Submissions",
                column: "ExamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assessments");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "Criteria");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Exams");
        }
    }
}
