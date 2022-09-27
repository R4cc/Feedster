using Feedster.DAL.BackgroundServices;
using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

public class FeedRepository
{
    private readonly ApplicationDbContext _db;
    private readonly BackgroundJobs _backgroundJobs;
    public FeedRepository(ApplicationDbContext db, BackgroundJobs backgroundJobs)
    {
        _db = db;
        _backgroundJobs = backgroundJobs;
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

    public async Task FetchFeed(Feed feed)
    {
        List<Feed> feeds = new() {feed};
        _backgroundJobs.BackgroundTasks.Enqueue(feeds);
    }
}