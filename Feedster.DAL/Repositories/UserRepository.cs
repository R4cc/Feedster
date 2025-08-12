using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

/// <summary>
/// Entity Framework implementation of <see cref="IUserRepository"/>.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _db;

    public UserRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserSettings> Get(CancellationToken cancellationToken = default)
    {
        return await _db.UserSettings.AsNoTracking().FirstAsync(cancellationToken);
    }

    public async Task Update(UserSettings userSettings, CancellationToken cancellationToken = default)
    {
        _db.UserSettings.Update(userSettings);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
