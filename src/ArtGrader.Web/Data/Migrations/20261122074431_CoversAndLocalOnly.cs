using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGrader.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoversAndLocalOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Covers",
                table: "Images",
                type: "TEXT",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "LocalOnly",
                table: "Exams",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Covers",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "LocalOnly",
                table: "Exams");
        }
    }
}
