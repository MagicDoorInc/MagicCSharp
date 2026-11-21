namespace MagicCSharp.Clients.Tests.Fakes;

/// <summary>Extends another interface, whose members a fake base stubs too.</summary>
public interface ILeaseNotes : ILeaseLookup
{
    event EventHandler? NoteAdded;

    Task AddNote(long leaseId, string note);
}
