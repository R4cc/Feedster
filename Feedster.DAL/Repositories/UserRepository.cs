using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace Feedster.DAL.Repositories;

public class UserRepository(ApplicationDbContext db) : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public async Task<UserSettings> Get()
    {
        return await db.UserSettings.FirstAsync();
    }

    public async Task Update(UserSettings userSettings)
    {
        db.UserSettings.Update(userSettings);
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
