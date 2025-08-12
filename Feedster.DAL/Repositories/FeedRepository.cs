using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Feedster.DAL.Services;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

public class FeedRepository
{
    private readonly ApplicationDbContext _db;
    private readonly RssFetchService _rssFetchService;

    public FeedRepository(ApplicationDbContext db, RssFetchService rssFetchService)
    {
        _db = db;
        _rssFetchService = rssFetchService;
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

    public async Task<Feed?> Get(int id)
    {
        return await _db.Feeds.Include(f => f.Articles).FirstOrDefaultAsync(f => f.FeedId == id);
    }

    public async Task Remove(Feed feed)
    {
        _db.Feeds.Remove(feed);
        await _db.SaveChangesAsync();
    }

    public async Task<int?> FetchFeed(Feed feed)
    {
        return await _rssFetchService.RefreshFeed(feed);
    }

    internal void Dispose()
    {
        _db.Dispose();
    }
}