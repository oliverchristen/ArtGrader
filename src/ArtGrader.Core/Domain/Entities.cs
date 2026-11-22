namespace ArtGrader.Core.Domain;

public sealed class Exam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string SchoolClass { get; set; } = "";
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string ExerciseText { get; set; } = "";
    public StoredFile? ExerciseFile { get; set; }
    public StoredFile? RubricFile { get; set; }
    public GradingSettings Settings { get; set; } = new();
    public bool LocalOnly { get; set; }
    public List<RubricCategory> Categories { get; set; } = [];
    public List<Submission> Submissions { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public IEnumerable<RubricCriterion> OrderedCriteria() =>
        Categories.OrderBy(c => c.Order).SelectMany(c => c.Criteria.OrderBy(k => k.Order));
}

public sealed class StoredFile
{
    public string OriginalName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string ContentType { get; set; } = "";
}

public sealed class RubricCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public List<RubricCriterion> Criteria { get; set; } = [];
}

public sealed class RubricCriterion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public string Text { get; set; } = "";
    public decimal Weight { get; set; } = 1m;
    public AssessedBy AssessedBy { get; set; } = AssessedBy.Ai;
    public int Order { get; set; }
}

public sealed class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public string StudentName { get; set; } = "";
    public bool HasPlaceholderName { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Neu;
    public string? ErrorMessage { get; set; }
    public List<SubmissionImage> Images { get; set; } = [];
    public List<CriterionAssessment> Assessments { get; set; } = [];
    public string AiDescription { get; set; } = "";
    public string AiOverallComment { get; set; } = "";
    public string AiStrengths { get; set; } = "";
    public string AiNextSteps { get; set; } = "";
    public string TeacherComment { get; set; } = "";
    public decimal? FinalGradeOverride { get; set; }
    public string? ModelId { get; set; }
    public DateTimeOffset? GradedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public CriterionAssessment AssessmentFor(Guid criterionId)
    {
        var assessment = Assessments.FirstOrDefault(a => a.CriterionId == criterionId);
        if (assessment is null)
        {
            assessment = new CriterionAssessment { SubmissionId = Id, CriterionId = criterionId };
            Assessments.Add(assessment);
        }
        return assessment;
    }
}

public sealed class SubmissionImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public string OriginalName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public int Order { get; set; }
    public List<CoverArea> Covers { get; set; } = [];
}

public sealed class CoverArea
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class CriterionAssessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid CriterionId { get; set; }
    public Level? AiLevel { get; set; }
    public string AiJustification { get; set; } = "";
    public Confidence? AiConfidence { get; set; }
    public Level? TeacherLevel { get; set; }

    public Level? EffectiveLevel => TeacherLevel ?? AiLevel;
}
