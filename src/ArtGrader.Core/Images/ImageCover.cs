using ArtGrader.Core.Domain;
using SkiaSharp;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Core.Images;

public static class ImageCover
{
    public static byte[] Apply(byte[] image, IReadOnlyCollection<CoverArea> areas, CoverArea? crop = null)
    {
        if (areas.Count == 0 && crop is null)
        {
            return image;
        }
        using var bitmap = SKBitmap.Decode(image)
            ?? throw new UnsupportedImageException(T("Das Bild ist beschädigt und kann nicht gelesen werden."));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill })
        {
            foreach (var area in areas)
            {
                canvas.DrawRect(ToPixels(area, bitmap.Width, bitmap.Height), paint);
            }
        }
        using var snapshot = SKImage.FromBitmap(bitmap);
        var bounds = crop is null ? SKRectI.Create(bitmap.Width, bitmap.Height) : SKRectI.Round(ToPixels(crop, bitmap.Width, bitmap.Height));
        bounds.Intersect(SKRectI.Create(bitmap.Width, bitmap.Height));
        if (bounds.Width < 1 || bounds.Height < 1)
        {
            bounds = SKRectI.Create(bitmap.Width, bitmap.Height);
        }
        using var cropped = snapshot.Subset(bounds);
        using var encoded = cropped.Encode(SKEncodedImageFormat.Jpeg, 90);
        return encoded.ToArray();
    }

    public static CoverArea Normalize(double x1, double y1, double x2, double y2)
    {
        static double Clamp(double v) => Math.Clamp(v, 0, 1);
        var left = Clamp(Math.Min(x1, x2));
        var top = Clamp(Math.Min(y1, y2));
        return new CoverArea
        {
            X = left,
            Y = top,
            Width = Clamp(Math.Max(x1, x2)) - left,
            Height = Clamp(Math.Max(y1, y2)) - top,
        };
    }

    private static SKRect ToPixels(CoverArea area, int width, int height)
    {
        var left = (float)Math.Floor(area.X * width);
        var top = (float)Math.Floor(area.Y * height);
        var right = (float)Math.Ceiling((area.X + area.Width) * width);
        var bottom = (float)Math.Ceiling((area.Y + area.Height) * height);
        return new SKRect(left, top, right, bottom);
    }
}
