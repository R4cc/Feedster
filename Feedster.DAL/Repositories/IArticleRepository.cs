using Feedster.DAL.Models;
namespace Feedster.DAL.Repositories;

/// <summary>
/// Provides data access for <see cref="Article"/> entities.
/// </summary>
public interface IArticleRepository
{
    /// <summary>Returns all articles including their associated feed.</summary>
    Task<List<Article>> GetAll(CancellationToken cancellationToken = default);

    /// <summary>Persists updates for the provided articles.</summary>
    Task UpdateRange(List<Article> articles, CancellationToken cancellationToken = default);

    /// <summary>Gets all articles from feeds within a specific folder.</summary>
    Task<List<Article>> GetFromFolderId(int id, CancellationToken cancellationToken = default);

    /// <summary>Deletes all articles and clears cached images.</summary>
    Task ClearAllArticles(CancellationToken cancellationToken = default);

    /// <summary>Deletes all articles older than the supplied date.</summary>
    Task ClearArticlesOlderThan(DateTime dateTime, CancellationToken cancellationToken = default);
}
