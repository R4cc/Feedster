using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Xml;

namespace Feedster.DAL.Models;

public class CustomValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? rssUrl, ValidationContext validationContext)
    {
        if (rssUrl is not null)
        {
            string content = rssUrl.ToString()!.ToLower();
            var httpClientFactory = (IHttpClientFactory?)validationContext.GetService(typeof(IHttpClientFactory));

            if (httpClientFactory != null && TryParseFeed(content, httpClientFactory))
            {
                return null;
            }
        }

        return new ValidationResult(ErrorMessage, new List<string> { validationContext.MemberName! });
    }

    private bool TryParseFeed(string url, IHttpClientFactory httpClientFactory)
    {
        try
        {
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = true
            };

            var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
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