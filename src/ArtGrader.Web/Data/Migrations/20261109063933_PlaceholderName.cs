using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGrader.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlaceholderName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasPlaceholderName",
                table: "Submissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasPlaceholderName",
                table: "Submissions");
        }
    }
}
