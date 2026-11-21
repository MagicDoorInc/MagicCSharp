using MagicCSharp.UseCases;

namespace PackageSmoke.Library;

/// <summary>Its interface and its <c>Executing:</c> line are both written by the generators.</summary>
[GenerateInterface]
[LogExecuting]
public partial class RenameWidgetUseCase
{
    /// <summary>Renames a widget and returns the new name.</summary>
    public Task<string> Execute(long widgetId, string name)
    {
        return Task.FromResult(name + " (" + widgetId + ")");
    }
}
