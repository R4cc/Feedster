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
        foreach(System.IO.FileInfo file in info.GetFiles()) file.Delete();
    }
}