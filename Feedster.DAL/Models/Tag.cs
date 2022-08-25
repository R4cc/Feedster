namespace Feedster.DAL.Models;

public class Tag
{
    public int TagId { get; set; }
    public string Name { get; set; }
    public List<Feed> Feeds { get; set; } = new();
}