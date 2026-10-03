using Feedster.DAL.Services;
using ImageMagick;
using Xunit;

namespace Feedster.Tests;

public class ImageServiceTests
{
    [Theory]
    [InlineData(32, 24, 32, 24)]
    [InlineData(1600, 900, 1280, 720)]
    [InlineData(900, 1600, 405, 720)]
    public void Webp_conversion_fits_large_images_without_enlarging_thumbnails(
        uint width, uint height, uint expectedWidth, uint expectedHeight)
    {
        using var source = new MagickImage(MagickColors.Red, width, height);
        var result = new ImageService().ResizeImage(source.ToByteArray(MagickFormat.Png));
        using var converted = new MagickImage(result);
        Assert.Equal(MagickFormat.WebP, converted.Format);
        Assert.Equal(expectedWidth, converted.Width);
        Assert.Equal(expectedHeight, converted.Height);
    }
}
