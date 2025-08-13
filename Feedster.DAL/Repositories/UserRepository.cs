using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace Feedster.DAL.Repositories;

public class UserRepository : IDisposable
{
    private readonly ApplicationDbContext _db;

    public UserRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserSettings> Get()
    {
        return await _db.UserSettings.FirstAsync();
    }

    public async Task Update(UserSettings _userSettings)
    {
        _db.UserSettings.Update(_userSettings);
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