using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using CodeHollow.FeedReader;
using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
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
            List<Feed> feeds = new() {await _feedRepository.Get(3)};
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
            
            

            var result = await FeedReader.ReadAsync(feed.RssUrl);

            // loop thoruh all results and add them to a list
            foreach (var itm in result.Items)
            {
                
                if (existingArticles.Any(x => x.Key == result.Link))
                {
                    // skip because it has already been loaded
                    continue;
                }

                string image = String.Empty;

                // Loop thorugh xElements until img is found
                foreach (var extension in itm.SpecificItem.Element.Elements())
                {
                    XElement element = extension;

                    if (element.HasAttributes)
                    {
                        var elements = element.ElementsAfterSelf();
                        foreach (var attribute in elements)
                        {
                            string value = attribute.ToString();

                            string extractedUrl = await ExtractUrlFromString(value);
                            if (extractedUrl != string.Empty)
                            {
                                image = extractedUrl; 
                            }
                        }                                
                    }                            
                }    
                
                feed.Articles.Add(new Article()
                {
                    Guid = String.IsNullOrEmpty(itm.Link.ToString()) ? null : itm.Link.ToString(),
                    Description = String.IsNullOrEmpty(itm.Description) ? null : itm.Description,
                    Title   = String.IsNullOrEmpty(itm.Title) ? null : itm.Title,
                    ImageUrl = String.IsNullOrEmpty(image) ? null  : image,
                    PublicationDate = itm.PublishingDate,
                    ArticleLink = String.IsNullOrEmpty(itm.Link) ? null : itm.Link,
                    FeedId = feed.FeedId
                });
            }
            
            return feed.Articles;
        }

        private async Task UpdateArticles(List<Article> articles)
        { 
            await _articleRepository.UpdateRange(articles);
        }

        private async Task<String> ExtractUrlFromString(string url)
        {
            if (!url.Contains("https://") && !url.Contains("https://"))
            {
                return String.Empty;
            }
            
            int indexStart = url.IndexOf("url=\"http");
            
            int indexEnd;
            switch (url)
            {
                case string value when url.Contains(".jpg"):
                    indexEnd = value.IndexOf(".jpg")+4;
                    break;
                case string value when url.Contains(".png"):
                    indexEnd = value.IndexOf(".png")+4;
                    break;
                case string value when url.Contains(".jpeg"):
                    indexEnd = value.IndexOf(".jpeg") +5;
                    break;
                case string value when url.Contains(".gif"):
                    indexEnd = value.IndexOf(".gif") +4;
                    break;
                case string value when url.Contains(".webp"):
                    indexEnd = value.IndexOf(".webp") +5;
                    break;
                default:
                    return String.Empty;
            }

            string returnUrl = url.Substring(indexStart, indexEnd - indexStart);
            return returnUrl.Split("url=\"")[1];
        }
    }
}
