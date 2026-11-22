using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGrader.Web.Data.Migrations;

/// <inheritdoc />
public partial class _20261122160707_RepairQuotedModelIds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Claude model ids were stored with JSON quotes ("claude-…"), so the previous migration took them for local models.
        migrationBuilder.Sql("UPDATE Submissions SET ModelId = trim(ModelId, '\"') WHERE ModelId LIKE '\"%'");
        migrationBuilder.Sql("UPDATE Submissions SET ModelProvider = 0 WHERE ModelId LIKE 'claude%'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
