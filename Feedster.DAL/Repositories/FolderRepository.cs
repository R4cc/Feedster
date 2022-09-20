using Feedster.DAL.Data;
using Feedster.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Repositories;

public class FolderRepository
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
    }
    public async Task<Folder> Get(int id)
    {
        return await _db.Folders.FirstOrDefaultAsync(f => f.FolderId == id);
    }
    public async Task Update(Folder folder)
    {
        _db.Folders.Update(folder);
        await _db.SaveChangesAsync();
    }
}