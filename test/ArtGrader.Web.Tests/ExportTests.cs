using ArtGrader.Core.Domain;
using ArtGrader.Web.Export;
using ClosedXML.Excel;

namespace ArtGrader.Web.Tests;

public class ExportTests
{
    static ExportTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static Exam ExampleExam()
    {
        var exam = new Exam { Title = "Stadt von oben", SchoolClass = "2b" };
        var perspective = new RubricCategory { Name = "Perspektivisches Zeichnen", Order = 0 };
        perspective.Criteria.Add(new RubricCriterion { Text = "Die Zeichnung ist perspektivisch korrekt.", Order = 0 });
        perspective.Criteria.Add(new RubricCriterion { Text = "Die Standflächen der Häuser sind sinnvoll.", Order = 1 });
        var process = new RubricCategory { Name = "Prozess und Engagement", Order = 1 };
        process.Criteria.Add(new RubricCriterion { Text = "Mit Ausdauer gearbeitet.", AssessedBy = AssessedBy.Teacher });
        exam.Categories.AddRange([perspective, process]);

        var complete = new Submission { StudentName = "Zoë Müller", TeacherComment = "Sehr «räumlich» – toll gemacht! Nächstes Mal: Dächer “detailreicher”, z. B. Pools…" };
        var criteria = exam.OrderedCriteria().ToList();
        var first = complete.AssessmentFor(criteria[0].Id);
        first.AiLevel = Level.Erfuellt;
        first.AiJustification = "Die Fluchtlinien laufen sauber zum Fluchtpunkt; nur unten rechts weichen sie ab.";
        complete.AssessmentFor(criteria[1].Id).AiLevel = Level.Uebertroffen;
        complete.AssessmentFor(criteria[2].Id).TeacherLevel = Level.Erfuellt;

        var open = new Submission { StudentName = "Noah" };
        open.AssessmentFor(criteria[0].Id).AiLevel = Level.Teilweise;
        exam.Submissions.AddRange([complete, open]);
        return exam;
    }

    [Fact]
    public void Pdf_renders_german_typography_with_the_bundled_font()
    {
        var pdf = FeedbackPdf.Create(ExampleExam(), ExampleExam().Submissions, includeJustifications: true);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Excel_lists_every_student_with_grade_and_open_grade_as_blank()
    {
        var exam = ExampleExam();

        using var workbook = new XLWorkbook(new MemoryStream(ExcelExporter.Create(exam)));
        var sheet = workbook.Worksheet("Noten");

        Assert.Equal("Name", sheet.Cell(4, 1).GetString());
        Assert.Equal("K3", sheet.Cell(4, 4).GetString());
        var rows = Enumerable.Range(5, 2).ToDictionary(r => sheet.Cell(r, 1).GetString(), r => sheet.Row(r));
        Assert.Equal(5.0, rows["Zoë Müller"].Cell(6).GetDouble());
        Assert.True(rows["Noah"].Cell(6).IsEmpty());
        Assert.Equal(3, workbook.Worksheet("Kriterien").RowsUsed().Count() - 1);
    }
}
