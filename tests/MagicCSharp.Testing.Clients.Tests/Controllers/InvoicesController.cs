using Microsoft.AspNetCore.Mvc;

namespace MagicCSharp.Testing.Clients.Tests.Controllers;

[ApiController]
[Area("billing")]
[Route("[area]/invoices")]
public class InvoicesController : ControllerBase
{
    [HttpGet("{invoiceId}")]
    public IActionResult GetInvoice(long invoiceId)
    {
        return Ok(invoiceId);
    }
}
