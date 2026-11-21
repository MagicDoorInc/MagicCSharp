using Microsoft.AspNetCore.Mvc;

namespace MagicCSharp.Testing.Clients.Tests.Controllers;

[ApiController]
[Route("api/status")]
public class StatusController : ControllerBase
{
    [HttpGet("~/health")]
    public IActionResult GetHealth()
    {
        return Ok("healthy");
    }
}
