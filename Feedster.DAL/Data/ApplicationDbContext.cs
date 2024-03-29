﻿using Feedster.DAL.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Feedster.DAL.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        protected override void OnModelCreating(ModelBuilder builder)
        {
            var feed = builder.Entity<Feed>();
            var folder = builder.Entity<Folder>();
            var userSettings = builder.Entity<UserSettings>();

            userSettings.HasData(new UserSettings()
            {
                UserSettingsId = 1
            });

            folder.HasData(new Folder()
            {
                FolderId = 1,
                Name = "Tech News"
            });

            folder.HasData(new Folder()
            {
                FolderId = 2,
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
                FeedId = 3,
                Name = "Threatpost",
                RssUrl = "https://threatpost.com/feed/",
                Folders = new List<Folder>()
            });

            builder.Entity<Article>()
                .Property(e => e.Tags)
                .HasConversion(
                    v => string.Join(',', v!),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));

            base.OnModelCreating(builder);
        }

        public virtual DbSet<Feed> Feeds { get; set; }
        public virtual DbSet<Article> Articles { get; set; }
        public virtual DbSet<Folder> Folders { get; set; }
        public virtual DbSet<UserSettings> UserSettings { get; set; }
    }
}