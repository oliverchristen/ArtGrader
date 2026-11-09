using System.Text.RegularExpressions;

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

    public static IEnumerable<string> All => Animals.SelectMany(a => Pigments.Select(p => $"{a} {p}"));

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
