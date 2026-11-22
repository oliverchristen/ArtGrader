using ArtGrader.Core.Domain;

namespace ArtGrader.Web.Services;

public sealed class CurrentExam
{
    public Exam? Value { get; private set; }

    public event Action? Changed;

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
