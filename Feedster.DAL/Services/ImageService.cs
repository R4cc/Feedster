using System.Text;
using Feedster.DAL.Models;
using ImageMagick;

namespace Feedster.DAL.Services;

public class ImageService
{
    public async Task<long> GetImageCacheFolderSize()
    {
        DirectoryInfo info = new DirectoryInfo(@"./images");
        return info.EnumerateFiles().Sum(file => file.Length) / 1024 / 1024;
    }
    
    public async Task<long> GetDatabaseSize()
    {
        DirectoryInfo info = new DirectoryInfo(@"./data");
        return info.EnumerateFiles().Sum(file => file.Length) / 1024 / 1024;
    }
    
    public async Task ClearImageCache()
    {
        DirectoryInfo info = new DirectoryInfo(@"./images");
        foreach(FileInfo file in info.GetFiles()) file.Delete();
    }

    public async Task ClearArticleImages(List<Article> articles)
    {
        foreach (var article in articles)
        {
            if (!String.IsNullOrEmpty("./images/" + article.ImagePath) && File.Exists("./images/" + article.ImagePath))
            {
                File.Delete("./images/" + article.ImagePath);
            }
        }
    }

    public async Task<byte[]> ResizeImage(byte[] byteArr)
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