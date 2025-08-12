using Feedster.DAL.Models;
namespace Feedster.DAL.Repositories;

/// <summary>
/// Provides data access for <see cref="Folder"/> entities.
/// </summary>
public interface IFolderRepository
{
    /// <summary>Returns all folders including their feeds.</summary>
    Task<List<Folder>> GetAll(CancellationToken cancellationToken = default);

    /// <summary>Creates a new folder.</summary>
    Task Create(Folder folder, CancellationToken cancellationToken = default);

    /// <summary>Gets a folder by identifier.</summary>
    Task<Folder?> Get(int id, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing folder.</summary>
    Task Update(Folder folder, CancellationToken cancellationToken = default);

    /// <summary>Removes the specified folder.</summary>
    Task Remove(Folder folder, CancellationToken cancellationToken = default);
}
