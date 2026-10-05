using Feedster.DAL.Data;
using Feedster.DAL.Services;
using ImageMagick;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// Initialize an isolated browser-test database without starting feed workers.
if (args.Length == 2 && args[0] == "--initialize-ui-database")
{
    await using var fixtureConnection = new SqliteConnection($"Data Source={args[1]}");
    await fixtureConnection.OpenAsync();
    await using var fixtureDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(fixtureConnection).Options);
    await fixtureDb.Database.MigrateAsync();
    var settings = await fixtureDb.UserSettings.SingleAsync();
    settings.ArticleRefreshAfterMinutes = 0;
    await fixtureDb.SaveChangesAsync();
    Console.WriteLine("Browser fixture database initialized.");
    return;
}

// Run against the final runtime image to catch missing native OS dependencies.
var publishPath = args.FirstOrDefault() ?? "/app";
if (!File.Exists(Path.Combine(publishPath, "wwwroot", "_framework", "blazor.server.js")))
    throw new InvalidOperationException("Blazor's browser runtime was not published.");
if (Directory.EnumerateFiles(publishPath, "Magick.Native*", SearchOption.AllDirectories).Count() != 1)
    throw new InvalidOperationException("Publish must contain one platform's Magick native library.");
if (Directory.EnumerateFiles(publishPath, "*.dll", SearchOption.AllDirectories)
    .Any(path => Path.GetFileName(path).StartsWith("Microsoft.Data.SqlClient")
        || Path.GetFileName(path).StartsWith("Microsoft.CodeAnalysis")))
    throw new InvalidOperationException("Unused SQL Server or migration tooling was published.");

using var source = new MagickImage(MagickColors.Red, 1600, 900);
using var converted = new MagickImage(new ImageService().ResizeImage(source.ToByteArray(MagickFormat.Png)));
if (converted.Format != MagickFormat.WebP || converted.Width != 1280 || converted.Height != 720)
    throw new InvalidOperationException("Native image conversion failed.");

await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
await db.Database.MigrateAsync();
if (await db.Feeds.CountAsync() != 2 || await db.UserSettings.CountAsync() != 1)
    throw new InvalidOperationException("SQLite migrations or seeds failed.");
Console.WriteLine("Runtime image: native WebP conversion, SQLite migrations, and publish contents passed.");
