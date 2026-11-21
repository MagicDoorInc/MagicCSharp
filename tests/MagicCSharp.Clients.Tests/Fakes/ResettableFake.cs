namespace MagicCSharp.Clients.Tests.Fakes;

/// <summary>A base class a codebase gives its fakes, which the fake base keeps.</summary>
public abstract class ResettableFake
{
    public int ResetCount { get; private set; }

    public void Reset()
    {
        ResetCount++;
    }
}
