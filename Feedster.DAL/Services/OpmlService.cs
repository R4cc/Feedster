using System.Xml.Linq;
using Feedster.DAL.Models;
using Feedster.DAL.Repositories;

namespace Feedster.DAL.Services;

public class OpmlService
{
    private readonly FeedRepository _feedRepo;
    private readonly FolderRepository _folderRepo;

    public OpmlService(FeedRepository feedRepo, FolderRepository folderRepo)
    {
        _feedRepo = feedRepo;
        _folderRepo = folderRepo;
    }

    public async Task<string> ExportAsync()
    {
        var folders = await _folderRepo.GetAll();
        var feeds = await _feedRepo.GetAll();

        var body = new XElement("body");

        foreach (var folder in folders)
        {
            var folderElement = new XElement("outline",
                new XAttribute("text", folder.Name),
                new XAttribute("title", folder.Name));

            foreach (var feed in folder.Feeds)
            {
                folderElement.Add(FeedToOutline(feed));
            }

            body.Add(folderElement);
        }

        foreach (var feed in feeds.Where(f => f.Folders.Count == 0))
        {
            body.Add(FeedToOutline(feed));
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("opml", new XAttribute("version", "2.0"),
                new XElement("head", new XElement("title", "Feedster Export")),
                body));

        return doc.ToString();
    }

    public async Task ImportAsync(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var body = doc.Root?.Element("body");
        if (body == null) return;

        var existingFolders = await _folderRepo.GetAll();
        var existingFeeds = await _feedRepo.GetAll();

        foreach (var outline in body.Elements("outline"))
        {
            var xmlUrl = outline.Attribute("xmlUrl")?.Value;
            if (!string.IsNullOrEmpty(xmlUrl))
            {
                await CreateFeed(outline, null, existingFeeds);
                continue;
            }

            var folderName = outline.Attribute("title")?.Value ?? outline.Attribute("text")?.Value;
            if (string.IsNullOrWhiteSpace(folderName)) continue;

            var folder = existingFolders.FirstOrDefault(f => f.Name == folderName);
            if (folder == null)
            {
                folder = new Folder { Name = folderName };
                await _folderRepo.Create(folder);
                existingFolders.Add(folder);
            }

            foreach (var feedOutline in outline.Elements("outline"))
            {
                await CreateFeed(feedOutline, folder, existingFeeds);
            }
        }
    }

    private static XElement FeedToOutline(Feed feed)
    {
        return new XElement("outline",
            new XAttribute("type", "rss"),
            new XAttribute("text", feed.Name),
            new XAttribute("title", feed.Name),
            new XAttribute("xmlUrl", feed.RssUrl));
    }

    private async Task CreateFeed(XElement outline, Folder? folder, List<Feed> existingFeeds)
    {
        var url = outline.Attribute("xmlUrl")?.Value;
        if (string.IsNullOrEmpty(url)) return;

        if (existingFeeds.Any(f => f.RssUrl == url)) return;

        var name = outline.Attribute("title")?.Value ?? outline.Attribute("text")?.Value ?? url;

        var feed = new Feed
        {
            Name = name,
            RssUrl = url
        };

        if (folder != null)
        {
            feed.Folders.Add(folder);
        }

        await _feedRepo.Create(feed);
        existingFeeds.Add(feed);
    }
}
