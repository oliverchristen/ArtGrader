namespace ArtGrader.Core.Domain;

/// <summary>Volksschule semesters: the first runs from August to January, the second from February to July.</summary>
public static class Semester
{
    public const int FirstSemesterStartMonth = 8;
    public const int SecondSemesterStartMonth = 2;

    public static DateOnly StartOf(DateOnly date) => date.Month switch
    {
        >= FirstSemesterStartMonth => new DateOnly(date.Year, FirstSemesterStartMonth, 1),
        >= SecondSemesterStartMonth => new DateOnly(date.Year, SecondSemesterStartMonth, 1),
        _ => new DateOnly(date.Year - 1, FirstSemesterStartMonth, 1),
    };

    public static bool HasEnded(DateOnly examDate, DateOnly today) => StartOf(examDate) < StartOf(today);
}
