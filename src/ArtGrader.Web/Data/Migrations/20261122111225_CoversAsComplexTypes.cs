using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtGrader.Web.Data.Migrations;

/// <inheritdoc />
public partial class _20261122111225_CoversAsComplexTypes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"Pages\" SET \"Covers\" = '[]' WHERE \"Covers\" IS NULL;");
        migrationBuilder.Sql("UPDATE \"Images\" SET \"Covers\" = '[]' WHERE \"Covers\" IS NULL;");

        migrationBuilder.AlterColumn<string>(
            name: "Covers",
            table: "Pages",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Covers",
            table: "Images",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Covers",
            table: "Pages",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT");

        migrationBuilder.AlterColumn<string>(
            name: "Covers",
            table: "Images",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT");
    }
}
