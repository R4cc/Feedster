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
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Ignore;
            settings.IgnoreWhitespace = true;
            
            SyndicationFeed feed = SyndicationFeed.Load(XmlReader.Create(url, settings));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}