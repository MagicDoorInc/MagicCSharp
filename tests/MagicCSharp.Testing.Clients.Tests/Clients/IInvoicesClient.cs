using MagicCSharp.Clients;

namespace MagicCSharp.Testing.Clients.Tests.Clients;

/// <summary>Served by <c>InvoicesController</c>, whose route starts with the [area] token.</summary>
[MagicClient("billing/invoices")]
public interface IInvoicesClient
{
    [ClientGet("{invoiceId}")]
    Task<string?> GetInvoice(long invoiceId);
}
