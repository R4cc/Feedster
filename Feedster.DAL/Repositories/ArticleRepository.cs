using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Feedster.DAL.Services;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

/// <summary>
/// Entity Framework implementation of <see cref="IArticleRepository"/>.
/// </summary>
public class ArticleRepository : IArticleRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ImageService _imageService;

    public ArticleRepository(ApplicationDbContext db, ImageService imageService)
    {
        _db = db;
        _imageService = imageService;
    }

    public async Task<List<Article>> GetAll(CancellationToken cancellationToken = default)
    {
        return await _db.Articles
            .AsNoTracking()
            .Include(a => a.Feed)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateRange(List<Article> articles, CancellationToken cancellationToken = default)
    {
        _db.Articles.UpdateRange(articles);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Article>> GetFromFolderId(int id, CancellationToken cancellationToken = default)
    {
        return (await _db.Folders
                .AsNoTracking()
                .Include(f => f.Feeds)
                .ThenInclude(f => f.Articles)
                .FirstOrDefaultAsync(f => f.FolderId == id, cancellationToken))?
            .Feeds.SelectMany(f => f.Articles!).ToList() ?? new();
    }

    public async Task ClearAllArticles(CancellationToken cancellationToken = default)
    {
        _db.Articles.RemoveRange(_db.Articles);
        _imageService.ClearImageCache();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearArticlesOlderThan(DateTime dateTime, CancellationToken cancellationToken = default)
    {
        var articlesToDelete = await _db.Articles
            .Where(x => x.PublicationDate < dateTime)
            .ToListAsync(cancellationToken);
        _db.Articles.RemoveRange(articlesToDelete);
        await _db.SaveChangesAsync(cancellationToken);
        _imageService.ClearArticleImages(articlesToDelete);
    }
}
