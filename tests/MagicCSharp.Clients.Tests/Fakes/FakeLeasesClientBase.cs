using MagicCSharp.Clients.Tests.Leases;
using MagicCSharp.Testing;

namespace MagicCSharp.Clients.Tests.Fakes;

[GenerateFakeBase(typeof(ILeasesClient))]
public abstract partial class FakeLeasesClientBase : ResettableFake;
