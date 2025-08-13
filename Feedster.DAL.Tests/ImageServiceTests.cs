using Feedster.DAL.Models;
using Feedster.DAL.Services;
using System.Collections.Generic;
using System.IO;
using Xunit;

public class ImageServiceTests
{
    [Fact]
    public void ClearArticleImages_HandlesNullAndEmptyPaths()
    {
        Directory.CreateDirectory("images");
        var existing = Path.Combine("images", "keep.jpg");
        File.WriteAllText(existing, "data");
        var articles = new List<Article>
        {
            new Article { ImagePath = null },
            new Article { ImagePath = string.Empty }
        };
        var service = new ImageService();

        service.ClearArticleImages(articles);

        Assert.True(File.Exists(existing));

        File.Delete(existing);
        Directory.Delete("images", true);
    }

    [Fact]
    public void ClearArticleImages_RemovesExistingFile()
    {
        Directory.CreateDirectory("images");
        var path = Path.Combine("images", "to-delete.jpg");
        File.WriteAllText(path, "data");
        var articles = new List<Article> { new Article { ImagePath = "to-delete.jpg" } };
        var service = new ImageService();

        service.ClearArticleImages(articles);

        Assert.False(File.Exists(path));

        Directory.Delete("images", true);
    }
}
