namespace ArtGrader.Core.Domain;

public static class SchoolYear
{
    public const int FirstMonth = 8;

    public static int StartOf(DateOnly date) => date.Month >= FirstMonth ? date.Year : date.Year - 1;

    public static string Label(DateOnly date)
    {
        var start = StartOf(date);
        return $"{start}/{(start + 1) % 100:00}";
    }

    public static bool HasEnded(DateOnly examDate, DateOnly today) => StartOf(examDate) < StartOf(today);
}
