using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

public class FeedRepository
{
    private readonly ApplicationDbContext _db;
    public FeedRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<Feed>> GetAll()
    {
        return await _db.Feeds.Include(f => f.Articles).ToListAsync();
    }    
    
    public async Task Create(Feed feed)
    {
        await _db.Feeds.AddAsync(feed);
        await _db.SaveChangesAsync();
    }    
    
    public async Task Update(Feed feed)
    {
        _db.Feeds.Update(feed);
        await _db.SaveChangesAsync();
    }
    
    public async Task<Feed> Get(int id)
    {
        return await _db.Feeds.Include(f => f.Articles).FirstOrDefaultAsync(f => f.FeedId == id);
    }
    public async Task Remove(Feed feed)
    {
        _db.Feeds.Remove(feed);
        await _db.SaveChangesAsync();
    }    
}