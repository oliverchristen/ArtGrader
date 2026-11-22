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
            throw new UnsupportedDocumentException(T("The PDF could not be read. Is it password-protected or damaged?"));
        }
        if (count > MaxPages)
        {
            throw new UnsupportedDocumentException(T("The PDF has {0} pages. Please upload no more than {1} pages.", count, MaxPages));
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
