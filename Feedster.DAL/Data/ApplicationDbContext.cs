using Feedster.DAL.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            var feed = builder.Entity<Feed>();
            var folder = builder.Entity<Folder>();

            folder.HasData(new Folder()
            {
                FolderId = 1,
                Name = "Tech News"
            });
            
            folder.HasData(new Folder()
            {
                FolderId = 2,
                Name = "Local News"
            });
            
            folder.HasData(new Folder()
            {
                FolderId =3,
                Name = "Security And Privacy"
            });
            
            
            feed.HasData(new Feed()
            {
                FeedId = 1,
                Name = "ycombinator",
                RssUrl = "https://news.ycombinator.com/rss",
                Folders = new List<Folder>() 
            });
            
            
            feed.HasData(new Feed()
            {
                FeedId = 2,
                Name = "wired",
                RssUrl = "https://wired.com/feed/rss",
                Folders = new List<Folder>() 
            });
            
            feed.HasData(new Feed()
            {
                FeedId = 3,
                Name = "Threatpost",
                RssUrl = "https://threatpost.com/feed/",
                Folders = new List<Folder>()
            });            
            
            feed.HasData(new Feed()
            {
                FeedId = 4,
                Name = "Fefe HTTPS HTML",
                RssUrl = "https://blog.fefe.de/rss.xml?html",
                Folders = new List<Folder>()
            });
            
            builder.Entity<Article>()
                .Property(e => e.Tags)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
            
            base.OnModelCreating(builder);
        }

        public virtual DbSet<Feed> Feeds { get; set; }
        public virtual DbSet<Article> Articles { get; set; }
        public virtual DbSet<Folder> Folders { get; set; }
    }
}