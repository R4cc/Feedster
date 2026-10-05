using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Feedster.DAL.Services;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

public class ArticleRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ImageService _imageService;

    public ArticleRepository(ApplicationDbContext db, ImageService imageService)
    {
        _db = db;
        _imageService = imageService;
    }

    public async Task<List<Article>> GetAll()
    {
        return await _db.Articles.AsNoTrackingWithIdentityResolution().Include(a => a.Feed).ToListAsync();
    }

    public async Task SaveFetchedArticles(List<Article> articles, CancellationToken cancellationToken = default)
    {
        if (articles.Count == 0) return;
        foreach (var article in articles)
        {
            // Avoid walking and updating the feed's entire tracked navigation graph.
            if (article.ArticleId == 0)
                _db.Entry(article).State = EntityState.Added;
            else
            {
                // Existing imports only repair cache paths. Keep article content untouched.
                var tracked = await _db.Articles.FindAsync([article.ArticleId], cancellationToken);
                if (tracked is not null) tracked.ImagePath = article.ImagePath;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Article>> GetForFeed(int feedId, CancellationToken cancellationToken = default) =>
        _db.Articles.Where(article => article.FeedId == feedId)
            .Select(article => new Article
            {
                ArticleId = article.ArticleId,
                FeedId = article.FeedId,
                Guid = article.Guid,
                ArticleLink = article.ArticleLink,
                ImageUrl = article.ImageUrl,
                ImagePath = article.ImagePath
            }).ToListAsync(cancellationToken);

    public async Task<List<Article>> GetFromFolderId(int id)
    {
        return await _db.Articles.AsNoTrackingWithIdentityResolution().Include(article => article.Feed)
            .Where(article => article.Feed!.Folders.Any(folder => folder.FolderId == id)).ToListAsync();
    }

    public async Task ClearAllArticles()
    {
        await _db.Articles.ExecuteDeleteAsync();
        DetachDeletedArticles(_ => true);
        _imageService.ClearImageCache();
    }

    public async Task ClearArticlesOlderThan(DateTime dateTime)
    {
        // Snapshot only cache paths and delete the same expired set in one SQL command.
        using var transaction = await _db.Database.BeginTransactionAsync();
        var expired = _db.Articles.Where(article => article.PublicationDate < dateTime);
        var imagePaths = await expired.Select(article => article.ImagePath).ToListAsync();
        await expired.ExecuteDeleteAsync();
        await transaction.CommitAsync();
        DetachDeletedArticles(article => article.PublicationDate < dateTime);
        _imageService.ClearArticleImages(imagePaths);
    }

    private void DetachDeletedArticles(Func<Article, bool> predicate)
    {
        var entries = _db.ChangeTracker.Entries<Article>().Where(entry => predicate(entry.Entity)).ToList();
        foreach (var group in entries.Where(entry => entry.Entity.Feed is not null).GroupBy(entry => entry.Entity.Feed!))
        {
            var deleted = group.Select(entry => entry.Entity).ToHashSet();
            group.Key.Articles?.RemoveAll(deleted.Contains);
        }
        foreach (var entry in entries) entry.State = EntityState.Detached;
    }

    internal void Dispose()
    {
        _db.Dispose();
    }
}
