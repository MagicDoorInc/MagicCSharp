using MagicCSharp.Testing;

namespace MagicCSharp.Clients.Tests.Fakes;

[GenerateFakeBase(typeof(ILeaseNotes))]
public abstract partial class FakeLeaseNotesBase : LeaseLookupDefaults;
