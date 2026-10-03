using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Feedster.Tests;

public class DatabaseMigrationTests
{
    [Fact]
    public async Task Existing_schema_and_data_survive_repeated_migration_with_updated_provider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using (var context = new ApplicationDbContext(options))
        {
            await context.Database.MigrateAsync();
            context.Articles.Add(new Article
            {
                FeedId = 1, Title = "Keep me", ArticleLink = "https://example.org/article",
                Tags = new[] { "retained" }
            });
            (await context.UserSettings.FirstAsync()).ArticleRefreshAfterMinutes = 0;
            await context.SaveChangesAsync();
        }
        await using var reopened = new ApplicationDbContext(options);
        await reopened.Database.MigrateAsync();
        var article = Assert.Single(await reopened.Articles.ToListAsync());
        Assert.Equal("Keep me", article.Title);
        Assert.Equal(new[] { "retained" }, article.Tags);
        Assert.Equal(0, (await reopened.UserSettings.FirstAsync()).ArticleRefreshAfterMinutes);
    }
}
