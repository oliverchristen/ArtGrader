using ArtGrader.Core.Domain;

namespace ArtGrader.Core.Grading;

public sealed record GradeInput(Guid CriterionId, decimal Weight, Level? Level);

public sealed record GradeResult(decimal Points, decimal MaxPoints, decimal? Grade, int MissingCount)
{
    public bool IsComplete => MissingCount == 0;

    public decimal? Provisional { get; init; }
}

public static class GradeCalculator
{
    public static GradeResult Calculate(IEnumerable<GradeInput> inputs, GradingSettings settings)
    {
        var list = inputs.Where(i => i.Weight > 0).ToList();
        var maxPoints = list.Sum(i => i.Weight * settings.MaxLevelPoints);
        var points = list.Where(i => i.Level is not null).Sum(i => i.Weight * settings.PointsFor(i.Level!.Value));
        var missing = list.Count(i => i.Level is null);

        if (maxPoints <= 0 || missing > 0)
        {
            var assessedMax = list.Where(i => i.Level is not null).Sum(i => i.Weight * settings.MaxLevelPoints);
            var provisional = assessedMax > 0 ? ToGrade(points, assessedMax, settings) : (decimal?)null;
            return new GradeResult(points, maxPoints, null, missing) { Provisional = provisional };
        }

        var grade = ToGrade(points, maxPoints, settings);
        return new GradeResult(points, maxPoints, grade, 0) { Provisional = grade };
    }

    public static GradeResult Calculate(Exam exam, Submission submission) =>
        Calculate(
            exam.OrderedCriteria()
                .Select(c => new GradeInput(
                    c.Id,
                    c.Weight,
                    submission.Assessments.FirstOrDefault(a => a.CriterionId == c.Id)?.EffectiveLevel)),
            exam.Settings);

    public static decimal ToGrade(decimal points, decimal maxPoints, GradingSettings settings)
    {
        var span = settings.MaxGrade - settings.MinGrade;
        var raw = settings.MinGrade + points / maxPoints * span;
        return Round(Math.Clamp(raw, settings.MinGrade, settings.MaxGrade), settings.RoundingStep);
    }

    public static decimal Round(decimal value, decimal step)
    {
        if (step <= 0)
        {
            return value;
        }
        return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
    }

    public static decimal? FinalGrade(Exam exam, Submission submission) =>
        submission.FinalGradeOverride ?? Calculate(exam, submission).Grade;
}
