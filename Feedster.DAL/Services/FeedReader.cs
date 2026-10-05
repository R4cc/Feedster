using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;

namespace Feedster.DAL.Services;

internal static class FeedReader
{
    // Reuse connections for feeds, validation and images; periodically refresh DNS.
    internal static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        UseCookies = false
    });

    static FeedReader()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        Client.DefaultRequestHeaders.Accept.ParseAdd("application/atom+xml,application/rss+xml,application/xml;q=0.9,text/xml;q=0.9,*/*;q=0.8");
    }

    public static async Task<(SyndicationFeed Feed, bool IsAtom)> ReadAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = true,
            XmlResolver = null
        });
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);

        // Seed Atom's inherited xml:base with the final URL, including redirects.
        // The syndication formatter then resolves bases on entries and links.
        bool isAtom = document.Root?.Name == XName.Get("feed", "http://www.w3.org/2005/Atom");
        if (isAtom)
        {
            var sourceUri = response.RequestMessage!.RequestUri!;
            var xmlBase = document.Root!.Attribute(XNamespace.Xml + "base")?.Value;
            document.Root.SetAttributeValue(XNamespace.Xml + "base",
                string.IsNullOrEmpty(xmlBase) ? sourceUri : new Uri(sourceUri, xmlBase));
        }

        using var feedReader = document.CreateReader();
        return (SyndicationFeed.Load(feedReader), isAtom);
    }
}
