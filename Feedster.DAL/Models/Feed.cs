using System.ComponentModel.DataAnnotations;

namespace Feedster.DAL.Models;

public class Feed
{
    public int FeedId { get; set; }
    
    [Required]
    [StringLength(64, ErrorMessage = "Name is too long(0-64).")]
    public string Name { get; set; }
    public List<Group> Groups { get; set; } = new();
    [Required]
    [StringLength(128, ErrorMessage = "URL is too long(0-128)."), CustomValidation()]
    public string RssUrl { get; set; }
    public List<Article> Articles { get; set; } = new();
}