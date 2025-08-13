using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Feedster.DAL.Services;
using Microsoft.EntityFrameworkCore;
using System;

namespace Feedster.DAL.Repositories;

public class ArticleRepository : IDisposable
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
        var list = await _db.Articles.Include(a => a.Feed).ToListAsync();
        return list;
    }

    public async Task UpdateRange(List<Article> articles)
    {
        _db.Articles.UpdateRange(articles);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Article>> GetFromFolderId(int id)
    {
        return (await _db.Folders.Include(f => f.Feeds).ThenInclude(f => f.Articles)
            .FirstOrDefaultAsync(f => f.FolderId == id))?.Feeds.SelectMany(f => f.Articles!).ToList() ?? new();
    }

    public async Task ClearAllArticles()
    {
        _db.Articles.RemoveRange(_db.Articles);
        _imageService.ClearImageCache();
        await _db.SaveChangesAsync();
    }

    public async Task ClearArticlesOlderThan(DateTime dateTime)
    {
        var articlesToDelete = await _db.Articles.Where(x => x.PublicationDate < dateTime).ToListAsync();
        _db.Articles.RemoveRange(articlesToDelete);
        await _db.SaveChangesAsync();
        _imageService.ClearArticleImages(articlesToDelete);
    }

    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _db.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}