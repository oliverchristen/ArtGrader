using System.Globalization;
using ArtGrader.Core.Domain;

namespace ArtGrader.Web.Components.Shared;

public static class ImageUrls
{
    public static string Covered(SubmissionImage image)
    {
        var hash = new HashCode();
        foreach (var cover in image.Covers)
        {
            hash.Add(cover.X);
            hash.Add(cover.Y);
            hash.Add(cover.Width);
            hash.Add(cover.Height);
        }
        return $"/bild/{image.Id:N}?v={image.Covers.Count}-{(uint)hash.ToHashCode():x}";
    }

    public static string Original(SubmissionImage image) => $"/files/{image.RelativePath}";

    public static string Percent(double value) => (value * 100).ToString("0.###", CultureInfo.InvariantCulture) + "%";
}
