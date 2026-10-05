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
    public partial class RssFetchService
    {
        private readonly ArticleRepository _articleRepository;
        private readonly UserRepository _userRepo;
        private readonly ImageService _imageService;

        public RssFetchService(ArticleRepository articleRepository, UserRepository userRepo, ImageService imageSerivce)
        {
            _articleRepository = articleRepository;
            _userRepo = userRepo;
            _imageService = imageSerivce;
        }

        public async Task RefreshFeeds(List<Feed> feeds, CancellationToken cancellationToken = default)
        {
            var settings = await _userRepo.GetSnapshot(cancellationToken);
            foreach (var feed in feeds)
            {
                var articles = await FetchFeedArticles(feed, settings, cancellationToken);
                if (articles is not null)
                {
                    await _articleRepository.SaveFetchedArticles(articles, cancellationToken);
                }
            }
        }

        public async Task<int?> RefreshFeed(Feed feed, CancellationToken cancellationToken = default)
        {
            var settings = await _userRepo.GetSnapshot(cancellationToken);
            var articles = await FetchFeedArticles(feed, settings, cancellationToken);
            if (articles is null)
            {
                return null;
            }

            await _articleRepository.SaveFetchedArticles(articles, cancellationToken);
            return articles.Count;
        }

        private static async Task<(bool Success, SyndicationFeed Feed, bool IsAtom)> ReadXml(string rssUrl, CancellationToken cancellationToken)
        {
            try
            {
                var (result, isAtom) = await FeedReader.ReadAsync(rssUrl, cancellationToken);
                return (true, result, isAtom);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch
            {
                // ignored
            }

            return (false, new SyndicationFeed(), false);
        }

        private async Task<List<Article>?> FetchFeedArticles(Feed feed, UserSettings settings, CancellationToken cancellationToken)
        {
            List<Article> articlesToUpdate = new();
            var (success, result, isAtom) = await ReadXml(feed.RssUrl, cancellationToken);
            if (!success)
            {
                return null;
            }
            var feedUri = new Uri(feed.RssUrl);

            // Read current DB state instead of stale navigation lists carried by callers.
            var savedArticles = await _articleRepository.GetForFeed(feed.FeedId, cancellationToken);
            var existingArticles = new Dictionary<string, Article>(savedArticles.Count, StringComparer.Ordinal);
            var existingIds = new Dictionary<string, Article>(isAtom ? savedArticles.Count : 0, StringComparer.Ordinal);
            foreach (var saved in savedArticles)
            {
                if (!string.IsNullOrEmpty(saved.ArticleLink)) existingArticles.TryAdd(saved.ArticleLink, saved);
                if (isAtom && !string.IsNullOrEmpty(saved.Guid)) existingIds.TryAdd(saved.Guid, saved);
            }
            var now = DateTime.Now;
            var expiration = settings.ArticleExpirationAfterDays == 0
                ? DateTime.MinValue : now.AddDays(-settings.ArticleExpirationAfterDays);

            // loop through all results and add them to a list
            foreach (var itm in result.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var publicationDate = itm.PublishDate != DateTimeOffset.MinValue
                        ? itm.PublishDate.LocalDateTime
                        : itm.LastUpdatedTime != DateTimeOffset.MinValue
                            ? itm.LastUpdatedTime.LocalDateTime
                            : now;

                    // Atom's published date is optional; use updated for expiry too.
                    if (publicationDate < expiration)
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
                        if (settings.DownloadImages && !File.Exists("images/" + article.ImagePath))
                        {
                            article.ImagePath = await DownloadFileAsync(article.ImageUrl, article.ImagePath, null, cancellationToken);

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
                    if (settings.DownloadImages && imageUrls.Count > 0)
                    {
                        var selected = await GetHighestResolutionImage(imageUrls, cancellationToken);
                        highestResImageUrl = selected.Url;
                        highestResImagePath = await DownloadFileAsync(highestResImageUrl,
                            Path.GetFileName(highestResImageUrl), selected.Bytes, cancellationToken);
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
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
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

            return imageUrls.Distinct(StringComparer.Ordinal).ToList();
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

        private async Task<string> DownloadFileAsync(string uri, string outputPath, byte[]? fileBytes, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(uri))
                {
                    return String.Empty;
                }

                // Remove special chars
                var normalizedFilename = FilenameRegex().Replace(Path.GetFileNameWithoutExtension(outputPath), "");

                // Avoid cache collisions between images with the same filename.
                normalizedFilename += "-" + Guid.NewGuid().ToString("N");
                outputPath = normalizedFilename + ".webp";

                if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
                    throw new InvalidOperationException("URI is invalid.");

                fileBytes ??= await GetImageBytes(uri, cancellationToken);

                // Resize image, then save it to disk
                await File.WriteAllBytesAsync("./images/" + outputPath, _imageService.ResizeImage(fileBytes), cancellationToken);

                return outputPath;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch
            {
                // most likely 403 No access so ignore
            }

            return String.Empty;
        }

        private static async Task<(string Url, byte[]? Bytes)> GetHighestResolutionImage(List<string> images, CancellationToken cancellationToken)
        {
            if (images.Count == 1)
            {
                return (images[0], null);
            }

            string highestResolutionImage = String.Empty;
            ulong highestResolution = 0;
            byte[]? selectedBytes = null;

            foreach (var img in images)
            {
                try
                {
                    // download image and pass it to the drawer
                    var bytes = await GetImageBytes(img, cancellationToken);
                    using var imgStream = new MemoryStream(bytes);
                    // Read dimensions without decoding every candidate's pixels.
                    var image = new MagickImageInfo(imgStream);
                    
                    var resolution = (ulong)image.Height * image.Width;
                    if (resolution <= highestResolution) continue;
                            
                    highestResolution = resolution;
                    highestResolutionImage = img;
                    selectedBytes = bytes;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch
                {
                    // img download probably failed; continue
                }
            }
            return (highestResolutionImage, selectedBytes);
        }

        private static async Task<byte[]> GetImageBytes(string url, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            using var response = await FeedReader.Client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        private static string StripTagsRegex(string source)
        {
            return HtmlTagsRegex().Replace(source, string.Empty);
        }

        [GeneratedRegex("<.*?>")]
        private static partial Regex HtmlTagsRegex();

        [GeneratedRegex("(?:[^a-z0-9 ]|(?<=['\"])s)")]
        private static partial Regex FilenameRegex();
    }
}
