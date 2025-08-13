using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Feedster.DAL.Services;
using Microsoft.EntityFrameworkCore;
using System;

namespace Feedster.DAL.Repositories;

public class ArticleRepository(ApplicationDbContext db, ImageService imageService) : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public async Task<List<Article>> GetAll()
    {
        var list = await db.Articles.Include(a => a.Feed).ToListAsync();
        return list;
    }

    public async Task UpdateRange(List<Article> articles)
    {
        db.Articles.UpdateRange(articles);
        await db.SaveChangesAsync();
    }

    public async Task<List<Article>> GetFromFolderId(int id)
    {
        return (await db.Folders.Include(f => f.Feeds).ThenInclude(f => f.Articles)
            .FirstOrDefaultAsync(f => f.FolderId == id))?.Feeds.SelectMany(f => f.Articles!).ToList() ?? new();
    }

    public async Task ClearAllArticles()
    {
        db.Articles.RemoveRange(db.Articles);
        imageService.ClearImageCache();
        await db.SaveChangesAsync();
    }

    public async Task ClearArticlesOlderThan(DateTime dateTime)
    {
        var articlesToDelete = await db.Articles.Where(x => x.PublicationDate < dateTime).ToListAsync();
        db.Articles.RemoveRange(articlesToDelete);
        await db.SaveChangesAsync();
        imageService.ClearArticleImages(articlesToDelete);
    }

    public void Dispose()
    {
        if (_disposed) return;
        db.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await db.DisposeAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
