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
        var list = await _db.Articles.Include(a => a.Feed).ToListAsync();
        //foreach (var article in list)
        //{
        //    _db.Entry(article).State = EntityState.Detached;
        //}
        return list;
    }    
    
    public async Task Create(Article article)
    {
        await _db.Articles.AddAsync(article);
    }    
    
    public async Task UpdateRange(List<Article> articles)
    {
        _db.Articles.UpdateRange(articles);
        _db.SaveChangesAsync();
    }
    
    public async Task<List<Article>> GetFromGroupId(int id)
    {
        return _db.Articles.Where(f => f.Feed.Groups.Any(x => x.GroupId == id)).ToList();
    }
    
    public async Task<Article> Get(int id)
    {
        return await _db.Articles.FirstOrDefaultAsync(f => f.FeedId == id);
    }
}