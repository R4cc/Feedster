using System.ComponentModel.DataAnnotations;

namespace Feedster.DAL.Models;

public class Folder
{
    public int FolderId { get; set; }

    [Required]
    [StringLength(64, ErrorMessage = "Name is too long(0-64).")]
    public string Name { get; set; } = string.Empty;

    public List<Feed> Feeds { get; set; } = new();
}