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
    public static TheoryData<AppLanguage> TranslatedLanguages => new(Texts.TranslatedLanguages);

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void Every_text_in_the_code_is_translated(AppLanguage language)
    {
        var translations = Texts.TranslationsOf(language);
        var missing = TextsInCode()
            .Where(t => !translations.ContainsKey(t.Text))
            .Select(t => $"{t.File}: «{t.Text}»")
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, $"Missing {language} texts:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void All_languages_translate_the_same_texts()
    {
        var german = Texts.GermanTexts.Keys.ToHashSet();
        foreach (var language in Texts.TranslatedLanguages)
        {
            var keys = Texts.TranslationsOf(language).Keys.ToHashSet();
            Assert.True(keys.SetEquals(german), $"{language} differs from German: " + string.Join(", ", keys.Except(german).Concat(german.Except(keys)).Take(10)));
        }
    }

    [Fact]
    public void Texts_are_looked_up_with_a_fixed_english_text_not_an_interpolated_one()
    {
        var interpolated = SourceFiles()
            .SelectMany(f => InterpolatedLookup().Matches(File.ReadAllText(f)).Select(m => $"{Path.GetFileName(f)}: {m.Value}"))
            .ToList();

        Assert.True(interpolated.Count == 0, "Use T(\"… {0} …\", value) instead of T($\"…\"):\n" + string.Join("\n", interpolated));
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void Translations_keep_the_placeholders_of_the_english_text(AppLanguage language)
    {
        var broken = Texts.TranslationsOf(language)
            .Where(e => !Placeholders(e.Key).SetEquals(Placeholders(e.Value)))
            .Select(e => $"«{e.Key}» → «{e.Value}»")
            .ToList();

        Assert.True(broken.Count == 0, "Placeholders differ:\n" + string.Join("\n", broken));
    }

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void No_english_text_has_two_different_translations(AppLanguage language)
    {
        var conflicts = Texts.ConflictsOf(language);
        Assert.True(conflicts.Count == 0, "Conflicting translations:\n" + string.Join("\n", conflicts));
    }

    [Fact]
    public void Every_language_has_its_swiss_formats()
    {
        Assert.Equal(["de-CH", "fr-CH", "it-CH", "rm-CH", "en-GB"], Language.All.Select(l => Language.CultureOf(l.Language).Name));
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

            Language.Current = AppLanguage.Fr;
            Assert.Contains("auf Französisch", Prompts.GradingSystem);

            Language.Current = AppLanguage.It;
            Assert.Contains("auf Italienisch", Prompts.GradingSystem);

            Language.Current = AppLanguage.Rm;
            Assert.Contains("auf Rumantsch Grischun", Prompts.GradingSystem);
        }
        finally
        {
            Language.Current = AppLanguage.De;
        }
    }

    [Fact]
    public void English_is_shown_as_written_and_other_languages_are_looked_up()
    {
        var english = Texts.GermanTexts.Keys.First();
        try
        {
            Language.Current = AppLanguage.En;
            Assert.Equal(english, Texts.T(english));

            foreach (var language in Texts.TranslatedLanguages)
            {
                Language.Current = language;
                Assert.Equal(Texts.TranslationsOf(language)[english], Texts.T(english));
            }
        }
        finally
        {
            Language.Current = AppLanguage.De;
        }
    }

    [Fact]
    public void Random_names_exist_in_every_language()
    {
        try
        {
            foreach (var (language, _) in Language.All)
            {
                Language.Current = language;
                Assert.Equal(384, Domain.Pseudonyms.All.Distinct().Count());
            }
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

    // T("…") with a plain string literal; the English text is the key.
    [GeneratedRegex("""\bT\(\s*"((?:[^"\\]|\\.)*)"\s*[,)]""")]
    private static partial Regex Lookup();

    [GeneratedRegex("""\bT\(\s*\$""")]
    private static partial Regex InterpolatedLookup();

    [GeneratedRegex(@"\{(\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();
}
