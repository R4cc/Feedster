namespace Feedster.DAL.Models;

public class Article
{
    public int ArticleId { get; set; }
    public string? Guid { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int FeedId { get; set; }
    public Feed? Feed { get; set; }
    public string ArticleLink { get; set; } = String.Empty;
    public string? ImageUrl { get; set; }
    public string? ImagePath { get; set; }
    public DateTime PublicationDate { get; set; } = DateTime.Now;
    public string[]? Tags { get; set; }
}