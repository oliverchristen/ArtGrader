using SkiaSharp;

namespace ArtGrader.Core.Images;

public static class ImagePreprocessor
{
    public const int StorageMaxSide = 2400;
    public const int ModelMaxSide = 1568;

    public static readonly IReadOnlyList<string> AcceptedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];

    public static async Task<byte[]> ToJpegAsync(Stream input, int maxSide, CancellationToken cancellationToken = default)
    {
        var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        if (IsHeif(bytes))
        {
            throw new UnsupportedImageException(
                "HEIC-Fotos (iPhone) werden nicht unterstützt. Bitte als JPEG exportieren oder auf dem iPhone unter "
                    + "Einstellungen › Kamera › Formate «Maximale Kompatibilität» wählen.");
        }

        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data)
            ?? throw new UnsupportedImageException("Diese Datei ist kein unterstütztes Bild (JPEG, PNG, WebP, GIF, BMP).");

        using var decoded = SKBitmap.Decode(codec)
            ?? throw new UnsupportedImageException("Das Bild ist beschädigt und kann nicht gelesen werden.");
        using var oriented = ApplyOrigin(decoded, codec.EncodedOrigin);
        using var resized = Downscale(oriented, maxSide);
        using var image = SKImage.FromBitmap(resized);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return jpeg.ToArray();
    }

    private static SKBitmap Downscale(SKBitmap source, int maxSide)
    {
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= maxSide)
        {
            return source.Copy();
        }
        var scale = (double)maxSide / longest;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new UnsupportedImageException("Das Bild konnte nicht verkleinert werden.");
    }

    private static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft)
        {
            return source.Copy();
        }

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swap ? source.Height : source.Width;
        var height = swap ? source.Width : source.Height;
        var result = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType));
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(OriginMatrix(origin, source.Width, source.Height));
        using var sourceImage = SKImage.FromBitmap(source);
        canvas.DrawImage(sourceImage, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        return result;
    }

    private static SKMatrix OriginMatrix(SKEncodedOrigin origin, int w, int h) =>
        origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity,
        };

    private static bool IsHeif(byte[] header)
    {
        if (header.Length < 12 || header[4] != 'f' || header[5] != 't' || header[6] != 'y' || header[7] != 'p')
        {
            return false;
        }
        var brand = System.Text.Encoding.ASCII.GetString(header, 8, 4);
        return brand is "heic" or "heix" or "hevc" or "hevx" or "mif1" or "msf1" or "heim" or "heis";
    }
}

public sealed class UnsupportedImageException(string message) : Exception(message);
