using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace Feedster.DAL.Repositories;

public class FolderRepository(ApplicationDbContext db) : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public async Task<List<Folder>> GetAll()
    {
        return await db.Folders.Include(g => g.Feeds).ToListAsync();
    }

    public async Task Create(Folder folder)
    {
        await db.Folders.AddAsync(folder);
        await db.SaveChangesAsync();
    }

    public async Task<Folder?> Get(int id)
    {
        return await db.Folders.FirstOrDefaultAsync(f => f.FolderId == id);
    }

    public async Task Update(Folder folder)
    {
        db.Folders.Update(folder);
        await db.SaveChangesAsync();
    }

    public async Task Remove(Folder folder)
    {
        db.Folders.Remove(folder);
        await db.SaveChangesAsync();
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
