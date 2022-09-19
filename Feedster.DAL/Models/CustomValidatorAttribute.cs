using System.ComponentModel.DataAnnotations;
using System.ServiceModel.Syndication;
using System.Xml;

namespace Feedster.DAL.Models;

public class CustomValidationAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object RssUrl, ValidationContext validationContext)
    {
        var content = RssUrl.ToString()?.ToLower();
        
        if (TryParseFeed(content))
        {
            return null;
        }
        return new ValidationResult(ErrorMessage, new[] { validationContext.MemberName });
    }
    
    public bool TryParseFeed(string url)
    {
        try
        {
            SyndicationFeed feed = SyndicationFeed.Load(XmlReader.Create(url));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}