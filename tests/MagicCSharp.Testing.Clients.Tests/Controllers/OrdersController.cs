using Microsoft.AspNetCore.Mvc;

namespace MagicCSharp.Testing.Clients.Tests.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpGet("{id:long}")]
    public IActionResult GetOrder(long id)
    {
        return Ok(id);
    }

    [HttpPost]
    public IActionResult CreateOrder([FromBody] string order)
    {
        return Ok(order);
    }

    [HttpPut("{id:long}/lines/{lineNumber}")]
    public IActionResult UpdateLine(long id, long lineNumber)
    {
        return Ok(lineNumber);
    }
}
