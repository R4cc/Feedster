using System.Drawing;
using System.Net.Mime;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Feedster.DAL.BackgroundServices;
using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using ImageMagick;
using Feed = Feedster.DAL.Models.Feed;

namespace Feedster.DAL.Services
{
    public class RssFetchService
    {
        private readonly ArticleRepository _articleRepository;
        private readonly UserRepository _userRepo;
        private readonly ImageService _imageService;
        private UserSettings _userSettings;

        public RssFetchService(ArticleRepository articleRepository, UserRepository userRepo, ImageService imageSerivce)
        {
            _articleRepository = articleRepository;
            _userRepo = userRepo;
            _imageService = imageSerivce;
        }
        
        public async Task RefreshFeeds(List<Feed> feeds)
        {
            List<Article> articlesToUpdate = new();

            foreach (var feed in feeds)
            {
                articlesToUpdate.AddRange(await FetchFeedArticles(feed));
            }

            await _articleRepository.UpdateRange(articlesToUpdate);
        }

        private async Task<SyndicationFeed> ReadXml(string rssUrl)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Ignore;
            settings.IgnoreWhitespace = true;
                
            XmlReader reader = XmlReader.Create(rssUrl, settings);
            SyndicationFeed result = SyndicationFeed.Load(reader);
            reader.Close();

            return result;
        }
        
        private async Task<List<Article>> FetchFeedArticles(Feed feed)
        {
            _userSettings = await _userRepo.Get();
            
            // get existing articles to match
            Dictionary<string, Article> existingArticles =
                (feed.Articles).ToDictionary(keySelector: x => x.ArticleLink,
                    elementSelector: x => x);

            List<Article> ArticlesToUpdate = new();

            SyndicationFeed result = await ReadXml(feed.RssUrl);

            // loop through all results and add them to a list
            foreach (var itm in result.Items)
            {
                try
                {
                    // Checks if article is older than max expiration setting
                    if (itm.PublishDate.DateTime != DateTime.MinValue && 
                        itm.PublishDate.DateTime < DateTime.Now.AddDays(-_userSettings.ArticleExpirationAfterDays))
                    {
                        continue;
                    }
                    
                    var articleLink = itm.Links.ToList()[0].Uri.ToString();
                    
                    // article already exists in DB
                    if (existingArticles.ContainsKey(articleLink))
                    {
                        var article = existingArticles.GetValueOrDefault(articleLink);
                        
                        if (String.IsNullOrEmpty(article.ImageUrl))
                        {
                            continue;
                        }

                        if (String.IsNullOrEmpty(article.ImagePath))
                        {
                            article.ImagePath = Path.GetFileName(article.ImageUrl);
                        }
                        
                        // download the image if it doesnt exist in the cache
                        if (!File.Exists("images/" + article.ImagePath) && _userSettings.DownloadImages)
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

                    List<string> imageUrls = GetAllImageUrls(itm);
                    string highestResImageUrl = string.Empty;
                    string highestResImagePath = string.Empty;
                    
                    // Find the highest Resolution image in array of multiple images
                    if (_userSettings.DownloadImages && imageUrls.Any())
                    {
                        highestResImageUrl = await GetHighestResolutionImage(imageUrls);
                        highestResImagePath= await DownloadFileAsync(highestResImageUrl, Path.GetFileName(highestResImageUrl));
                        
                        //await _imageService.CompressImage(highestResImagePath);
                    }

                    ArticlesToUpdate.Add(new Article()
                    {
                        Guid = itm.Id,
                        Description = StripTagsRegex(itm.Summary.Text),
                        Title = itm.Title.Text,
                        ImagePath = highestResImagePath,
                        ImageUrl = highestResImageUrl,
                        PublicationDate = (itm.PublishDate.DateTime == DateTime.MinValue ? DateTime.Now : itm.PublishDate.DateTime),
                        ArticleLink = !itm.Links.ToList().Any() ? null : itm.Links.ToList()[0].Uri.ToString(),
                        FeedId = feed.FeedId,
                        Tags = itm.Categories.Select(x => x.Name).ToArray()
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    // ignored
                }
            }

            return ArticlesToUpdate;
        }

        /// <summary>
        /// Take a syndication Item and extract all images (jpg, jpeg, png, gif)
        /// </summary>
        /// <param name="itm">SyndicationItem</param>
        /// <returns>String list of all image URLs</returns>
        private static List<string> GetAllImageUrls(SyndicationItem itm)
        {
            List<string> imageUrls = new();
            
            foreach (SyndicationElementExtension extension in itm.ElementExtensions)
            {
                XElement element = extension.GetObject<XElement>();

                if (element.HasAttributes)
                {
                    foreach (var attribute in element.Attributes())
                    {
                        string value = attribute.Value;
                        if (value.ToLower().StartsWith("http") && (value.ToLower().EndsWith(".jpg") || value.ToLower().EndsWith(".png") ||
                                                                   value.ToLower().EndsWith(".gif") || value.ToLower().EndsWith(".jpeg")))
                        {
                            imageUrls.Add(value);
                        }
                    }
                }
            }

            return imageUrls;
        }

        private async Task<string> DownloadFileAsync(string uri, string outputPath)
        {
            try
            {
                if (string.IsNullOrEmpty(uri))
                {
                    return String.Empty;
                }

                // Remove special chars
                var normalizedFilename = Regex.Replace(Path.GetFileNameWithoutExtension(outputPath), "(?:[^a-z0-9 ]|(?<=['\"])s)", "");
                
                // add "seed" to differentiate between images with the same filename
                normalizedFilename += "-" + new Random().Next(10000, 100000);
                //outputPath = normalizedFilename + Path.GetExtension(outputPath);
                outputPath = normalizedFilename + ".webp";
                
                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("rss-reader/1.0 bot");

                if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                    throw new InvalidOperationException("URI is invalid.");

                byte[] fileBytes = await httpClient.GetByteArrayAsync(uri);

                // Resize image, then save it to disk
                await File.WriteAllBytesAsync("./images/" + outputPath, await _imageService.ResizeImage(fileBytes));
                
                return outputPath;
            }
            catch(Exception e)
            {
                // most likely 403 No access so ignore
            }

            return String.Empty;
        }

        private async Task<string> GetHighestResolutionImage(List<string> Images)
        {
            if (Images.Count == 1)
            {
                return Images.First();
            }
            
            string highestResolutionImage = String.Empty;
            int highestResolution = 0;
            
            foreach (var img in Images)
            {
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("rss-reader/1.0 bot");
                        
                    // download image and pass it to the drawer
                    using (var imgStream = new MemoryStream(await httpClient.GetByteArrayAsync(img)))
                    {
                        using (var image = new MagickImage(imgStream))
                        {
                            if (image.Height * image.Width > highestResolution)
                            {
                                highestResolution = image.Height * image.Width;
                                highestResolutionImage = img;
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
        
        private static string StripTagsRegex(string source)
        {
            return Regex.Replace(source, "<.*?>", string.Empty);
        }
    }
}
