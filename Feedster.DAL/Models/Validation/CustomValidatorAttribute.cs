using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Xml;

namespace Feedster.DAL.Models;

public class CustomValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? rssUrl, ValidationContext validationContext)
    {
        if (rssUrl is not null)
        {
            string content = rssUrl!.ToString()!.ToLower();

            if (TryParseFeed(content))
            {
                return null;
            }
        }

        return new ValidationResult(ErrorMessage, new List<string> { validationContext!.MemberName! });
    }

    private bool TryParseFeed(string url)
    {
        try
        {
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = true
            };

            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("rss-reader/1.0 bot");
            using var stream = httpClient.GetStreamAsync(url).Result;
            using var reader = XmlReader.Create(stream, settings);

            _ = SyndicationFeed.Load(reader);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}