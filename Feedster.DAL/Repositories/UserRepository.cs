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
    public async Task<UserSettings> Get(int userId)
    {
        return await _db.UserSettings.FirstOrDefaultAsync(u => u.UserSettingsId == userId);
    }
}