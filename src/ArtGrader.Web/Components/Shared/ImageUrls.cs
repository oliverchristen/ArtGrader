using System.Globalization;
using ArtGrader.Core.Domain;

namespace ArtGrader.Web.Components.Shared;

public sealed record ImageEdits(List<CoverArea> Covers, CoverArea? Crop);

public static class ImageUrls
{
    public static string Covered(SubmissionImage image) => $"/bild/{image.Id:N}?v={Version(image.Covers, image.Crop)}";

    public static string Page(DocumentPage page) => $"/seite/{page.Id:N}?v={Version(page.Covers, page.Crop)}";

    public static string Original(SubmissionImage image) => $"/files/{image.RelativePath}";

    public static string Original(DocumentPage page) => $"/files/{page.RelativePath}";

    public static string Percent(double value) => (value * 100).ToString("0.###", CultureInfo.InvariantCulture) + "%";

    private static string Version(IReadOnlyCollection<CoverArea> covers, CoverArea? crop)
    {
        var hash = new HashCode();
        foreach (var area in covers.Append(crop).OfType<CoverArea>())
        {
            hash.Add(area.X);
            hash.Add(area.Y);
            hash.Add(area.Width);
            hash.Add(area.Height);
        }
        return $"{covers.Count}{(crop is null ? "" : "c")}-{(uint)hash.ToHashCode():x}";
    }
}
