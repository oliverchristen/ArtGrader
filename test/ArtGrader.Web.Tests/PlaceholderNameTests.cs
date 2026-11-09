using ArtGrader.Core.Domain;
using ArtGrader.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtGrader.Web.Tests;

public sealed class PlaceholderNameTests : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"artgrader-test-{Guid.NewGuid():N}.db");
    private Factory factory = null!;
    private ExamRepository repository = null!;
    private Exam exam = null!;

    private sealed class Factory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }

    public async Task InitializeAsync()
    {
        factory = new Factory(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={databasePath};Pooling=False").Options);
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        exam = new Exam { Title = "Stadt von oben" };
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        repository = new ExamRepository(factory);
    }

    public Task DisposeAsync()
    {
        File.Delete(databasePath);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task New_works_without_a_name_get_distinct_random_names()
    {
        var first = await repository.AddSubmissionAsync(exam.Id, null, [Image("Lena Muster.jpg")]);
        var second = await repository.AddSubmissionAsync(exam.Id, null, [Image("IMG_1.jpg")]);

        Assert.True(first.HasPlaceholderName);
        Assert.Contains(first.StudentName, Pseudonyms.All);
        Assert.NotEqual(first.StudentName, second.StudentName);
        Assert.DoesNotContain("Lena", first.StudentName);
    }

    [Fact]
    public async Task A_name_typed_by_the_teacher_is_kept_and_clearing_it_gives_a_new_random_name()
    {
        var work = await repository.AddSubmissionAsync(exam.Id, null, [Image("a.jpg")]);

        var typed = await repository.RenameSubmissionAsync(work.Id, "  Lena Muster ");
        Assert.Equal(("Lena Muster", false), typed);

        var cleared = await repository.RenameSubmissionAsync(work.Id, " ");
        Assert.True(cleared.Placeholder);
        Assert.Contains(cleared.Name, Pseudonyms.All);
    }

    [Fact]
    public async Task Names_taken_from_file_names_by_earlier_versions_are_replaced_once()
    {
        var fromFile = await repository.AddSubmissionAsync(exam.Id, "Lena Muster", [Image("Lena Muster.jpeg")]);
        var numbered = await repository.AddSubmissionAsync(exam.Id, "Arbeit 3", [Image("x.jpg")]);
        var real = await repository.AddSubmissionAsync(exam.Id, "Noah Beispiel", [Image("IMG_2.jpg")]);

        Assert.Equal(2, await repository.ReplaceGeneratedNamesAsync());
        Assert.Equal(0, await repository.ReplaceGeneratedNamesAsync());

        var stored = (await repository.GetAsync(exam.Id))!.Submissions.ToDictionary(s => s.Id);
        Assert.True(stored[fromFile.Id].HasPlaceholderName);
        Assert.Contains(stored[fromFile.Id].StudentName, Pseudonyms.All);
        Assert.True(stored[numbered.Id].HasPlaceholderName);
        Assert.Equal("Noah Beispiel", stored[real.Id].StudentName);
        Assert.False(stored[real.Id].HasPlaceholderName);
    }

    private static SubmissionImage Image(string originalName) => new() { OriginalName = originalName, RelativePath = $"x/{Guid.NewGuid():N}.jpg" };
}
