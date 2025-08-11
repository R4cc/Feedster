using System.Text;
using Feedster.DAL.Services;
using Microsoft.AspNetCore.Mvc;

namespace Feedster.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpmlController : ControllerBase
{
    private readonly OpmlService _opmlService;

    public OpmlController(OpmlService opmlService)
    {
        _opmlService = opmlService;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var content = await _opmlService.ExportAsync();
        var bytes = Encoding.UTF8.GetBytes(content);
        return File(bytes, "text/xml", "feeds.opml");
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is empty");
        }

        using var stream = file.OpenReadStream();
        await _opmlService.ImportAsync(stream);

        return Ok();
    }
}
