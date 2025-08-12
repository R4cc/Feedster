using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

/// <summary>
/// Entity Framework implementation of <see cref="IFolderRepository"/>.
/// </summary>
public class FolderRepository : IFolderRepository
{
    private readonly ApplicationDbContext _db;

    public FolderRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<Folder>> GetAll(CancellationToken cancellationToken = default)
    {
        return await _db.Folders
            .AsNoTracking()
            .Include(g => g.Feeds)
            .ToListAsync(cancellationToken);
    }

    public async Task Create(Folder folder, CancellationToken cancellationToken = default)
    {
        await _db.Folders.AddAsync(folder, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Folder?> Get(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FolderId == id, cancellationToken);
    }

    public async Task Update(Folder folder, CancellationToken cancellationToken = default)
    {
        _db.Folders.Update(folder);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task Remove(Folder folder, CancellationToken cancellationToken = default)
    {
        _db.Folders.Remove(folder);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
