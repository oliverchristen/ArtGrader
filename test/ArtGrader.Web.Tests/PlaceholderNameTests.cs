using ArtGrader.Core.Domain;
using ArtGrader.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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

    [Fact]
    public async Task The_header_saves_class_and_date_but_never_an_empty_title()
    {
        var date = new DateOnly(2027, 3, 4);
        Assert.Equal("Stadt bei Nacht", await repository.SaveExamHeaderAsync(exam.Id, "  Stadt bei Nacht ", " 2b ", date));
        Assert.Equal("Stadt bei Nacht", await repository.SaveExamHeaderAsync(exam.Id, "   ", "2b", date));

        var stored = (await repository.GetAsync(exam.Id))!;
        Assert.Equal("Stadt bei Nacht", stored.Title);
        Assert.Equal("2b", stored.SchoolClass);
        Assert.Equal(date, stored.Date);
    }

    [Fact]
    public async Task Covers_and_the_local_only_flag_are_stored()
    {
        var work = await repository.AddSubmissionAsync(exam.Id, null, [Image("a.jpg")]);

        await repository.SetImageEditsAsync(work.Images[0].Id, [new CoverArea { X = 0.7, Y = 0.8, Width = 0.3, Height = 0.2 }], new CoverArea { X = 0.1, Y = 0.1, Width = 0.8, Height = 0.8 });
        await repository.SetLocalOnlyAsync(exam.Id, localOnly: true);

        var stored = (await repository.GetAsync(exam.Id))!;
        Assert.True(stored.LocalOnly);
        var cover = Assert.Single(stored.Submissions.Single().Images.Single().Covers);
        Assert.Equal(0.7, cover.X, 6);
        Assert.Equal(0.2, cover.Height, 6);
        Assert.Equal(0.8, stored.Submissions.Single().Images.Single().Crop!.Width, 6);
    }

    [Fact]
    public async Task Images_without_covers_from_older_versions_load_after_the_upgrade()
    {
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20261122090653_DocumentsLessonAndCrop");
            db.Exams.Add(exam = new Exam { Title = "Alt" });
            await db.SaveChangesAsync();
            // The current model writes the work, so columns of later migrations must exist for the insert.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Submissions ADD COLUMN ModelProvider INTEGER");
        }
        var work = await repository.AddSubmissionAsync(exam.Id, null, [Image("a.jpg")]);
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Submissions DROP COLUMN ModelProvider");
            await db.Database.ExecuteSqlRawAsync("UPDATE Images SET Covers = NULL");
            await db.Database.MigrateAsync();
        }

        var image = (await repository.GetAsync(exam.Id))!.Submissions.Single(s => s.Id == work.Id).Images.Single();

        Assert.Empty(image.Covers);
        Assert.Null(image.Crop);
    }

    [Fact]
    public async Task Deleting_photos_keeps_grades_and_other_exams()
    {
        var work = await repository.AddSubmissionAsync(exam.Id, "Lena Muster", [Image("a.jpg"), Image("b.jpg")]);
        var other = new Exam { Title = "Andere" };
        await using (var db = factory.CreateDbContext())
        {
            db.Exams.Add(other);
            await db.SaveChangesAsync();
        }
        await repository.AddSubmissionAsync(other.Id, null, [Image("c.jpg")]);

        var deleted = await repository.DeletePhotosAsync([exam.Id]);

        Assert.Equal(2, deleted.Count);
        var stored = (await repository.GetAsync(exam.Id))!.Submissions.Single();
        Assert.Equal("Lena Muster", stored.StudentName);
        Assert.Empty(stored.Images);
        var summaries = await repository.ListAsync();
        Assert.Equal(0, summaries.Single(e => e.Id == exam.Id).PhotoCount);
        Assert.Equal(1, summaries.Single(e => e.Id == other.Id).PhotoCount);
    }

    [Fact]
    public async Task A_merged_photo_can_be_split_off_into_its_own_work_again()
    {
        var first = await repository.AddSubmissionAsync(exam.Id, "Lena Muster", [Image("a.jpg")]);
        var second = await repository.AddSubmissionAsync(exam.Id, null, [Image("b.jpg")]);
        await repository.MergeSubmissionsAsync(first.Id, [second.Id]);
        var merged = (await repository.GetAsync(exam.Id))!.Submissions.Single();
        Assert.Equal(2, merged.Images.Count);

        var split = await repository.SplitImageAsync(merged.Images[1].Id);

        Assert.NotNull(split);
        Assert.True(split.HasPlaceholderName);
        var stored = (await repository.GetAsync(exam.Id))!.Submissions.ToDictionary(s => s.Id);
        Assert.Equal(["a.jpg"], stored[first.Id].Images.Select(i => i.OriginalName));
        Assert.Equal(0, stored[first.Id].Images.Single().Order);
        Assert.Equal(["b.jpg"], stored[split.Id].Images.Select(i => i.OriginalName));
        Assert.Equal("Lena Muster", stored[first.Id].StudentName);
        Assert.NotEqual(stored[first.Id].StudentName, stored[split.Id].StudentName);
    }

    [Fact]
    public async Task Points_in_the_wrong_order_are_not_saved()
    {
        Assert.False(await repository.UpdateGradingSettingsAsync(exam.Id, new GradingSettings { PointsUebertroffen = 1, PointsErfuellt = 5, PointsTeilweise = 4, PointsNicht = 3 }));
        Assert.Equal(6, (await repository.GetAsync(exam.Id))!.Settings.PointsUebertroffen);

        Assert.True(await repository.UpdateGradingSettingsAsync(exam.Id, new GradingSettings { PointsUebertroffen = 10, PointsErfuellt = 5, PointsTeilweise = 4, PointsNicht = 3 }));
        Assert.Equal(10, (await repository.GetAsync(exam.Id))!.Settings.PointsUebertroffen);
    }

    [Fact]
    public async Task The_only_photo_of_a_work_cannot_be_split_off()
    {
        var work = await repository.AddSubmissionAsync(exam.Id, null, [Image("a.jpg")]);

        Assert.Null(await repository.SplitImageAsync(work.Images[0].Id));
        Assert.Single((await repository.GetAsync(exam.Id))!.Submissions);
    }

    private static SubmissionImage Image(string originalName) => new() { OriginalName = originalName, RelativePath = $"x/{Guid.NewGuid():N}.jpg" };
}
