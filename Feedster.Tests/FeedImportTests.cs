using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Feedster.DAL.Data;
using Feedster.DAL.BackgroundServices;
using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Feedster.DAL.Services;
using ImageMagick;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Feedster.Tests;

[Collection("Image cache")]
public class FeedImportTests
{
    private static string Atom(string entries, string attributes = "") => $"""
        <feed xmlns="http://www.w3.org/2005/Atom" {attributes}>
          <title>Test feed</title><id>urn:feed:test</id>
          <updated>2026-10-01T10:00:00Z</updated>
          <author><name>Test author</name></author>
          {entries}
        </feed>
        """;

    private static string Entry(string content, string id = "urn:entry:1") => $"""
        <entry><id>{id}</id><title>Test entry</title>
          <updated>2026-10-01T10:00:00Z</updated>{content}
        </entry>
        """;

    [Fact]
    public async Task Atom_selects_article_link_and_imports_metadata()
    {
        await using var fixture = await ImportFixture.Create(Atom(Entry("""
            <link rel="self" href="https://example.org/entry.atom"/>
            <link rel="enclosure" type="image/png" href="https://example.org/image.png"/>
            <link rel="alternate" type="application/atom+xml" href="https://example.org/alternate.atom"/>
            <link rel="alternate" type="text/html" href="https://example.org/article"/>
            <published>2026-09-30T14:30:00+02:00</published>
            <summary type="html">A &lt;b&gt;summary&lt;/b&gt; &amp;amp; details</summary>
            <content type="text">Longer body</content>
            <category term="news"/><category term="tech"/>
            """)));

        Assert.Equal(1, await fixture.Refresh());
        var article = Assert.Single(await fixture.Articles());
        Assert.Equal("urn:entry:1", article.Guid);
        Assert.Equal("https://example.org/article", article.ArticleLink);
        Assert.Equal("Test entry", article.Title);
        Assert.Equal("A summary & details", article.Description);
        Assert.Equal(DateTimeOffset.Parse("2026-09-30T14:30:00+02:00").LocalDateTime, article.PublicationDate);
        Assert.Equal(new[] { "news", "tech" }, article.Tags);
        Assert.Equal(fixture.Feed.FeedId, article.FeedId);
    }

    [Theory]
    [InlineData("<content type=\"text\">Plain &amp; &lt;literal&gt;</content>", "Plain & <literal>")]
    [InlineData("<content type=\"html\">&lt;p&gt;HTML &amp;amp; text&lt;/p&gt;</content>", "HTML & text")]
    [InlineData("<content type=\"xhtml\"><div xmlns=\"http://www.w3.org/1999/xhtml\"><p>XHTML <b>text</b></p></div></content>", "XHTML text")]
    [InlineData("", "")]
    public async Task Atom_imports_entries_without_summary(string content, string description)
    {
        await using var fixture = await ImportFixture.Create(Atom(Entry(
            "<link href=\"https://example.org/article\"/>" + content)));
        Assert.Equal(1, await fixture.Refresh());
        var article = Assert.Single(await fixture.Articles());
        Assert.Equal(description, article.Description);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T10:00:00Z").LocalDateTime, article.PublicationDate);
    }

