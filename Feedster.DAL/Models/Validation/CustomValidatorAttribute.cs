using System.ComponentModel.DataAnnotations;
using Feedster.DAL.Services;

namespace Feedster.DAL.Models;

public class CustomValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? rssUrl, ValidationContext validationContext)
    {
        if (rssUrl is not null)
        {
            string content = rssUrl.ToString()!;

            if (TryParseFeed(content))
            {
                return null;
            }
        }

        return new ValidationResult(ErrorMessage, new List<string> { validationContext!.MemberName! });
    }

    private static bool TryParseFeed(string url)
    {
        try
        {
            // ValidationAttribute is synchronous; run async IO outside Blazor's context.
            _ = Task.Run(() => FeedReader.ReadAsync(url)).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
