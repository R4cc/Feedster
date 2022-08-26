using System;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;
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
                (await _articleRepository.GetAll()).ToDictionary(keySelector: x => x.ArticleLink, elementSelector: x => x);

            XmlReader reader = XmlReader.Create(feed.RssUrl);
            SyndicationFeed result = SyndicationFeed.Load(reader);
            reader.Close();

            // loop through all results and add them to a list
            foreach (var itm in result.Items)
            {
                if (existingArticles.Any(x => x.Key == itm.Links.ToList()[0].Uri.ToString()))
                {
                    // skip item cus it already exists
                    continue;
                }
                
                List<string> images = new();

                foreach (SyndicationElementExtension extension in itm.ElementExtensions)
                {
                    XElement element = extension.GetObject<XElement>();

                    if (element.HasAttributes)
                    {
                        foreach (var attribute in element.Attributes())
                        {
                            string value = attribute.Value.ToLower();
                            if (value.StartsWith("http") && (value.EndsWith(".jpg") || value.EndsWith(".png") || value.EndsWith(".gif") || value.EndsWith(".jpeg")))
                            {
                                images.Add(value); // Add here the image link to some array
                            }
                        }                                
                    }                            
                }
                feed.Articles.Add(new Article()
                {
                    Guid = itm.Id is null ? null : itm.Id ,
                    Description = String.IsNullOrEmpty(itm.Summary.Text) ? null : (itm.Summary.Text.Contains("</a>") ? null : itm.Summary.Text),
                    Title   = String.IsNullOrEmpty(itm.Title.Text) ? null : itm.Title.Text,
                    ImageUrl = images?.Count() == 0 ? null  : images.OrderBy(x => x.Length).ToList()[0],
                    PublicationDate = itm.PublishDate.DateTime,
                    ArticleLink = itm.Links.ToList().Count() == 0 ? null : itm.Links.ToList()[0].Uri.ToString(),
                    FeedId = feed.FeedId,
                    Tags = itm.Categories.Select(x => x.Name).ToArray()
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
