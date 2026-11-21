using Microsoft.AspNetCore.Mvc;

namespace MagicCSharp.Testing.Clients.Tests.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    [HttpGet("{reportId?}")]
    public IActionResult GetReport(long? reportId)
    {
        return Ok(reportId);
    }
}
