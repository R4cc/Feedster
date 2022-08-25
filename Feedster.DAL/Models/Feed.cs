namespace Feedster.DAL.Models;

public class Feed
{
    public int FeedId { get; set; }
    public string Name { get; set; }
    public List<Tag> Tags { get; set; } = new();
    public string RssUrl { get; set; }
    public List<Article> Articles { get; set; }
}