    [Fact]
    public async Task Atom_resolves_inherited_xml_base_with_prefixed_namespace()
    {
        var xml = """
            <a:feed xmlns:a="http://www.w3.org/2005/Atom" xml:base="https://example.org/blog/">
              <a:title>Feed</a:title><a:id>urn:feed</a:id><a:updated>2026-10-01T10:00:00Z</a:updated>
              <a:entry xml:base="posts/">
                <a:id>urn:entry</a:id><a:title type="html">&lt;b&gt;Title&lt;/b&gt;</a:title>
                <a:updated>2026-10-01T10:00:00Z</a:updated>
                <a:link xml:base="2026/" href="article?lang=EN"/>
                <a:summary>Summary</a:summary>
              </a:entry>
            </a:feed>
            """;
        await using var fixture = await ImportFixture.Create(xml);
        Assert.Equal(1, await fixture.Refresh());
        var article = Assert.Single(await fixture.Articles());
        Assert.Equal("https://example.org/blog/posts/2026/article?lang=EN", article.ArticleLink);
        Assert.Equal("Title", article.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("xml:base=\"../blog/\"")]
    public async Task Atom_resolves_relative_links_against_final_response_url(string attributes)
    {
        await using var fixture = await ImportFixture.Create(Atom(Entry(
            "<link href=\"article\"/><summary>Summary</summary>"), attributes));
        fixture.Server.Redirect = "/blog/Feed.xml";
        Assert.Equal(1, await fixture.Refresh());
        Assert.Equal(new Uri(fixture.Server.BaseUri, "blog/article").AbsoluteUri,
            Assert.Single(await fixture.Articles()).ArticleLink);
    }

    [Fact]
    public async Task Atom_deduplicates_by_id_within_feed_and_across_refreshes()
    {
        var entry = Entry("<link href=\"https://example.org/original\"/><summary>Summary</summary>");
        await using var fixture = await ImportFixture.Create(Atom(entry + entry));
        Assert.Equal(1, await fixture.Refresh());
        fixture.Server.Xml = Atom(Entry("<link href=\"https://example.org/changed\"/><summary>Summary</summary>"));
        Assert.Equal(0, await fixture.Refresh());
        Assert.Single(await fixture.Articles());
    }

    [Fact]
    public async Task Atom_keeps_distinct_linkless_entries_and_deduplicates_them()
    {
        await using var fixture = await ImportFixture.Create(Atom(
            Entry("<content>First body</content>", "urn:first") +
            Entry("<content>Second body</content>", "urn:second")));
        Assert.Equal(2, await fixture.Refresh());
        Assert.Equal(0, await fixture.Refresh());
        var articles = await fixture.Articles();
        Assert.Equal(2, articles.Count);
        Assert.All(articles, article => Assert.Empty(article.ArticleLink));
        Assert.Contains(articles, article => article.Description == "Second body");
    }

    [Fact]
    public async Task Atom_uses_http_id_when_no_alternate_link_exists()
    {
        await using var fixture = await ImportFixture.Create(Atom(Entry(
            "<content>Body</content>", "https://example.org/article")));
        Assert.Equal(1, await fixture.Refresh());
        Assert.Equal("https://example.org/article", Assert.Single(await fixture.Articles()).ArticleLink);
    }

    [Fact]
    public async Task Atom_does_not_use_self_enclosure_or_non_http_links_as_permalink()
    {
        await using var fixture = await ImportFixture.Create(Atom(Entry("""
            <link rel="self" href="https://example.org/entry.atom"/>
            <link rel="enclosure" href="https://example.org/image.png"/>
            <link rel="alternate" href="javascript:alert(1)"/>
            <content>Body</content>
            """)));
        Assert.Equal(1, await fixture.Refresh());
        Assert.Empty(Assert.Single(await fixture.Articles()).ArticleLink);
    }

    [Fact]
    public async Task Invalid_entry_date_does_not_prevent_other_entries_from_importing()
    {
        await using var fixture = await ImportFixture.Create(Atom(
            Entry("<content>Invalid date</content>", "urn:bad-date")
                .Replace("2026-10-01T10:00:00Z", "invalid") +
            Entry("<content>Valid body</content>", "urn:valid")));
        Assert.Equal(1, await fixture.Refresh());
        Assert.Equal("urn:valid", Assert.Single(await fixture.Articles()).Guid);
    }

    [Fact]
    public async Task Atom_downloads_relative_image_enclosure_without_filename_extension()
    {
        await using var fixture = await ImportFixture.Create(Atom(Entry("""
            <link href="https://example.org/article"/>
            <link rel="enclosure" type="image/png" href="image?Token=AbC"/>
            <content>Body</content>
            """)));
        using var image = new MagickImage(MagickColors.Red, 2, 2);
        fixture.Server.ImageBytes = image.ToByteArray(MagickFormat.Png);
        (await fixture.Db.UserSettings.FirstAsync()).DownloadImages = true;
        await fixture.Db.SaveChangesAsync();

        var imageDirectory = Path.GetFullPath("images");
        bool createdDirectory = !Directory.Exists(imageDirectory);
        Directory.CreateDirectory(imageDirectory);
        string? imagePath = null;
        try
        {
            Assert.Equal(1, await fixture.Refresh());
            var article = Assert.Single(await fixture.Articles());
            Assert.Equal(new Uri(fixture.Server.BaseUri, "image?Token=AbC").AbsoluteUri, article.ImageUrl);
            Assert.False(string.IsNullOrEmpty(article.ImagePath));
            imagePath = Path.Combine(imageDirectory, article.ImagePath!);
            Assert.True(File.Exists(imagePath));
            using var downloaded = new MagickImage(imagePath);
            Assert.Equal(MagickFormat.WebP, downloaded.Format);
        }
        finally
        {
            if (imagePath is not null) File.Delete(imagePath);
            if (createdDirectory && !Directory.EnumerateFileSystemEntries(imageDirectory).Any()) Directory.Delete(imageDirectory);
        }
    }

    [Fact]
    public async Task Atom_expiration_uses_updated_when_published_is_absent()
    {
        var old = DateTimeOffset.UtcNow.AddDays(-10).ToString("O");
        var recent = DateTimeOffset.UtcNow.AddHours(-1).ToString("O");
        await using var fixture = await ImportFixture.Create(Atom(
            Entry("<content>Expired</content>", "urn:old").Replace("2026-10-01T10:00:00Z", old) +
            Entry($"<published>{old}</published><content>Old published</content>", "urn:published")
                .Replace("2026-10-01T10:00:00Z", recent) +
            Entry("<content>Recent</content>", "urn:recent").Replace("2026-10-01T10:00:00Z", recent)));
        var settings = await fixture.Db.UserSettings.FirstAsync();
        settings.ArticleExpirationAfterDays = 3;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(1, await fixture.Refresh());
        Assert.Equal("urn:recent", Assert.Single(await fixture.Articles()).Guid);
    }

    [Fact]
    public async Task Image_candidates_are_downloaded_once_and_cache_repair_keeps_article_content()
    {
        await using var fixture = await ImportFixture.Create(Atom(Entry("""
            <link href="https://example.org/article"/>
            <link rel="enclosure" type="image/png" href="small.png"/>
            <link rel="enclosure" type="image/png" href="large.png"/>
            <link rel="enclosure" type="image/png" href="large.png"/>
            <content>Keep body</content><category term="keep"/>
            """)));
        using var small = new MagickImage(MagickColors.Red, 2, 2);
        using var large = new MagickImage(MagickColors.Blue, 8, 8);
        fixture.Server.Images["/small.png"] = small.ToByteArray(MagickFormat.Png);
        fixture.Server.Images["/large.png"] = large.ToByteArray(MagickFormat.Png);
        (await fixture.Db.UserSettings.FirstAsync()).DownloadImages = true;
        await fixture.Db.SaveChangesAsync();
        Directory.CreateDirectory("images");
        string? path = null;
        try
        {
            Assert.Equal(1, await fixture.Refresh());
            var article = Assert.Single(await fixture.Articles());
            Assert.Equal(new Uri(fixture.Server.BaseUri, "large.png").AbsoluteUri, article.ImageUrl);
            Assert.Equal(1, fixture.Server.RequestCounts["/small.png"]);
            Assert.Equal(1, fixture.Server.RequestCounts["/large.png"]);
            path = Path.Combine("images", article.ImagePath!);
            using (var cached = new MagickImage(path)) Assert.Equal(8u, cached.Width);
            Assert.Equal(0, await fixture.Refresh());
            Assert.Equal(1, fixture.Server.RequestCounts["/large.png"]);

            File.Delete(path);
            Assert.Equal(1, await fixture.Refresh());
            article = Assert.Single(await fixture.Articles());
            path = Path.Combine("images", article.ImagePath!);
            Assert.True(File.Exists(path));
            Assert.Equal("Test entry", article.Title);
            Assert.Equal("Keep body", article.Description);
            Assert.Equal(new[] { "keep" }, article.Tags);
            Assert.Equal(1, fixture.Server.RequestCounts["/small.png"]);
            Assert.Equal(2, fixture.Server.RequestCounts["/large.png"]);
        }
        finally
        {
            if (path is not null) File.Delete(path);
        }
    }

    [Fact]
    public async Task Cancelled_refresh_propagates_cancellation_without_downloading_or_saving()
    {
        await using var fixture = await ImportFixture.Create(Atom(Entry("<content>Body</content>")));
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Refresh(stop.Token));
        Assert.Empty(fixture.Server.RequestCounts);
        Assert.Empty(await fixture.Articles());
    }

