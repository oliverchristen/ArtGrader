using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGrader.Web.Data.Migrations;

/// <inheritdoc />
public partial class ModelProvider : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ModelProvider",
            table: "Submissions",
            type: "INTEGER",
            nullable: true);

        // Works assessed before: Claude model ids start with "claude"; everything else ran in Ollama on this computer.
        migrationBuilder.Sql(
            "UPDATE Submissions SET ModelProvider = CASE WHEN ModelId LIKE 'claude%' THEN 0 ELSE 1 END WHERE ModelId IS NOT NULL AND ModelId <> ''");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ModelProvider",
            table: "Submissions");
    }
}
