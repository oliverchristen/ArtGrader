using ArtGrader.Core.Domain;
using ArtGrader.Core.Grading;

namespace ArtGrader.Core.Tests;

public class GradeCalculatorTests
{
    private static readonly GradingSettings Defaults = new();

    private static GradeInput[] Levels(params Level?[] levels) =>
        levels.Select(l => new GradeInput(Guid.NewGuid(), 1m, l)).ToArray();

    [Fact]
    public void All_criteria_exceeded_gives_six()
    {
        var result = GradeCalculator.Calculate(Levels(Level.Uebertroffen, Level.Uebertroffen, Level.Uebertroffen), Defaults);
        Assert.Equal(6m, result.Grade);
    }

    [Fact]
    public void All_criteria_not_met_gives_one()
    {
        var result = GradeCalculator.Calculate(Levels(Level.Nicht, Level.Nicht), Defaults);
        Assert.Equal(1m, result.Grade);
    }

    [Fact]
    public void Twenty_two_of_thirty_points_gives_four_and_a_half()
    {
        var levels = Enumerable.Repeat<Level?>(Level.Erfuellt, 8).Concat([Level.Uebertroffen, Level.Uebertroffen]).ToArray();
        var result = GradeCalculator.Calculate(Levels(levels), Defaults);

        Assert.Equal(22m, result.Points);
        Assert.Equal(30m, result.MaxPoints);
        Assert.Equal(4.5m, result.Grade);
    }

    [Fact]
    public void All_criteria_met_gives_four_and_a_half_when_rounded_to_halves()
    {
        var result = GradeCalculator.Calculate(Levels(Level.Erfuellt, Level.Erfuellt, Level.Erfuellt), Defaults);
        Assert.Equal(4.5m, result.Grade);
    }

    [Fact]
    public void Weights_change_the_share_of_a_criterion()
    {
        var inputs = new[]
        {
            new GradeInput(Guid.NewGuid(), 3m, Level.Uebertroffen),
            new GradeInput(Guid.NewGuid(), 1m, Level.Nicht),
        };

        var result = GradeCalculator.Calculate(inputs, Defaults);

        Assert.Equal(9m, result.Points);
        Assert.Equal(12m, result.MaxPoints);
        Assert.Equal(5m, result.Grade);
    }

    [Theory]
    [InlineData("0.5", "4.5")]
    [InlineData("0.25", "4.25")]
    [InlineData("0.1", "4.3")]
    public void Rounding_step_is_applied(string step, string expectedGrade)
    {
        var settings = new GradingSettings { RoundingStep = decimal.Parse(step, System.Globalization.CultureInfo.InvariantCulture) };
        var expected = decimal.Parse(expectedGrade, System.Globalization.CultureInfo.InvariantCulture);
        var result = GradeCalculator.Calculate(Levels(Level.Erfuellt, Level.Erfuellt, Level.Erfuellt, Level.Erfuellt, Level.Erfuellt, Level.Erfuellt), settings);
        Assert.Equal(12m, result.Points);
        Assert.Equal(expected, result.Grade);
    }

    [Fact]
    public void Missing_level_gives_no_grade_yet()
    {
        var result = GradeCalculator.Calculate(Levels(Level.Erfuellt, null, Level.Uebertroffen), Defaults);

        Assert.Null(result.Grade);
        Assert.Equal(1, result.MissingCount);
        Assert.False(result.IsComplete);
    }

    [Fact]
    public void Provisional_grade_uses_only_assessed_criteria()
    {
        var result = GradeCalculator.Calculate(Levels(Level.Uebertroffen, null, Level.Erfuellt), Defaults);

        Assert.Null(result.Grade);
        Assert.Equal(5m, result.Provisional);
    }

    [Fact]
    public void Nothing_assessed_gives_no_provisional_grade()
    {
        Assert.Null(GradeCalculator.Calculate(Levels(null, null), Defaults).Provisional);
    }

    [Fact]
    public void Teacher_level_overrides_ai_level()
    {
        var exam = new Exam();
        var category = new RubricCategory();
        var criterion = new RubricCriterion();
        category.Criteria.Add(criterion);
        exam.Categories.Add(category);
        var submission = new Submission();
        var assessment = submission.AssessmentFor(criterion.Id);
        assessment.AiLevel = Level.Nicht;
        assessment.TeacherLevel = Level.Uebertroffen;

        Assert.Equal(6m, GradeCalculator.Calculate(exam, submission).Grade);
    }
}
