using ArtGrader.Core.Domain;

namespace ArtGrader.Core.Tests;

public class SemesterTests
{
    [Theory]
    [InlineData("2026-08-01", "2026-08-01")]
    [InlineData("2027-01-31", "2026-08-01")]
    [InlineData("2027-02-01", "2027-02-01")]
    [InlineData("2027-07-31", "2027-02-01")]
    public void Semesters_start_in_august_and_february(string date, string start)
    {
        Assert.Equal(DateOnly.Parse(start), Semester.StartOf(DateOnly.Parse(date)));
    }

    [Theory]
    [InlineData("2026-03-10", "2026-07-31", false)]
    [InlineData("2026-03-10", "2026-08-01", true)]
    [InlineData("2026-11-09", "2026-11-22", false)]
    [InlineData("2026-11-09", "2027-01-31", false)]
    [InlineData("2026-11-09", "2027-02-01", true)]
    public void An_exam_is_due_once_a_new_semester_has_started(string exam, string today, bool expected)
    {
        Assert.Equal(expected, Semester.HasEnded(DateOnly.Parse(exam), DateOnly.Parse(today)));
    }
}
