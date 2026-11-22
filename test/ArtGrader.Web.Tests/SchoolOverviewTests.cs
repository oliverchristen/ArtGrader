namespace ArtGrader.Web.Tests;

public class SchoolOverviewTests
{
    [Fact]
    public void The_overview_for_school_management_is_built_into_the_app()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("schulleitung.html");

        Assert.NotNull(stream);
        var html = new StreamReader(stream).ReadToEnd();
        Assert.Contains("<title>ArtGrader für die Schulleitung</title>", html);
    }
}
