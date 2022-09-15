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
            var group = builder.Entity<Group>();

            group.HasData(new Group()
            {
                GroupId = 1,
                Name = "Tech News"
            });
            
            group.HasData(new Group()
            {
                GroupId = 2,
                Name = "Local News"
            });
            
            group.HasData(new Group()
            {
                GroupId =3,
                Name = "Security And Privacy"
            });
            
            
            feed.HasData(new Feed()
            {
                FeedId = 1,
                Name = "ycombinator",
                RssUrl = "https://news.ycombinator.com/rss",
                Groups = new List<Group>() 
            });
            
            
            feed.HasData(new Feed()
            {
                FeedId = 2,
                Name = "wired",
                RssUrl = "https://wired.com/feed/rss",
                Groups = new List<Group>() 
            });
            
            feed.HasData(new Feed()
            {
                FeedId = 3,
                Name = "Threatpost",
                RssUrl = "https://threatpost.com/feed/",
                Groups = new List<Group>()
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
        public virtual DbSet<Group> Groups { get; set; }
    }
}