using System.ComponentModel.DataAnnotations;
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
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = true
            };

            _ = SyndicationFeed.Load(XmlReader.Create(url, settings));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}