namespace MagicCSharp.Cli.Tests;

/// <summary>A throwaway directory that cleans itself up.</summary>
public sealed class TemporaryRepository : IDisposable
{
    public TemporaryRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), "mcs-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string OverrideDirectory => Path.Combine(Root, ".magiccsharp", "templates");

    public void WriteOverride(string name, string content)
    {
        var path = Path.Combine(OverrideDirectory, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }
}
