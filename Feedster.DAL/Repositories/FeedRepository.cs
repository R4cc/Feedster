using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Feedster.DAL.Services;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

/// <summary>
/// Entity Framework implementation of <see cref="IFeedRepository"/>.
/// </summary>
public class FeedRepository : IFeedRepository
{
    private readonly ApplicationDbContext _db;
    private readonly RssFetchService _rssFetchService;

    public FeedRepository(ApplicationDbContext db, RssFetchService rssFetchService)
    {
        _db = db;
        _rssFetchService = rssFetchService;
    }

    public async Task<List<Feed>> GetAll(CancellationToken cancellationToken = default)
    {
        return await _db.Feeds
            .AsNoTracking()
            .Include(f => f.Articles)
            .ToListAsync(cancellationToken);
    }

    public async Task Create(Feed feed, CancellationToken cancellationToken = default)
    {
        await _db.Feeds.AddAsync(feed, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(Feed feed, CancellationToken cancellationToken = default)
    {
        _db.Feeds.Update(feed);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Feed?> Get(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Feeds
            .AsNoTracking()
            .Include(f => f.Articles)
            .FirstOrDefaultAsync(f => f.FeedId == id, cancellationToken);
    }

    public async Task Remove(Feed feed, CancellationToken cancellationToken = default)
    {
        _db.Feeds.Remove(feed);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int?> FetchFeed(Feed feed, CancellationToken cancellationToken = default)
    {
        return await _rssFetchService.RefreshFeed(feed, cancellationToken);
    }
}
