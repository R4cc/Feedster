using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

public class GroupRepository
{
    private readonly ApplicationDbContext _db;
    public GroupRepository(ApplicationDbContext db)
    {
        _db = db;
    }
    public async Task<List<Group>> GetAll()
    {
        return await _db.Groups.Include(g => g.Feeds).ToListAsync();
    }
    public async Task Create(Group group)
    {
        await _db.Groups.AddAsync(group);
    }
    public async Task<Group> Get(int id)
    {
        return await _db.Groups.FirstOrDefaultAsync(f => f.GroupId == id);
    }
    public async Task Update(Group group)
    {
        _db.Groups.Update(group);
        await _db.SaveChangesAsync();
    }
}