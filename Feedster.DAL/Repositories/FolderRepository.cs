using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace Feedster.DAL.Repositories;

public class FolderRepository : IDisposable
{
    private readonly ApplicationDbContext _db;

    public FolderRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<Folder>> GetAll()
    {
        return await _db.Folders.Include(g => g.Feeds).ToListAsync();
    }

    public async Task Create(Folder folder)
    {
        await _db.Folders.AddAsync(folder);
        await _db.SaveChangesAsync();
    }

    public async Task<Folder?> Get(int id)
    {
        return await _db.Folders.FirstOrDefaultAsync(f => f.FolderId == id);
    }

    public async Task Update(Folder folder)
    {
        _db.Folders.Update(folder);
        await _db.SaveChangesAsync();
    }

    public async Task Remove(Folder folder)
    {
        _db.Folders.Remove(folder);
        await _db.SaveChangesAsync();
    }

    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _db.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}