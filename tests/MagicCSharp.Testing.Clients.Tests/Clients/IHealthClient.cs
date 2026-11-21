using MagicCSharp.Clients;

namespace MagicCSharp.Testing.Clients.Tests.Clients;

/// <summary>Served by an action whose ~/ route overrides its controller's route.</summary>
[MagicClient("health")]
public interface IHealthClient
{
    [ClientGet]
    Task<string> GetHealth();
}
