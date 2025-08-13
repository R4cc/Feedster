using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Feedster.DAL.Services;
using Microsoft.EntityFrameworkCore;
using System;

namespace Feedster.DAL.Repositories;

public class FeedRepository(ApplicationDbContext db, RssFetchService rssFetchService) : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public async Task<List<Feed>> GetAll()
    {
        return await db.Feeds.Include(f => f.Articles).ToListAsync();
    }

    public async Task Create(Feed feed)
    {
        await db.Feeds.AddAsync(feed);
        await db.SaveChangesAsync();
    }

    public async Task Update(Feed feed)
    {
        db.Feeds.Update(feed);
        await db.SaveChangesAsync();
    }

    public async Task<Feed?> Get(int id)
    {
        return await db.Feeds.Include(f => f.Articles).FirstOrDefaultAsync(f => f.FeedId == id);
    }

    public async Task Remove(Feed feed)
    {
        db.Feeds.Remove(feed);
        await db.SaveChangesAsync();
    }

    public async Task<int?> FetchFeed(Feed feed)
    {
        return await rssFetchService.RefreshFeed(feed);
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
