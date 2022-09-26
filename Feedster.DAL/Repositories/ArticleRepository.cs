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
        var list = await _db.Articles.Include(a => a.Feed).ToListAsync();
        return list;
    }

    public async Task UpdateRange(List<Article> articles)
    {
        _db.Articles.UpdateRange(articles);
        await _db.SaveChangesAsync();
    }
    
    public async Task<List<Article>> GetFromGroupId(int id)
    {
        return _db.Articles.Include(x => x.Feed).ThenInclude(c => c.Folders).Where(f => f.Feed.Folders.Any(g => g.FolderId == id)).ToList();
    }

    public async Task ClearAllArticles()
    {
        _db.Articles.RemoveRange(_db.Articles);
        await _imageService.ClearImageCache();
        await _db.SaveChangesAsync();
    }
    
    public async Task ClearArticlesOlderThan(DateTime dateTime)
    {
        var articlesToDelete = await _db.Articles.Where(x => x.PublicationDate < dateTime).ToListAsync();
        _db.Articles.RemoveRange(articlesToDelete);
        await _db.SaveChangesAsync();
        await _imageService.ClearArticleImages(articlesToDelete);
    }
}