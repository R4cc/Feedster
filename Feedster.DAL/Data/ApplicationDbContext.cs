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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            var feed = builder.Entity<Feed>();

            feed.HasData(new Feed()
            {
                FeedId = 1,
                Name = "ycombinator",
                RssUrl = "https://news.ycombinator.com/rss"
            });
            
            
            feed.HasData(new Feed()
            {
                FeedId = 2,
                Name = "wired",
                RssUrl = "https://wired.com/feed/rss"
            });
            
            
            feed.HasData(new Feed()
            {
                FeedId = 3,
                Name = "Threatpost",
                RssUrl = "https://threatpost.com/feed/"
            });
            
            base.OnModelCreating(builder);
        }

        public virtual DbSet<Feed> Feeds { get; set; }
        public virtual DbSet<Tag> Tags { get; set; }
        public virtual DbSet<Article> Articles { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
    }
}