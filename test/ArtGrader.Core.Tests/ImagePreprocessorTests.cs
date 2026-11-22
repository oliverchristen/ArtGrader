using ArtGrader.Core.Images;
using SkiaSharp;

namespace ArtGrader.Core.Tests;

public class ImagePreprocessorTests
{
    public static byte[] Image(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    [Fact]
    public async Task Large_image_is_downscaled_to_jpeg_with_max_side()
    {
        var jpeg = await ImagePreprocessor.ToJpegAsync(new MemoryStream(Image(4000, 3000)), ImagePreprocessor.ModelMaxSide, TestContext.Current.CancellationToken);

        using var codec = SKCodec.Create(new MemoryStream(jpeg));
        Assert.Equal(1568, codec.Info.Width);
        Assert.Equal(1176, codec.Info.Height);
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
    }

    [Fact]
    public async Task Small_image_keeps_its_size()
    {
        var jpeg = await ImagePreprocessor.ToJpegAsync(new MemoryStream(Image(800, 600)), ImagePreprocessor.ModelMaxSide, TestContext.Current.CancellationToken);

        using var codec = SKCodec.Create(new MemoryStream(jpeg));
        Assert.Equal(800, codec.Info.Width);
    }

    [Fact]
    public async Task Real_phone_photo_stays_portrait()
    {
        var photo = Path.Combine(AppContext.BaseDirectory, "Fixtures", "foto-stadt-von-oben.jpeg");

        var jpeg = await ImagePreprocessor.ToJpegAsync(File.OpenRead(photo), ImagePreprocessor.ModelMaxSide, TestContext.Current.CancellationToken);

        using var codec = SKCodec.Create(new MemoryStream(jpeg));
        Assert.True(codec.Info.Height > codec.Info.Width);
    }

    [Fact]
    public async Task Heic_is_rejected_with_a_hint()
    {
        var heic = new byte[64];
        "\0\0\0\u0018ftypheic"u8.ToArray().CopyTo(heic, 0);

        var ex = await Assert.ThrowsAsync<UnsupportedImageException>(() => ImagePreprocessor.ToJpegAsync(new MemoryStream(heic), 1568, TestContext.Current.CancellationToken));

        Assert.Contains("HEIC", ex.Message);
    }

    [Fact]
    public async Task Garbage_is_rejected()
    {
        await Assert.ThrowsAsync<UnsupportedImageException>(() => ImagePreprocessor.ToJpegAsync(new MemoryStream([1, 2, 3, 4, 5]), 1568, TestContext.Current.CancellationToken));
    }
}
