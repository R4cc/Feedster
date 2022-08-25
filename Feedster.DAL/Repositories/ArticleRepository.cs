using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

public class ArticleRepository
{
    private readonly ApplicationDbContext _db;
    public ArticleRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<Article>> GetAll()
    {
        return await _db.Articles.ToListAsync();
    }    
    
    public async Task Create(Article article)
    {
        await _db.Articles.AddAsync(article);
    }    
    
    public async Task UpdateRange(List<Article> articles)
    {
        _db.Articles.UpdateRange(articles);
    }
    
    public async Task<Article> Get(int id)
    {
        return await _db.Articles.FirstOrDefaultAsync(f => f.FeedId == id);
    }
}