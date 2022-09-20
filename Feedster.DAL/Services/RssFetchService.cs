using System.Drawing;
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

        public async Task RefreshFeed(Feed feed)
        {
            List<Article> articlesToUpdate = new();
            articlesToUpdate.AddRange(await FetchFeedArticles(feed));

            await UpdateArticles(articlesToUpdate);
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
                    (feed.Articles).ToDictionary(keySelector: x => x.ArticleLink,
                        elementSelector: x => x);

                List<Article> ArticlesToUpdate = new();

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Ignore;
                settings.IgnoreWhitespace = true;
                
                XmlReader reader = XmlReader.Create(feed.RssUrl, settings);
                SyndicationFeed result = SyndicationFeed.Load(reader);
                reader.Close();

                // loop through all results and add them to a list
                foreach (var itm in result.Items)
                {
                    var articleLink = itm.Links.ToList()[0].Uri.ToString();
                    
                    if (existingArticles.ContainsKey(articleLink))
                    {
                        var article = existingArticles.GetValueOrDefault(articleLink);
                        
                        if (String.IsNullOrEmpty(article.ImageUrl))
                        {
                            continue;
                        }

                        // donwload the image if it doesnt exist in the cache
                        if (!File.Exists("images/" + article.ImagePath))
                        {
                            article.ImagePath = await DownloadFileAsync(article.ImageUrl, article.ImagePath);
                            
                            if (!string.IsNullOrEmpty(article.ImagePath))
                            {
                                ArticlesToUpdate.Add(article);
                            }
                        }
                        
                        // skip item cus it already exists
                        continue;
                    }

                    //List<string> imagePaths = new();
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
                                    imageUrls.Add(value);
                                }
                            }
                        }
                    }
                    
                    // Find the highest Resolution image in array of multiple image
                    string highestResImageUrl = (imageUrls.Count > 1 ? await GetHighestResolutionImage(imageUrls) : (imageUrls.Any() ? imageUrls.First() : string.Empty));
                    string highestResImagePath = (highestResImageUrl == string.Empty ? String.Empty : await DownloadFileAsync(highestResImageUrl, Path.GetFileName(highestResImageUrl))) ;
                    
                    ArticlesToUpdate.Add(new Article()
                    {
                        Guid = itm.Id,
                        Description = String.IsNullOrEmpty(itm.Summary.Text) ? null : (itm.Summary.Text.Contains("</a>") ? null : itm.Summary.Text),
                        Title = String.IsNullOrEmpty(itm.Title.Text) ? null : itm.Title.Text,
                        ImagePath = highestResImagePath == String.Empty ? null : highestResImagePath,
                        ImageUrl = !imageUrls.Any() ? null : highestResImageUrl,
                        PublicationDate = itm.PublishDate.DateTime,
                        ArticleLink = !itm.Links.ToList().Any() ? null : itm.Links.ToList()[0].Uri.ToString(),
                        FeedId = feed.FeedId,
                        Tags = itm.Categories.Select(x => x.Name).ToArray()
                    });
                }

                return ArticlesToUpdate;
            }
            catch(Exception e)
            {
                Console.WriteLine("failed");
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
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("rss-reader/1.0 bot");

                if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                    throw new InvalidOperationException("URI is invalid.");

                byte[] fileBytes = await httpClient.GetByteArrayAsync(uri);
                await File.WriteAllBytesAsync("images/" + outputPath, fileBytes);
                
                return outputPath;
            }
            catch
            {
                // most likely 403 No access so ignore
            }

            return String.Empty;
        }

        private async Task<string> GetHighestResolutionImage(List<string> Images)
        {
            string highestResolutionImage = String.Empty;
            int highestResolution = 0;
            
            foreach (var img in Images)
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("rss-reader/1.0 bot");
                        
                        // download image and pass it to the drawer
                        using (var imgStream = new MemoryStream(await httpClient.GetByteArrayAsync(img)))
                        {
                            using (var image = Image.FromStream(imgStream))
                            {
                                if (image.Height * image.Width > highestResolution)
                                {
                                    highestResolution = image.Height * image.Width;
                                    highestResolutionImage = img;
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // img download probably failed; continue
                }
            }
            return highestResolutionImage;
        }
    }
}
