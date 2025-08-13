using System.IO;
using System.Text;
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
            if (!string.IsNullOrEmpty(article.ImagePath))
            {
                var path = Path.Combine("images", article.ImagePath);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    public byte[] ResizeImage(byte[] byteArr)
    {
        var data = byteArr;
        using (var image = new MagickImage(data))
        {
            var size = new MagickGeometry(1280, 720);
            image.Resize(size); ;
            image.Format = MagickFormat.WebP;
            byteArr = image.ToByteArray();
        }

        return byteArr;
    }
}