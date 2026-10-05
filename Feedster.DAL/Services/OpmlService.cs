using System.Xml.Linq;
using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Services;

public class OpmlService
{
    private readonly ApplicationDbContext _db;

    public OpmlService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> ExportAsync()
    {
        var folders = await _db.Folders.AsNoTracking().ToListAsync();
        var feeds = await _db.Feeds.AsNoTracking().Include(feed => feed.Folders).ToListAsync();
        var feedsByFolder = feeds.SelectMany(feed => feed.Folders.Select(folder => (folder.FolderId, Feed: feed)))
            .ToLookup(entry => entry.FolderId, entry => entry.Feed);

        var body = new XElement("body");

        foreach (var folder in folders)
        {
            var folderElement = new XElement("outline",
                new XAttribute("text", folder.Name),
                new XAttribute("title", folder.Name));

            foreach (var feed in feedsByFolder[folder.FolderId])
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

        var existingFolders = new Dictionary<string, Folder>(StringComparer.Ordinal);
        foreach (var folder in await _db.Folders.ToListAsync()) existingFolders.TryAdd(folder.Name, folder);
        var existingUrls = (await _db.Feeds.Select(feed => feed.RssUrl).ToListAsync())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var outline in body.Elements("outline"))
        {
            var xmlUrl = outline.Attribute("xmlUrl")?.Value;
            if (!string.IsNullOrEmpty(xmlUrl))
            {
                CreateFeed(outline, null, existingUrls);
                continue;
            }

            var folderName = outline.Attribute("title")?.Value ?? outline.Attribute("text")?.Value;
            if (string.IsNullOrWhiteSpace(folderName)) continue;

            if (!existingFolders.TryGetValue(folderName, out var folder))
            {
                folder = new Folder { Name = folderName };
                _db.Folders.Add(folder);
                existingFolders.Add(folderName, folder);
            }

            foreach (var feedOutline in outline.Elements("outline"))
            {
                CreateFeed(feedOutline, folder, existingUrls);
            }
        }
        // One transaction and change-detection pass for the entire import.
        await _db.SaveChangesAsync();
    }

    private static XElement FeedToOutline(Feed feed)
    {
        return new XElement("outline",
            new XAttribute("type", "rss"),
            new XAttribute("text", feed.Name),
            new XAttribute("title", feed.Name),
            new XAttribute("xmlUrl", feed.RssUrl));
    }

    private void CreateFeed(XElement outline, Folder? folder, HashSet<string> existingUrls)
    {
        var url = outline.Attribute("xmlUrl")?.Value;
        if (string.IsNullOrEmpty(url)) return;

        if (!existingUrls.Add(url)) return;

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

        _db.Feeds.Add(feed);
    }
}
