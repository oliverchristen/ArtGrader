using ArtGrader.Core.Domain;

namespace ArtGrader.Core.Tests;

public class PseudonymsTests
{
    [Fact]
    public void Names_are_two_words_and_unique_within_an_exam()
    {
        var taken = new List<string>();
        for (var i = 0; i < 40; i++)
        {
            taken.Add(Pseudonyms.Next(taken, new Random(i)));
        }

        Assert.Equal(40, taken.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(taken, n => Assert.Equal(2, n.Split(' ').Length));
    }

    [Fact]
    public void Taken_names_are_skipped_ignoring_case()
    {
        var all = Pseudonyms.All.ToList();
        var taken = all.Skip(1).Select(n => n.ToUpperInvariant()).ToList();

        Assert.Equal(all[0], Pseudonyms.Next(taken, new Random(1)));
    }

    [Fact]
    public void A_number_is_added_when_all_names_are_taken()
    {
        var name = Pseudonyms.Next(Pseudonyms.All.ToList(), new Random(1));

        Assert.Matches(@"^\S+ \S+ 2$", name);
    }

    [Theory]
    [InlineData("IMG_6291", "IMG_6291.jpeg", true)]
    [InlineData("Lena Muster", "Lena Muster.JPG", true)]
    [InlineData("Arbeit 7", "foto.jpg", true)]
    [InlineData("Lena Muster", "IMG_6291.jpeg", false)]
    [InlineData("Arbeit", "foto.jpg", false)]
    public void Generated_names_from_earlier_versions_are_recognised(string name, string originalFile, bool expected)
    {
        Assert.Equal(expected, Pseudonyms.LooksGenerated(name, [originalFile]));
    }
}
