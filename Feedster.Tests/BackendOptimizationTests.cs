using System.Data.Common;
using System.Text;
using System.Xml.Linq;
using Feedster.DAL.BackgroundServices;
using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using Feedster.DAL.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Feedster.Tests;

[Collection("Image cache")]
public class BackendOptimizationTests
{
    [Fact]
    public async Task Feed_metadata_queries_do_not_load_article_history()
    {
        await using var fixture = await DatabaseFixture.Create();
        fixture.Db.Articles.Add(new Article { FeedId = 1, Title = "Large article body", Tags = [] });
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        fixture.Commands.Sql.Clear();

        var repository = fixture.Feeds();
        var feeds = await repository.GetAll();
        Assert.Equal(2, feeds.Count);
        Assert.All(feeds, feed => Assert.Empty(feed.Articles!));
        Assert.Equal(2, (await repository.GetIds()).Count);
        Assert.NotNull(await repository.GetMetadata(1));
        Assert.DoesNotContain(fixture.Commands.Sql, sql => sql.Contains("Articles"));
        Assert.Empty(fixture.Db.ChangeTracker.Entries<Article>());
    }

    [Fact]
    public async Task Folder_article_query_returns_feed_metadata_without_tracking_the_graph()
    {
        await using var fixture = await DatabaseFixture.Create();
        var feed = await fixture.Db.Feeds.FindAsync(1);
        var folder = await fixture.Db.Folders.FindAsync(1);
        folder!.Feeds.Add(feed!);
        fixture.Db.Articles.AddRange(
            new Article { FeedId = 1, Title = "Included", Tags = [] },
            new Article { FeedId = 3, Title = "Excluded", Tags = [] });
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var articles = await fixture.Articles.GetFromFolderId(1);
        var article = Assert.Single(articles);
        Assert.Equal("Included", article.Title);
        Assert.Equal("ycombinator", article.Feed!.Name);
        Assert.Empty(await fixture.Articles.GetFromFolderId(-1));
        Assert.Equal(2, (await fixture.Articles.GetAll()).Count);
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Refresh_snapshots_omit_article_content_and_repairs_only_write_cache_path()
    {
        await using var fixture = await DatabaseFixture.Create();
        fixture.Db.Articles.Add(new Article
        {
            FeedId = 1, Guid = "id", ArticleLink = "https://example.org/article",
            Title = "Keep title", Description = "Keep body", Tags = ["keep"], ImageUrl = "https://example.org/image.png"
        });
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        fixture.Commands.Sql.Clear();

        var snapshot = Assert.Single(await fixture.Articles.GetForFeed(1));
        Assert.Null(snapshot.Title);
        Assert.Null(snapshot.Description);
        Assert.Empty(fixture.Db.ChangeTracker.Entries<Article>());
        Assert.DoesNotContain(fixture.Commands.Sql, sql => sql.Contains("\"Description\""));
        snapshot.ImagePath = "repaired.webp";
        await fixture.Articles.SaveFetchedArticles([snapshot]);

        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.Articles.SingleAsync();
        Assert.Equal("Keep title", saved.Title);
        Assert.Equal("Keep body", saved.Description);
        Assert.Equal(new[] { "keep" }, saved.Tags);
        Assert.Equal("repaired.webp", saved.ImagePath);
        var update = Assert.Single(fixture.Commands.Sql, sql => sql.StartsWith("UPDATE"));
        Assert.Contains("\"ImagePath\"", update);
        Assert.DoesNotContain("\"Title\"", update);
    }

    [Fact]
    public async Task Expiration_deletes_many_articles_with_one_delete_and_preserves_recent_cache()
    {
        await using var fixture = await DatabaseFixture.Create();
        Directory.CreateDirectory("images");
        var expiredPath = $"expired-{Guid.NewGuid():N}.webp";
        var recentPath = $"recent-{Guid.NewGuid():N}.webp";
        var boundary = DateTime.Now.AddDays(-3);
        await File.WriteAllTextAsync(Path.Combine("images", expiredPath), "expired");
        await File.WriteAllTextAsync(Path.Combine("images", recentPath), "recent");
        try
        {
            fixture.Db.Articles.AddRange(Enumerable.Range(0, 200).Select(index => new Article
            {
                FeedId = 1, Title = $"Expired {index}", Tags = [], PublicationDate = boundary.AddDays(-1),
                ImagePath = index == 0 ? expiredPath : null
            }));
            fixture.Db.Articles.Add(new Article
            {
                FeedId = 1, Title = "At boundary", Tags = [], PublicationDate = boundary, ImagePath = recentPath
            });
            await fixture.Db.SaveChangesAsync();
            var trackedFeed = await fixture.Db.Feeds.FindAsync(1);
            fixture.Commands.Sql.Clear();

            await fixture.Articles.ClearArticlesOlderThan(boundary);
            Assert.Single(fixture.Commands.Sql, sql => sql.StartsWith("DELETE"));
            var select = Assert.Single(fixture.Commands.Sql, sql => sql.StartsWith("SELECT"));
            Assert.StartsWith("SELECT \"a\".\"ImagePath\"", select);
            Assert.False(File.Exists(Path.Combine("images", expiredPath)));
            Assert.True(File.Exists(Path.Combine("images", recentPath)));
            Assert.Single(fixture.Db.ChangeTracker.Entries<Article>());
            Assert.Single(trackedFeed!.Articles!);
            Assert.Equal("At boundary", (await fixture.Db.Articles.SingleAsync()).Title);
            // A subsequent save cannot accidentally resurrect detached expired entities.
            await fixture.Db.SaveChangesAsync();
            Assert.Equal(1, await fixture.Db.Articles.CountAsync());
        }
        finally
        {
            File.Delete(Path.Combine("images", expiredPath));
            File.Delete(Path.Combine("images", recentPath));
        }
    }

    [Fact]
    public async Task Clear_all_uses_one_delete_without_materializing_articles()
    {
        await using var fixture = await DatabaseFixture.Create();
        Directory.CreateDirectory("images");
        fixture.Db.Articles.AddRange(Enumerable.Range(0, 100).Select(_ => new Article { FeedId = 1, Tags = [] }));
        await fixture.Db.SaveChangesAsync();
        fixture.Commands.Sql.Clear();
        await fixture.Articles.ClearAllArticles();
        Assert.Single(fixture.Commands.Sql);
        Assert.StartsWith("DELETE", fixture.Commands.Sql[0]);
        Assert.Empty(fixture.Db.ChangeTracker.Entries<Article>());
        Assert.Equal(0, await fixture.Db.Articles.CountAsync());
        Assert.Equal(2, await fixture.Db.Feeds.CountAsync());
    }

    [Fact]
    public async Task Opml_batch_import_preserves_folders_duplicates_and_export_without_loading_articles()
    {
        await using var fixture = await DatabaseFixture.Create();
        var outlines = string.Concat(Enumerable.Range(0, 100).Select(index =>
            $"<outline text='Feed {index}' xmlUrl='https://example.org/{index}'/>"));
        var xml = $"<opml><body><outline text='Imported'>{outlines}</outline>" +
            "<outline text='Imported'><outline xmlUrl='https://example.org/0'/></outline>" +
            "<outline text='Loose' xmlUrl='https://example.org/loose'/></body></opml>";
        var service = new OpmlService(fixture.Db);
        fixture.Commands.Sql.Clear();
        fixture.Saves.Count = 0;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml))) await service.ImportAsync(stream);
        Assert.Equal(1, fixture.Saves.Count);
        Assert.DoesNotContain(fixture.Commands.Sql, sql => sql.Contains("Articles"));
        Assert.Equal(103, await fixture.Db.Feeds.CountAsync());
        Assert.Single(await fixture.Db.Folders.Where(folder => folder.Name == "Imported").ToListAsync());
        fixture.Db.ChangeTracker.Clear();
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml))) await service.ImportAsync(stream);
        Assert.Equal(103, await fixture.Db.Feeds.CountAsync());

        fixture.Db.ChangeTracker.Clear();
        var export = XDocument.Parse(await service.ExportAsync());
        var body = export.Root!.Element("body")!;
        Assert.Equal(100, body.Elements("outline").Single(outline => (string?)outline.Attribute("text") == "Imported")
            .Elements("outline").Count());
        Assert.Contains(body.Elements("outline"), outline => (string?)outline.Attribute("xmlUrl") == "https://example.org/loose");
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Opml_batch_import_rolls_back_all_new_feeds_and_folders_when_an_insert_fails()
    {
        await using var fixture = await DatabaseFixture.Create();
        await fixture.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_feed BEFORE INSERT ON Feeds WHEN NEW.Name = 'Reject'
            BEGIN SELECT RAISE(ABORT, 'rejected'); END;
            """);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            <opml><body><outline text="New folder">
              <outline text="Valid" xmlUrl="https://example.org/valid"/>
              <outline text="Reject" xmlUrl="https://example.org/reject"/>
            </outline></body></opml>
            """));
        await Assert.ThrowsAsync<DbUpdateException>(() => new OpmlService(fixture.Db).ImportAsync(stream));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(2, await fixture.Db.Feeds.CountAsync());
        Assert.Equal(2, await fixture.Db.Folders.CountAsync());
    }

    [Fact]
    public async Task Queue_wakes_on_arrival_coalesces_pending_jobs_and_cancels_idle_wait()
    {
        var jobs = new BackgroundJobs();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var reader = jobs.ReadAllAsync(stop.Token).GetAsyncEnumerator();
        var waiting = reader.MoveNextAsync().AsTask();
        Assert.False(waiting.IsCompleted);
        jobs.Enqueue([1, 1, 3]);
        Assert.True(await waiting);
        Assert.Equal(1, reader.Current);
        jobs.Enqueue([1, 3]); // In-flight and queued IDs are both coalesced.
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(3, reader.Current);
        jobs.Complete(1);
        jobs.Enqueue([1]);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(1, reader.Current);
        var idle = reader.MoveNextAsync().AsTask();
        Assert.False(idle.IsCompleted);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => idle);
    }

    private sealed class DatabaseFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Db { get; }
        public CommandRecorder Commands { get; } = new();
        public SaveRecorder Saves { get; } = new();
        public ArticleRepository Articles { get; }

        private DatabaseFixture(SqliteConnection connection)
        {
            _connection = connection;
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection).AddInterceptors(Commands, Saves).Options);
            Articles = new ArticleRepository(Db, new ImageService());
        }

        public FeedRepository Feeds() => new(Db, new RssFetchService(Articles, new UserRepository(Db), new ImageService()));

        public static async Task<DatabaseFixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var fixture = new DatabaseFixture(connection);
            await fixture.Db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SaveRecorder : SaveChangesInterceptor
    {
        public int Count { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
