using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

public class UserRepository
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

    internal void Dispose()
    {
        _db.Dispose();
    }
}