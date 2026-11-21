# MagicCSharp.Generators

Source generators for a MagicCSharp codebase. The generated code is written on every build and shown by the IDE;
it is never committed.

```bash
dotnet add package MagicCSharp.Generators
```

## `[GenerateInterface]`

Mark a use case class `partial` and `[GenerateInterface]`, and leave out its interface:

```csharp
[GenerateInterface]
public partial class PayChargeUseCase(IChargesRepository chargesRepository)
{
    /// <summary>Marks the charge paid.</summary>
    public async Task<Charge> Execute(long chargeId)
    {
        // ...
    }
}
```

The compiler writes `IPayChargeUseCase : IMagicUseCase` with every public `Execute` method of the class and its
doc comment, and makes the class implement it. A class that already implements its own interface is left alone.

To see the generated files on disk, set `EmitCompilerGeneratedFiles` to `true`; they are written under `obj/`.

A codebase with its own use case marker interface names it once, in `Directory.Build.props`:

```xml
<PropertyGroup>
  <MagicCSharpUseCaseMarkerInterface>Acme.Infrastructure.IMagicUseCase</MagicCSharpUseCaseMarkerInterface>
</PropertyGroup>
```

## Rules

| Rule | Reported when |
|---|---|
| MCS1001 | A `[GenerateInterface]` class is not `partial` |
| MCS1002 | It has no public instance `Execute` method |
| MCS1003 | It is nested in another type |
| MCS1004 | It is generic |
| MCS1005 | It is static |
| MCS1006 | One of its `Execute` methods is generic |
| MCS1007 | The use case marker interface is not an interface the project can see |

## Building on the declarations

`MagicCSharp.Generators.Declarations.dll`, in the same analyzer folder, holds the declaration model, its readers and
the source writers. A generator in another repository compiles against it from this package and is loaded next to it.
