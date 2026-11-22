using ArtGrader.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtGrader.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<RubricCategory> Categories => Set<RubricCategory>();
    public DbSet<RubricCriterion> Criteria => Set<RubricCriterion>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionImage> Images => Set<SubmissionImage>();
    public DbSet<CriterionAssessment> Assessments => Set<CriterionAssessment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Exam>(exam =>
        {
            exam.OwnsOne(e => e.Settings);
            exam.OwnsOne(e => e.ExerciseFile);
            exam.OwnsOne(e => e.RubricFile);
            exam.Property(e => e.CreatedAt).HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            exam.HasMany(e => e.Categories).WithOne().HasForeignKey(c => c.ExamId).OnDelete(DeleteBehavior.Cascade);
            exam.HasMany(e => e.Submissions).WithOne().HasForeignKey(s => s.ExamId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RubricCategory>()
            .HasMany(c => c.Criteria)
            .WithOne()
            .HasForeignKey(k => k.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Submission>(submission =>
        {
            submission.HasMany(s => s.Images).WithOne().HasForeignKey(i => i.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            submission.HasMany(s => s.Assessments).WithOne().HasForeignKey(a => a.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            submission.Property(s => s.CreatedAt).HasConversion(v => v.ToUnixTimeMilliseconds(), v => DateTimeOffset.FromUnixTimeMilliseconds(v));
            submission.Property(s => s.GradedAt).HasConversion(
                v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : (long?)null,
                v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);
        });

        modelBuilder.Entity<SubmissionImage>().OwnsMany(i => i.Covers, covers => covers.ToJson());

        modelBuilder.Entity<CriterionAssessment>(assessment =>
        {
            assessment.HasOne<RubricCriterion>().WithMany().HasForeignKey(a => a.CriterionId).OnDelete(DeleteBehavior.Cascade);
            assessment.Ignore(a => a.EffectiveLevel);
        });
    }
}
