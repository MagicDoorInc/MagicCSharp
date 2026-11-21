namespace MagicCSharp.Clients.Tests.Leases;

public record DeleteNotesRequest
{
    public required IReadOnlyList<long> NoteIds { get; init; }
}
