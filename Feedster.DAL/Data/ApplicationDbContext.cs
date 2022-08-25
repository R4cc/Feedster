using Feedster.DAL.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        
        public virtual DbSet<Feed> Feeds { get; set; }
        public virtual DbSet<Tag> Tags { get; set; }
        public virtual DbSet<Article> Articles { get; set; }
    }
}