using System.Net.Http;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
        private UserSettings? _userSettings;

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
                var articles = await FetchFeedArticles(feed);
                if (articles is not null)
                {
                    articlesToUpdate.AddRange(articles);
                }
            }

            await _articleRepository.UpdateRange(articlesToUpdate);
        }

        public async Task<int?> RefreshFeed(Feed feed)
        {
            var articles = await FetchFeedArticles(feed);
            if (articles is null)
            {
                return null;
            }

            await _articleRepository.UpdateRange(articles);
            return articles.Count;
        }

        private static async Task<(bool Success, SyndicationFeed Feed, bool IsAtom)> ReadXml(string rssUrl)
        {
            try
            {
                var (result, isAtom) = await FeedReader.ReadAsync(rssUrl);
                return (true, result, isAtom);
            }
            catch
            {
                // ignored
            }

            return (false, new SyndicationFeed(), false);
        }

        private async Task<List<Article>?> FetchFeedArticles(Feed feed)
        {
            _userSettings = await _userRepo.Get();

            // get existing articles to match
            var existingArticles = (feed.Articles ?? new())
                .Where(x => !string.IsNullOrEmpty(x.ArticleLink))
                .GroupBy(x => x.ArticleLink).ToDictionary(x => x.Key, x => x.First());
            var existingIds = (feed.Articles ?? new())
                .Where(x => !string.IsNullOrEmpty(x.Guid))
                .GroupBy(x => x.Guid!).ToDictionary(x => x.Key, x => x.First());

            List<Article> articlesToUpdate = new();

            var (success, result, isAtom) = await ReadXml(feed.RssUrl);
            if (!success)
            {
                return null;
            }
            var feedUri = new Uri(feed.RssUrl);

            // loop through all results and add them to a list
            foreach (var itm in result.Items)
            {
                try
                {
                    var publicationDate = itm.PublishDate != DateTimeOffset.MinValue
                        ? itm.PublishDate.LocalDateTime
                        : itm.LastUpdatedTime != DateTimeOffset.MinValue
                            ? itm.LastUpdatedTime.LocalDateTime
                            : DateTime.Now;

                    // Atom's published date is optional; use updated for expiry too.
                    if (_userSettings.ArticleExpirationAfterDays != 0
                        && publicationDate < DateTime.Now.AddDays(-_userSettings.ArticleExpirationAfterDays))
                    {
                        continue;
                    }

                    string articleLink = GetArticleLink(itm, feedUri);
                    if (string.IsNullOrEmpty(articleLink) && string.IsNullOrEmpty(itm.Id))
                    {
                        continue;
                    }

                    // Atom IDs remain stable even when a permalink changes or is absent.
                    Article? article = isAtom && !string.IsNullOrEmpty(itm.Id)
                        ? existingIds.GetValueOrDefault(itm.Id)
                        : null;
                    article ??= existingArticles.GetValueOrDefault(articleLink);
                    if (article is not null)
                    {
                        if (string.IsNullOrEmpty(article.ImageUrl))
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
                            article.ImagePath = await DownloadFileAsync(article.ImageUrl, article.ImagePath);

                            if (!string.IsNullOrEmpty(article.ImagePath))
                            {
                                articlesToUpdate.Add(article);
                            }
                        }

                        // skip item cus it already exists
                        continue;
                    }

                    List<string> imageUrls = GetAllImageUrls(itm, feedUri);
                    string highestResImageUrl = string.Empty;
                    string highestResImagePath = string.Empty;

                    // Find the highest Resolution image in array of multiple images
                    if (_userSettings.DownloadImages && imageUrls.Any())
                    {
                        highestResImageUrl = await GetHighestResolutionImage(imageUrls);
                        highestResImagePath = await DownloadFileAsync(highestResImageUrl, Path.GetFileName(highestResImageUrl));

                        //await _imageService.CompressImage(highestResImagePath);
                    }

                    var newArticle = new Article()
                    {
                        Guid = itm.Id,
                        Description = GetPlainText(itm.Summary ?? itm.Content as TextSyndicationContent, !isAtom),
                        Title = GetPlainText(itm.Title),
                        ImagePath = highestResImagePath,
                        ImageUrl = highestResImageUrl,
                        PublicationDate = publicationDate,
                        ArticleLink = articleLink,
                        FeedId = feed.FeedId,
                        Tags = itm.Categories.Select(x => x.Name).ToArray()
                    };
                    articlesToUpdate.Add(newArticle);
                    if (!string.IsNullOrEmpty(articleLink)) existingArticles[articleLink] = newArticle;
                    if (isAtom && !string.IsNullOrEmpty(itm.Id)) existingIds[itm.Id] = newArticle;
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
        private static List<string> GetAllImageUrls(SyndicationItem itm, Uri feedUri)
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
                if (link.RelationshipType == "enclosure"
                    || (link.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    string url = GetHttpUrl(link.Uri, link.BaseUri, feedUri);
                    if (!string.IsNullOrEmpty(url) && (IsImageUrl(url)
                        || (link.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)))
                    {
                        imageUrls.Add(url);
                    }
                }
            }

            return imageUrls;
        }

        private static bool IsImageUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return false;
            string extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            return extension is ".jpg" or ".png" or ".gif" or ".jpeg" or ".webp";
        }

        private static string GetArticleLink(SyndicationItem item, Uri feedUri)
        {
            // Prefer the human-readable alternate, never self or enclosure links.
            foreach (var link in item.Links
                .Where(x => string.IsNullOrEmpty(x.RelationshipType) || x.RelationshipType == "alternate")
                .OrderBy(x => x.MediaType is "text/html" or "application/xhtml+xml" ? 0
                    : string.IsNullOrEmpty(x.MediaType) ? 1 : 2))
            {
                var url = GetHttpUrl(link.Uri, link.BaseUri ?? item.BaseUri, feedUri);
                if (!string.IsNullOrEmpty(url)) return url;
            }

            return Uri.TryCreate(item.Id, UriKind.Absolute, out var id)
                ? GetHttpUrl(id, null, feedUri) : string.Empty;
        }

        private static string GetHttpUrl(Uri? uri, Uri? baseUri, Uri feedUri)
        {
            if (uri is null) return string.Empty;
            var resolvedBase = baseUri is null ? feedUri
                : baseUri.IsAbsoluteUri ? baseUri : new Uri(feedUri, baseUri);
            var resolved = uri.IsAbsoluteUri ? uri : new Uri(resolvedBase, uri);
            return resolved.Scheme is "http" or "https" ? resolved.AbsoluteUri : string.Empty;
        }

        private static string GetPlainText(TextSyndicationContent? content, bool stripPlainText = false)
        {
            if (content is null) return string.Empty;
            return content.Type == "text" && !stripPlainText
                ? content.Text : WebUtility.HtmlDecode(StripTagsRegex(content.Text));
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
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                    throw new InvalidOperationException("URI is invalid.");

                byte[] fileBytes = await httpClient.GetByteArrayAsync(uri);

                // Resize image, then save it to disk
                await File.WriteAllBytesAsync("./images/" + outputPath, _imageService.ResizeImage(fileBytes));

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
                    using var imgStream = new MemoryStream(await httpClient.GetByteArrayAsync(img));
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
