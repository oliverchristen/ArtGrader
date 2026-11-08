using ArtGrader.Core.Domain;
using ArtGrader.Core.Grading;
using ClosedXML.Excel;

namespace ArtGrader.Web.Export;

public static class ExcelExporter
{
    public static byte[] Create(Exam exam)
    {
        using var workbook = new XLWorkbook();
        var criteria = exam.OrderedCriteria().ToList();
        var categoryNames = exam.Categories.SelectMany(c => c.Criteria.Select(k => (k.Id, c.Name))).ToDictionary(x => x.Id, x => x.Name);

        var sheet = workbook.Worksheets.Add("Noten");
        sheet.Cell(1, 1).Value = exam.Title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = $"{exam.SchoolClass} · {exam.Date:dd.MM.yyyy}";

        const int headerRow = 4;
        var column = 1;
        sheet.Cell(headerRow, column++).Value = "Name";
        for (var i = 0; i < criteria.Count; i++)
        {
            sheet.Cell(headerRow, column).Value = $"K{i + 1}";
            sheet.Cell(headerRow, column).GetComment().AddText($"{categoryNames[criteria[i].Id]}: {criteria[i].Text}");
            column++;
        }
        var pointsColumn = column++;
        var gradeColumn = column++;
        var statusColumn = column;
        sheet.Cell(headerRow, pointsColumn).Value = "Punkte";
        sheet.Cell(headerRow, gradeColumn).Value = "Note";
        sheet.Cell(headerRow, statusColumn).Value = "Status";
        sheet.Row(headerRow).Style.Font.Bold = true;

        var row = headerRow + 1;
        foreach (var submission in exam.Submissions)
        {
            sheet.Cell(row, 1).Value = submission.StudentName;
            for (var i = 0; i < criteria.Count; i++)
            {
                var level = submission.Assessments.FirstOrDefault(a => a.CriterionId == criteria[i].Id)?.EffectiveLevel;
                if (level is not null)
                {
                    sheet.Cell(row, i + 2).Value = LevelText.Short(level.Value);
                }
            }
            var result = GradeCalculator.Calculate(exam, submission);
            sheet.Cell(row, pointsColumn).Value = $"{result.Points:0.##} / {result.MaxPoints:0.##}";
            var grade = GradeCalculator.FinalGrade(exam, submission);
            if (grade is not null)
            {
                sheet.Cell(row, gradeColumn).Value = grade.Value;
                sheet.Cell(row, gradeColumn).Style.NumberFormat.Format = "0.0#";
            }
            sheet.Cell(row, statusColumn).Value = LevelText.Label(submission.Status);
            row++;
        }

        var table = sheet.Range(headerRow, 1, Math.Max(headerRow + 1, row - 1), statusColumn);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Columns().AdjustToContents();

        var legend = workbook.Worksheets.Add("Kriterien");
        legend.Cell(1, 1).Value = "Kürzel";
        legend.Cell(1, 2).Value = "Kategorie";
        legend.Cell(1, 3).Value = "Kriterium";
        legend.Cell(1, 4).Value = "Gewicht";
        legend.Cell(1, 5).Value = "Beurteilt durch";
        legend.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < criteria.Count; i++)
        {
            legend.Cell(i + 2, 1).Value = $"K{i + 1}";
            legend.Cell(i + 2, 2).Value = categoryNames[criteria[i].Id];
            legend.Cell(i + 2, 3).Value = criteria[i].Text;
            legend.Cell(i + 2, 4).Value = criteria[i].Weight;
            legend.Cell(i + 2, 5).Value = criteria[i].AssessedBy == AssessedBy.Teacher ? "Lehrperson" : "KI-Vorschlag";
        }
        legend.Column(3).Width = 90;
        legend.Column(3).Style.Alignment.WrapText = true;
        legend.Columns(1, 2).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
