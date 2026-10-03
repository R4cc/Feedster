using Feedster.DAL.Models;
using ImageMagick;

namespace Feedster.DAL.Services;

public class ImageService
{
    public long GetImageCacheFolderSize()
    {
        DirectoryInfo info = new DirectoryInfo(@"./images");
        return info.EnumerateFiles().Sum(file => file.Length) / 1024 / 1024;
    }

    public long GetDatabaseSize()
    {
        DirectoryInfo info = new DirectoryInfo(@"./data");
        return info.EnumerateFiles().Sum(file => file.Length) / 1024 / 1024;
    }

    public void ClearImageCache()
    {
        DirectoryInfo info = new DirectoryInfo(@"./images");
        foreach (FileInfo file in info.GetFiles()) file.Delete();
    }

    public void ClearArticleImages(List<Article> articles)
    {
        foreach (var article in articles)
        {
            if (!String.IsNullOrEmpty("./images/" + article.ImagePath) && File.Exists("./images/" + article.ImagePath))
            {
                File.Delete("./images/" + article.ImagePath);
            }
        }
    }

    public byte[] ResizeImage(byte[] byteArr)
    {
        using (var image = new MagickImage(byteArr))
        {
            image.AutoOrient();
            // Fit large images while keeping small thumbnails at their original size.
            image.Resize(new MagickGeometry(1280, 720) { Greater = true });
            image.Strip();
            image.Format = MagickFormat.WebP;
            image.Quality = 75;
            return image.ToByteArray();
        }
    }
}
