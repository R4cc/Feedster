using System.ComponentModel.DataAnnotations;

namespace Feedster.DAL.Models;

public class UserSettings
{
    public int UserSettingsId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "The field {0} must be greater than {1}.")]
    public int ArticleExpirationAfterDays { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "The field {0} must be greater than {1}.")]
    public int ArticleRefreshAfterMinutes { get; set; } = 15;
    [Range(0, int.MaxValue, ErrorMessage = "The field {0} must be greater than {1}.")]
    public int ArticleCountOnPage { get; set; } = 20;
    [Range(0, int.MaxValue, ErrorMessage = "The field {0} must be greater than {1}.")]
    public int MaxArticleCountInDb { get; set; } = 0;
    public bool ShowImages { get; set; } = true;
    public bool DownloadImages { get; set; } = true;
    public bool IsDarkMode { get; set; } = true;
}