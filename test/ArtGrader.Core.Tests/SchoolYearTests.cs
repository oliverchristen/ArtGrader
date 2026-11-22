using ArtGrader.Core.Domain;

namespace ArtGrader.Core.Tests;

public class SchoolYearTests
{
    [Theory]
    [InlineData("2026-08-01", "2026/27")]
    [InlineData("2026-07-31", "2025/26")]
    [InlineData("2027-01-15", "2026/27")]
    public void School_year_starts_in_august(string date, string label)
    {
        Assert.Equal(label, SchoolYear.Label(DateOnly.Parse(date)));
    }

    [Theory]
    [InlineData("2026-03-10", "2026-07-31", false)]
    [InlineData("2026-03-10", "2026-08-01", true)]
    [InlineData("2026-11-09", "2026-11-22", false)]
    public void An_exam_is_due_once_a_new_school_year_has_started(string exam, string today, bool expected)
    {
        Assert.Equal(expected, SchoolYear.HasEnded(DateOnly.Parse(exam), DateOnly.Parse(today)));
    }
}
