using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
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

        public RssFetchService(FeedRepository feedRepository, ArticleRepository articleRepository)
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

        private async Task<List<Article>> FetchFeedArticles(Feed feed)
        {
            try
            {
                // get existing articles to match
                Dictionary<string, Article> existingArticles =
                    (await _articleRepository.GetAll()).ToDictionary(keySelector: x => x.ArticleLink,
                        elementSelector: x => x);

                XmlReader reader = XmlReader.Create(feed.RssUrl);
                SyndicationFeed result = SyndicationFeed.Load(reader);
                reader.Close();

                // loop through all results and add them to a list
                foreach (var itm in result.Items)
                {
                    if (existingArticles.Any(x => x.Key == itm.Links.ToList()[0].Uri.ToString()))
                    {
                        var article = existingArticles.Values.FirstOrDefault(x =>
                            x.ArticleLink == itm.Links.ToList()[0].Uri.ToString());
                        
                        if (article.ImageUrl is null)
                        {
                            continue;
                        }

                        // donwload the image if it doesnt exist in the cache
                        if (!File.Exists("images/" + article.ImagePath))
                        {
                            article.ImagePath = await DownloadFileAsync(article.ImageUrl, article.ImagePath);
                            await _articleRepository.Update(article);
                        }
                        
                        // skip item cus it already exists
                        continue;
                    }

                    List<string> imagePaths = new();
                    List<string> imageUrls = new();

                    foreach (SyndicationElementExtension extension in itm.ElementExtensions)
                    {
                        XElement element = extension.GetObject<XElement>();

                        if (element.HasAttributes)
                        {
                            foreach (var attribute in element.Attributes())
                            {
                                string value = attribute.Value.ToLower();
                                if (value.StartsWith("http") && (value.EndsWith(".jpg") || value.EndsWith(".png") ||
                                                                 value.EndsWith(".gif") || value.EndsWith(".jpeg")))
                                {
                                    // Add here the image link to some array
                                    imageUrls.Add(value);
                                    imagePaths.Add(await DownloadFileAsync(value, Path.GetFileName(value)));
                                }
                            }
                        }
                    }

                    feed.Articles.Add(new Article()
                    {
                        Guid = itm.Id,
                        Description = String.IsNullOrEmpty(itm.Summary.Text)
                            ? null
                            : (itm.Summary.Text.Contains("</a>") ? null : itm.Summary.Text),
                        Title = String.IsNullOrEmpty(itm.Title.Text) ? null : itm.Title.Text,
                        ImagePath = !imagePaths.Any() ? null : imagePaths.OrderBy(x => x.Length).ToList()[0],
                        ImageUrl = !imageUrls.Any() ? null : imageUrls.OrderBy(x => x.Length).ToList()[0],
                        PublicationDate = itm.PublishDate.DateTime,
                        ArticleLink = !itm.Links.ToList().Any() ? null : itm.Links.ToList()[0].Uri.ToString(),
                        FeedId = feed.FeedId,
                        Tags = itm.Categories.Select(x => x.Name).ToArray()
                    });
                }

                return feed.Articles;
            }
            catch
            {
                // ignored
            }

            return new List<Article>();
        }

        private async Task UpdateArticles(List<Article> articles)
        {
            await _articleRepository.UpdateRange(articles);
        }

        private static async Task<string> DownloadFileAsync(string uri, string outputPath)
        {
            try
            {
                // Remove special chars
                var normalizedFilename = Regex.Replace(Path.GetFileNameWithoutExtension(outputPath), "(?:[^a-z0-9 ]|(?<=['\"])s)", "");
                outputPath = normalizedFilename + Path.GetExtension(outputPath);
                
                using HttpClient httpClient = new();

                if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                    throw new InvalidOperationException("URI is invalid.");

                byte[] fileBytes = await httpClient.GetByteArrayAsync(uri);
                await File.WriteAllBytesAsync("images/" + outputPath, fileBytes);
                
                return outputPath;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return String.Empty;
        }
    }
}
