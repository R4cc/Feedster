namespace Feedster.DAL.Models;

public class UserSettings
{
    public int UserSettingsId { get; set; }
    public int ArticleExpirationAfterDays { get; set; }
    public int ArticleRefreshAfterMinutes { get; set; } = 10;
    public int ArticleCountOnPage { get; set; } = 0;
    public int MaxArticleCountInDb { get; set; } = 0;
    public bool ShowImages { get; set; } = true;
    public bool DownloadImages { get; set; } = true;
    public int MaxImageCacheSizeInMb { get; set; } = 1024;
}