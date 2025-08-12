using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Feedster.DAL.Models;
using Feedster.DAL.Repositories;
using ImageMagick;
using Feed = Feedster.DAL.Models.Feed;

namespace Feedster.DAL.Services
{
    public class RssFetchService
    {
        private readonly IArticleRepository _articleRepository;
        private readonly IUserRepository _userRepo;
        private readonly ImageService _imageService;
        private UserSettings? _userSettings;

        public RssFetchService(IArticleRepository articleRepository, IUserRepository userRepo, ImageService imageSerivce)
        {
            _articleRepository = articleRepository;
            _userRepo = userRepo;
            _imageService = imageSerivce;
        }

        public async Task RefreshFeeds(List<Feed> feeds, CancellationToken cancellationToken = default)
        {
            List<Article> articlesToUpdate = new();

            foreach (var feed in feeds)
            {
                var articles = await FetchFeedArticles(feed, cancellationToken);
                if (articles is not null)
                {
                    articlesToUpdate.AddRange(articles);
                }
            }

            await _articleRepository.UpdateRange(articlesToUpdate, cancellationToken);
        }

        public async Task<int?> RefreshFeed(Feed feed, CancellationToken cancellationToken = default)
        {
            var articles = await FetchFeedArticles(feed, cancellationToken);
            if (articles is null)
            {
                return null;
            }

            await _articleRepository.UpdateRange(articles, cancellationToken);
            return articles.Count;
        }

        private static async Task<(bool Success, SyndicationFeed Feed)> ReadXml(string rssUrl, CancellationToken cancellationToken)
        {
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Ignore;
                settings.IgnoreWhitespace = true;

                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                using var stream = await httpClient.GetStreamAsync(rssUrl, cancellationToken);
                using var reader = XmlReader.Create(stream, settings);

                SyndicationFeed result = SyndicationFeed.Load(reader);
                return (true, result);
            }
            catch
            {
                // ignored
            }

            return (false, new SyndicationFeed());
        }

        private async Task<List<Article>?> FetchFeedArticles(Feed feed, CancellationToken cancellationToken)
        {
            _userSettings = await _userRepo.Get(cancellationToken);

            // get existing articles to match
            Dictionary<string, Article> existingArticles =
                (feed!.Articles!).ToDictionary(keySelector: x => x.ArticleLink,
                    elementSelector: x => x);

            List<Article> articlesToUpdate = new();

            var (success, result) = await ReadXml(feed.RssUrl, cancellationToken);
            if (!success)
            {
                return null;
            }

            // loop through all results and add them to a list
            foreach (var itm in result.Items)
            {
                try
                {
                    // Checks if article is older than max expiration setting
                    if (itm.PublishDate.DateTime != DateTime.MinValue
                        && _userSettings.ArticleExpirationAfterDays != 0 
                        && itm.PublishDate.DateTime < DateTime.Now.AddDays(-_userSettings.ArticleExpirationAfterDays))
                    {
                        continue;
                    }

                    string articleLink = itm.Links.ToList()[0].Uri.ToString();

                    // article already exists in DB
                    if (existingArticles.ContainsKey(articleLink) && articleLink != string.Empty)
                    {
                        Article? article = existingArticles.GetValueOrDefault(articleLink);

                        if (article is null || string.IsNullOrEmpty(article.ImageUrl))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(article.ImagePath))
                        {
                            article.ImagePath = Path.GetFileName(article.ImageUrl);
                        }

                        // download the image if it doesnt exist in the cache
                        if (!File.Exists("images/" + article.ImagePath) && _userSettings.DownloadImages)
                        {
                            article.ImagePath = await DownloadFileAsync(article.ImageUrl, article.ImagePath, cancellationToken);

                            if (!string.IsNullOrEmpty(article.ImagePath))
                            {
                                articlesToUpdate.Add(article);
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
                        highestResImageUrl = await GetHighestResolutionImage(imageUrls, cancellationToken);
                        highestResImagePath = await DownloadFileAsync(highestResImageUrl, Path.GetFileName(highestResImageUrl), cancellationToken);

                        //await _imageService.CompressImage(highestResImagePath);
                    }

                    articlesToUpdate.Add(new Article()
                    {
                        Guid = itm.Id,
                        Description = StripTagsRegex(itm.Summary.Text),
                        Title = itm.Title.Text,
                        ImagePath = highestResImagePath,
                        ImageUrl = highestResImageUrl,
                        PublicationDate = (itm.PublishDate.DateTime == DateTime.MinValue ? DateTime.Now : itm.PublishDate.DateTime),
                        ArticleLink = articleLink,
                        FeedId = feed.FeedId,
                        Tags = itm.Categories.Select(x => x.Name).ToArray()
                    });
                }
                catch
                {
                    // ignored
                }
            }

            return articlesToUpdate;
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

                if (!element.HasAttributes) continue;

                foreach (var attribute in element.Attributes())
                {
                    string value = attribute.Value;
                    if (IsImageUrl(value))
                    {
                        imageUrls.Add(value);
                    }
                }
            }

            foreach (var link in itm.Links)
            {
                if (link.RelationshipType == "enclosure" || (link.MediaType?.StartsWith("image") ?? false))
                {
                    string url = link.Uri.ToString();
                    if (IsImageUrl(url))
                    {
                        imageUrls.Add(url);
                    }
                }
            }

            return imageUrls;
        }

        private static bool IsImageUrl(string value)
        {
            string lower = value.ToLower();
            return lower.StartsWith("http") && (lower.EndsWith(".jpg") || lower.EndsWith(".png") || lower.EndsWith(".gif") ||
                                                lower.EndsWith(".jpeg") || lower.EndsWith(".webp"));
        }

        private async Task<string> DownloadFileAsync(string uri, string outputPath, CancellationToken cancellationToken)
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
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                    throw new InvalidOperationException("URI is invalid.");

                byte[] fileBytes = await httpClient.GetByteArrayAsync(uri, cancellationToken);

                // Resize image, then save it to disk
                await File.WriteAllBytesAsync("./images/" + outputPath, _imageService.ResizeImage(fileBytes), cancellationToken);

                return outputPath;
            }
            catch
            {
                // most likely 403 No access so ignore
            }

            return String.Empty;
        }

        private async Task<string> GetHighestResolutionImage(List<string> Images, CancellationToken cancellationToken)
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
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                    // download image and pass it to the drawer
                    using var imgStream = new MemoryStream(await httpClient.GetByteArrayAsync(img, cancellationToken));
                    using var image = new MagickImage(imgStream);
                    
                    if (image.Height * image.Width <= highestResolution) continue;
                            
                    highestResolution = (int)(image.Height * image.Width);
                    highestResolutionImage = img;
                }
                catch
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