    [Fact]
    public async Task Background_worker_uses_and_disposes_a_fresh_context_per_feed_and_skips_deleted_feeds()
    {
        await using var fixture = await ImportFixture.Create(Atom(Entry("<content>Body</content>")));
        var contexts = new ConcurrentBag<ApplicationDbContext>();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(fixture.Db.Database.GetDbConnection()).Options;
        await using var services = new ServiceCollection()
            .AddScoped(_ =>
            {
                var db = new ApplicationDbContext(options);
                contexts.Add(db);
                return db;
            })
            .AddTransient<FeedRepository>().AddTransient<ArticleRepository>().AddTransient<UserRepository>()
            .AddTransient<ImageService>().AddTransient<RssFetchService>()
            .BuildServiceProvider();
        var jobs = new BackgroundJobs();
        using var worker = new FeedUpdateDequeueService(services.GetRequiredService<IServiceScopeFactory>(),
            jobs, NullLogger<FeedUpdateDequeueService>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StartAsync(timeout.Token);
        try
        {
            jobs.Enqueue([fixture.Feed.FeedId, 9999, 8888]);
            while (contexts.Count < 3) await Task.Delay(10, timeout.Token);
        }
        finally
        {
            await worker.StopAsync(timeout.Token);
        }
        Assert.Equal(3, contexts.Count);
        Assert.Single(await fixture.Articles());
        Assert.Single(fixture.Server.RequestCounts);
        foreach (var context in contexts)
            await Assert.ThrowsAsync<ObjectDisposedException>(() => context.Articles.CountAsync());
    }

    private const string Rss = """
        <rss version="2.0"><channel><title>RSS feed</title><link>https://example.org</link>
          <description>RSS feed</description><item><guid isPermaLink="false">rss-1</guid>
          <title>RSS article</title><link>https://example.org/rss-article</link>
          <description>&lt;p&gt;RSS body&lt;/p&gt;</description>
          <pubDate>Thu, 01 Oct 2026 10:00:00 GMT</pubDate><category>rss</category>
          </item></channel></rss>
        """;

    [Fact]
    public async Task Rss_import_and_repeat_refresh_still_work()
    {
        await using var fixture = await ImportFixture.Create(Rss);
        Assert.Equal(1, await fixture.Refresh());
        Assert.Equal(0, await fixture.Refresh());
        var article = Assert.Single(await fixture.Articles());
        Assert.Equal("RSS body", article.Description);
        Assert.Equal("RSS article", article.Title);
        Assert.Equal("https://example.org/rss-article", article.ArticleLink);
        Assert.Equal(new[] { "rss" }, article.Tags);
    }

    [Fact]
    public async Task Rss_preserves_link_based_deduplication_when_guid_is_reused()
    {
        await using var fixture = await ImportFixture.Create(Rss);
        Assert.Equal(1, await fixture.Refresh());
        fixture.Server.Xml = Rss.Replace("https://example.org/rss-article", "https://example.org/another-article");
        Assert.Equal(1, await fixture.Refresh());
        Assert.Equal(2, (await fixture.Articles()).Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Validation_accepts_rss_and_atom_without_lowercasing_url(bool atom)
    {
        await using var server = new FeedServer(atom ? Atom(Entry("<content>Body</content>")) : Rss);
        var feed = new Feed { Name = "Feed", RssUrl = new Uri(server.BaseUri, "Feed.xml?Token=AbC").AbsoluteUri };
        var results = new List<ValidationResult>();
        Assert.True(Validator.TryValidateObject(feed, new ValidationContext(feed), results, true));
        Assert.Empty(results);
        Assert.Equal("/Feed.xml?Token=AbC", server.LastPath);
        Assert.Contains("application/atom+xml", server.LastHeaders);
    }

    [Theory]
    [InlineData("<html><body>Not a feed</body></html>", 200)]
    [InlineData("<feed xmlns=\"http://www.w3.org/2005/Atom\"><entry>", 200)]
    [InlineData("Not found", 404)]
    public async Task Invalid_feed_fails_validation_and_refresh(string xml, int status)
    {
        await using var fixture = await ImportFixture.Create(xml);
        fixture.Server.Status = status;
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(fixture.Feed, new ValidationContext(fixture.Feed), results, true));
        Assert.Contains(results, result => result.ErrorMessage == "Enter a valid RSS or Atom feed URL.");
        Assert.Null(await fixture.Refresh());
        Assert.Empty(await fixture.Articles());
    }

    private sealed class ImportFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly RssFetchService _fetchService;
        public FeedServer Server { get; }
        public ApplicationDbContext Db { get; }
        public Feed Feed { get; private set; }

        private ImportFixture(FeedServer server, SqliteConnection connection, ApplicationDbContext db, Feed feed)
        {
            Server = server;
            _connection = connection;
            Db = db;
            Feed = feed;
            var images = new ImageService();
            _fetchService = new RssFetchService(new ArticleRepository(db, images), new UserRepository(db), images);
        }

        public static async Task<ImportFixture> Create(string xml)
        {
            var server = new FeedServer(xml);
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            (await db.UserSettings.FirstAsync()).DownloadImages = false;
            var feed = new Feed { Name = "Test", RssUrl = new Uri(server.BaseUri, "Feed.xml?Token=AbC").AbsoluteUri };
            db.Feeds.Add(feed);
            await db.SaveChangesAsync();
            return new ImportFixture(server, connection, db, feed);
        }

        public async Task<int?> Refresh(CancellationToken cancellationToken = default)
        {
            Db.ChangeTracker.Clear();
            Feed = await Db.Feeds.SingleAsync(feed => feed.FeedId == Feed.FeedId);
            return await _fetchService.RefreshFeed(Feed, cancellationToken);
        }

        public Task<List<Article>> Articles() => Db.Articles.Where(article => article.FeedId == Feed.FeedId).ToListAsync();

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
            await Server.DisposeAsync();
        }
    }

