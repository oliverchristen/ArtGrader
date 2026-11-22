using System.Runtime.Versioning;
using PDFtoImage;
using SkiaSharp;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Core.Documents;

public static class PdfPages
{
    public const int MaxPages = 20;

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public static List<byte[]> Render(byte[] pdf)
    {
        int count;
        try
        {
            count = Conversion.GetPageCount(pdf);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new UnsupportedDocumentException(T("Das PDF konnte nicht gelesen werden. Ist es passwortgeschützt oder beschädigt?"));
        }
        if (count > MaxPages)
        {
            throw new UnsupportedDocumentException(T("Das PDF hat {0} Seiten. Bitte höchstens {1} Seiten hochladen.", count, MaxPages));
        }

        var pages = new List<byte[]>();
        var options = new RenderOptions(Dpi: 130, BackgroundColor: SKColors.White, WithAnnotations: true);
        foreach (var bitmap in Conversion.ToImages(pdf, password: null, options))
        {
            using (bitmap)
            using (var image = SKImage.FromBitmap(bitmap))
            using (var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85))
            {
                pages.Add(jpeg.ToArray());
            }
        }
        return pages;
    }
}
