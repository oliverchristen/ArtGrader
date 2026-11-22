using System.Text.RegularExpressions;
using ArtGrader.Core.Localization;

namespace ArtGrader.Core.Domain;

public static partial class Pseudonyms
{
    private static readonly string[] Animals =
    [
        "Fuchs", "Kranich", "Luchs", "Otter", "Dachs", "Eule", "Reiher", "Igel", "Biber", "Falke", "Hase", "Marder",
        "Specht", "Rabe", "Möwe", "Gecko", "Tukan", "Pinguin", "Wiesel", "Zebra", "Lama", "Panda", "Koala", "Delfin",
    ];

    private static readonly string[] Pigments =
    [
        "Indigo", "Ocker", "Karmin", "Kobalt", "Siena", "Umbra", "Zinnober", "Türkis", "Magenta", "Oliv", "Smaragd",
        "Safran", "Petrol", "Lavendel", "Bernstein", "Purpur",
    ];

    private static readonly string[] EnglishAnimals =
    [
        "Fox", "Crane", "Lynx", "Otter", "Badger", "Owl", "Heron", "Hedgehog", "Beaver", "Falcon", "Hare", "Marten",
        "Woodpecker", "Raven", "Gull", "Gecko", "Toucan", "Penguin", "Weasel", "Zebra", "Llama", "Panda", "Koala", "Dolphin",
    ];

    private static readonly string[] EnglishPigments =
    [
        "Indigo", "Ochre", "Carmine", "Cobalt", "Sienna", "Umber", "Vermilion", "Turquoise", "Magenta", "Olive", "Emerald",
        "Saffron", "Teal", "Lavender", "Amber", "Purple",
    ];

    private static readonly string[] FrenchAnimals =
    [
        "Renard", "Grue", "Lynx", "Loutre", "Blaireau", "Hibou", "Héron", "Hérisson", "Castor", "Faucon", "Lièvre", "Martre",
        "Pic", "Corbeau", "Mouette", "Gecko", "Toucan", "Pingouin", "Belette", "Zèbre", "Lama", "Panda", "Koala", "Dauphin",
    ];

    private static readonly string[] FrenchPigments =
    [
        "Indigo", "Ocre", "Carmin", "Cobalt", "Sienne", "Ombre", "Vermillon", "Turquoise", "Magenta", "Olive", "Émeraude",
        "Safran", "Pétrole", "Lavande", "Ambre", "Pourpre",
    ];

    private static readonly string[] ItalianAnimals =
    [
        "Volpe", "Gru", "Lince", "Lontra", "Tasso", "Gufo", "Airone", "Riccio", "Castoro", "Falco", "Lepre", "Martora",
        "Picchio", "Corvo", "Gabbiano", "Geco", "Tucano", "Pinguino", "Donnola", "Zebra", "Lama", "Panda", "Koala", "Delfino",
    ];

    private static readonly string[] ItalianPigments =
    [
        "Indaco", "Ocra", "Carminio", "Cobalto", "Siena", "Ombra", "Vermiglio", "Turchese", "Magenta", "Oliva", "Smeraldo",
        "Zafferano", "Petrolio", "Lavanda", "Ambra", "Porpora",
    ];

    private static readonly string[] RomanshAnimals =
    [
        "Vulp", "Luf", "Urs", "Tschierv", "Capricorn", "Chamutsch", "Muntanella", "Aglia", "Corv", "Tschuetta", "Ritsch", "Lieur",
        "Falcun", "Martra", "Tais", "Lutra", "Pinguin", "Zebra", "Lama", "Panda", "Koala", "Delfin", "Tucan", "Gecko",
    ];

    private static readonly string[] RomanshPigments =
    [
        "Indigo", "Ocra", "Carmin", "Cobalt", "Siena", "Umbra", "Cinober", "Turchis", "Magenta", "Oliva", "Smaragd",
        "Safran", "Petrol", "Lavanda", "Ambra", "Purpur",
    ];

    /// <summary>All random names in the app language: «Fuchs Indigo» in German, «Indigo Fox» in English, «Renard Indigo» in French.</summary>
    public static IEnumerable<string> All => Language.Current switch
    {
        AppLanguage.En => EnglishAnimals.SelectMany(a => EnglishPigments.Select(p => $"{p} {a}")),
        AppLanguage.Fr => Combine(FrenchAnimals, FrenchPigments),
        AppLanguage.It => Combine(ItalianAnimals, ItalianPigments),
        AppLanguage.Rm => Combine(RomanshAnimals, RomanshPigments),
        _ => Combine(Animals, Pigments),
    };

    private static IEnumerable<string> Combine(string[] animals, string[] pigments) =>
        animals.SelectMany(a => pigments.Select(p => $"{a} {p}"));

    public static string Next(IEnumerable<string> taken, Random? random = null)
    {
        random ??= Random.Shared;
        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var free = All.Where(n => !used.Contains(n)).ToList();
        if (free.Count > 0)
        {
            return free[random.Next(free.Count)];
        }
        var names = All.ToList();
        for (var suffix = 2; ; suffix++)
        {
            var candidates = names.Select(n => $"{n} {suffix}").Where(n => !used.Contains(n)).ToList();
            if (candidates.Count > 0)
            {
                return candidates[random.Next(candidates.Count)];
            }
        }
    }

    public static bool LooksGenerated(string name, IEnumerable<string> originalFileNames)
    {
        var trimmed = name.Trim();
        return trimmed.Length == 0
            || NumberedWork().IsMatch(trimmed)
            || originalFileNames.Any(f => string.Equals(Path.GetFileNameWithoutExtension(f), trimmed, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"^Arbeit \d+$")]
    private static partial Regex NumberedWork();
}