    // Real HTTP fixtures exercise the same validation, fetch, and database path as the app.
    private sealed class FeedServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _requests;
        public Uri BaseUri { get; }
        public string Xml { get; set; }
        public int Status { get; set; } = 200;
        public string? Redirect { get; set; }
        public byte[]? ImageBytes { get; set; }
        public Dictionary<string, byte[]> Images { get; } = [];
        public ConcurrentDictionary<string, int> RequestCounts { get; } = new();
        public string? LastPath { get; private set; }
        public string LastHeaders { get; private set; } = "";

        public FeedServer(string xml)
        {
            Xml = xml;
            _listener.Start();
            BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
            _requests = Serve();
        }

        private async Task Serve()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, leaveOpen: true);
                    var request = await reader.ReadLineAsync(_stop.Token);
                    LastPath = request?.Split(' ')[1];
                    if (LastPath is not null) RequestCounts.AddOrUpdate(LastPath, 1, (_, count) => count + 1);
                    var headers = new StringBuilder();
                    string? line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(_stop.Token))) headers.AppendLine(line);
                    LastHeaders = headers.ToString();
                    var redirect = Redirect is not null && LastPath != Redirect;
                    var imageBytes = ImageBytes is not null && LastPath?.StartsWith("/image?") == true
                        ? ImageBytes : Images.GetValueOrDefault(LastPath ?? "");
                    var isImage = imageBytes is not null;
                    var body = imageBytes ?? Encoding.UTF8.GetBytes(Xml);
                    var response = $"HTTP/1.1 {(redirect ? 302 : Status)} Response\r\n" +
                        (redirect ? $"Location: {Redirect}\r\n" : "") +
                        $"Content-Type: {(isImage ? "image/png" : "application/xml")}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stop.Token);
                    await stream.WriteAsync(body, _stop.Token);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            await _requests;
            _listener.Stop();
            _stop.Dispose();
        }
    }
}
