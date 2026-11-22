using ArtGrader.Core.Domain;

namespace ArtGrader.Web.Services;

public sealed class CurrentExam
{
    public Exam? Value { get; private set; }

    private readonly Dictionary<Guid, int> lastSteps = [];

    public event Action? Changed;

    /// <summary>The step last shown for an exam in this browser tab, so returning from a work opens the same step.</summary>
    public int? LastStep(Guid examId) => lastSteps.TryGetValue(examId, out var step) ? step : null;

    public void RememberStep(Guid examId, int step) => lastSteps[examId] = step;

    public void Set(Exam? exam)
    {
        Value = exam;
        Changed?.Invoke();
    }

    public void Clear(Guid examId)
    {
        if (Value?.Id == examId)
        {
            Set(null);
        }
    }
}
