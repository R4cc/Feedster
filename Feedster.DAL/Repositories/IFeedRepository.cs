using Feedster.DAL.Models;
namespace Feedster.DAL.Repositories;

/// <summary>
/// Provides data access for <see cref="Feed"/> entities.
/// </summary>
public interface IFeedRepository
{
    /// <summary>Returns all feeds including their articles.</summary>
    Task<List<Feed>> GetAll(CancellationToken cancellationToken = default);

    /// <summary>Creates a new feed.</summary>
    Task Create(Feed feed, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing feed.</summary>
    Task Update(Feed feed, CancellationToken cancellationToken = default);

    /// <summary>Gets a feed by identifier including its articles.</summary>
    Task<Feed?> Get(int id, CancellationToken cancellationToken = default);

    /// <summary>Removes the specified feed.</summary>
    Task Remove(Feed feed, CancellationToken cancellationToken = default);

    /// <summary>Refreshes the supplied feed from its source.</summary>
    Task<int?> FetchFeed(Feed feed, CancellationToken cancellationToken = default);
}
