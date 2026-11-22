using System.Text.RegularExpressions;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Localization;

namespace ArtGrader.Core.Tests;

/// <summary>Switching the app language is process-wide, so these tests must not run beside others.</summary>
[CollectionDefinition(nameof(LanguageSwitching), DisableParallelization = true)]
public sealed class LanguageSwitching;

[Collection(nameof(LanguageSwitching))]
public partial class LocalizationTests
{
    [Fact]
    public void Every_text_in_the_code_has_an_english_translation()
    {
        var missing = TextsInCode()
            .Where(t => !Texts.EnglishTexts.ContainsKey(t.Text))
            .Select(t => $"{t.File}: «{t.Text}»")
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, "Missing English texts:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Texts_are_looked_up_with_a_fixed_german_text_not_an_interpolated_one()
    {
        var interpolated = SourceFiles()
            .SelectMany(f => InterpolatedLookup().Matches(File.ReadAllText(f)).Select(m => $"{Path.GetFileName(f)}: {m.Value}"))
            .ToList();

        Assert.True(interpolated.Count == 0, "Use T(\"… {0} …\", value) instead of T($\"…\"):\n" + string.Join("\n", interpolated));
    }

    [Fact]
    public void Translations_keep_the_placeholders_of_the_german_text()
    {
        var broken = Texts.EnglishTexts
            .Where(e => !Placeholders(e.Key).SetEquals(Placeholders(e.Value)))
            .Select(e => $"«{e.Key}» → «{e.Value}»")
            .ToList();

        Assert.True(broken.Count == 0, "Placeholders differ:\n" + string.Join("\n", broken));
    }

    [Fact]
    public void No_german_text_has_two_different_translations()
    {
        _ = Texts.EnglishTexts;
        Assert.True(Texts.Conflicts.Count == 0, "Conflicting translations:\n" + string.Join("\n", Texts.Conflicts));
    }

    [Fact]
    public void The_ai_writes_in_the_app_language()
    {
        try
        {
            Language.Current = AppLanguage.De;
            Assert.Contains("Schweizer Rechtschreibung", Prompts.GradingSystem);

            Language.Current = AppLanguage.En;
            Assert.Contains("Englisch (britische Rechtschreibung)", Prompts.GradingSystem);
            Assert.DoesNotContain("Schweizer Rechtschreibung", Prompts.GradingSystem);
        }
        finally
        {
            Language.Current = AppLanguage.De;
        }
    }

    [Fact]
    public void German_is_shown_as_written_and_english_is_looked_up()
    {
        var (german, english) = Texts.EnglishTexts.First();
        try
        {
            Language.Current = AppLanguage.De;
            Assert.Equal(german, Texts.T(german));

            Language.Current = AppLanguage.En;
            Assert.Equal(english, Texts.T(german));
        }
        finally
        {
            Language.Current = AppLanguage.De;
        }
    }

    private static HashSet<string> Placeholders(string text) => Placeholder().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    private static IEnumerable<(string File, string Text)> TextsInCode() =>
        SourceFiles().SelectMany(file => Lookup().Matches(File.ReadAllText(file))
            .Select(m => (Path.GetFileName(file), Unescape(m.Groups[1].Value))));

    private static string Unescape(string literal) =>
        literal.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");

    private static IEnumerable<string> SourceFiles()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        return Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArtGrader.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    // T("…") with a plain string literal; the German text is the key.
    [GeneratedRegex("""\bT\(\s*"((?:[^"\\]|\\.)*)"\s*[,)]""")]
    private static partial Regex Lookup();

    [GeneratedRegex("""\bT\(\s*\$""")]
    private static partial Regex InterpolatedLookup();

    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();
}
