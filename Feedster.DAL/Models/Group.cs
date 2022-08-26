namespace Feedster.DAL.Models;

public class Group
{
    public int GroupId { get; set; }
    public string Name { get; set; }
    public List<Feed> Feeds { get; set; } = new();
}