using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGrader.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DocumentsLessonAndCrop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Crop",
                table: "Images",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LessonFile_ContentType",
                table: "Exams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LessonFile_OriginalName",
                table: "Exams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LessonFile_RelativePath",
                table: "Exams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LessonText",
                table: "Exams",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RubricText",
                table: "Exams",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    Covers = table.Column<string>(type: "TEXT", nullable: true),
                    Crop = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pages_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pages_ExamId",
                table: "Pages",
                column: "ExamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pages");

            migrationBuilder.DropColumn(
                name: "Crop",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "LessonFile_ContentType",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "LessonFile_OriginalName",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "LessonFile_RelativePath",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "LessonText",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "RubricText",
                table: "Exams");
        }
    }
}
