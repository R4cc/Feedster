using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using SimpleFeedReader;
using Feed = Feedster.DAL.Models.Feed;

namespace Feedster.DAL.Services
{
    public class RssFetchService
    {
        private readonly FeedRepository _feedRepository;
        private readonly ArticleRepository _articleRepository;
        public RssFetchService( FeedRepository feedRepository, ArticleRepository articleRepository)
        {
            _feedRepository = feedRepository;
            _articleRepository = articleRepository;
        }

        public async Task RefreshFeeds()
        {
            List<Feed> feeds = await _feedRepository.GetAll();
            List<Article> articlesToUpdate = new();

            foreach (var feed in feeds)
            {
                    articlesToUpdate.AddRange(await FetchFeedArticles(feed));
            }

            await UpdateArticles(articlesToUpdate);
        }

        public async Task<List<Article>> FetchFeedArticles(Feed feed)
        {
            // get existing articles to match
            Dictionary<string, Article> existingArticles =
                (await _articleRepository.GetAll()).ToDictionary(keySelector: x => x.Guid, elementSelector: x => x);
            
            var reader = new FeedReader();

            var results = reader.RetrieveFeed(feed.RssUrl);

            // loop thoruh all results and add them to a list
            foreach (var result in results)
            {
                if (existingArticles.Any(x => x.Key == result.Id))
                {
                    // skip because it has already been loaded
                    continue;
                }
                
                feed.Articles.Add(new Article()
                {
                    Guid = String.IsNullOrEmpty(result.Uri.ToString()) ? null : result.Uri.ToString(),
                    Description = String.IsNullOrEmpty(result.Content) ? null : result.Content,
                    Title   = String.IsNullOrEmpty(result.Title) ? null : result.Title,
                    ImageUrl = result.Images is null || !result.Images.Any() ? null  : result.Images.First().ToString(),
                    PublicationDate = result.PublishDate.DateTime,
                    ArticleLink = String.IsNullOrEmpty(result.Uri.ToString()) ? null : result.Uri.ToString(),
                    FeedId = feed.FeedId
                });
            }
            
            return feed.Articles;
        }

        private async Task UpdateArticles(List<Article> articles)
        { 
            await _articleRepository.UpdateRange(articles);
        }
    }
}
