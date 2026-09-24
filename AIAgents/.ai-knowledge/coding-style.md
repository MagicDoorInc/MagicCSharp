# Coding Style

Most of this is enforced: `MagicCSharp.Analyzers` makes the rules below build errors, in tests too. A rule
that fires is a convention broken — fix the code, do not suppress the rule. EF migrations are generated code
and are not analysed.

## Enforced rules

| Rule | Enforces |
|---|---|
| MCS0001 | Records declare members in a body, never a positional parameter list |
| MCS0002 | At most four parameters per method; group the rest into a request record (controller actions exempt) |
| MCS0003–MCS0005 | `== null` / `!= null`; no property patterns (`is { }`); no patterns that declare a variable |
| MCS0006 | Braces on every `if`, `else`, loop and `using` body — including a one-line `return` |
| MCS0007 / MCS0020 | A dependency is named after its type: `createLease`, `chargesRepository`, `timeProvider`, never `useCase` |
| MCS0008 | No `DateTime.Now`, `UtcNow` or `Today`, nor `DateTimeOffset.Now` / `UtcNow`, outside a `TimeProvider` implementation |
| MCS0009 | Methods and local functions have block bodies (`=>` is fine on properties) |
| MCS0010 | No cryptic abbreviations: `repo`, `ctx`, `obj`, `svc` |
| MCS0011 | Booleans start with `is`, `has`, `can`, `should`, `allow`, `was`, `will`, `must` or `are` |
| MCS0012 | A type's suffix matches what it implements: `…UseCase`, `…Repository` |
| MCS0013 / MCS0014 | Extra public types in a file support its main type; the file is named after a type it declares |
| MCS0015 | Read-only use cases (Get/List/Search) do not log the `Executing:` line |
| MCS0017 | No mutable static state |
| MCS0018 | File-scoped namespaces |
| MCS0019 | A variable made with `new` or by `foreach` ends with its type's name: `lateFeePolicy`, `rentCharge`, `chargeDal` — not `policy`, `fee`, `dal` |
| MCS0021 | `DbSet` properties are plural |
| MCS0022 | Request, Result, Payload and Event types are records |

To change how one rule applies — rarely — set its severity in `.editorconfig`
(`dotnet_diagnostic.MCS0019.severity = warning`) and say why in a comment beside it.

## Not enforced, still the style

- `var` when the type is obvious; never target-typed `new()`.
- Trailing commas in multiline initializers, never in argument lists.
- Primary constructors for dependencies, used directly — no fields copying them.
- Enum members without explicit values.
- Switch expressions only when every arm maps one input to one value; otherwise a switch statement.
- Built-in guards (`ArgumentOutOfRangeException.ThrowIfNegative`, `ArgumentNullException.ThrowIfNull`), not a
  guard library.
- No dead code, no commented-out code, no unused usings or references. Git has the history.
- Comments explain **why** — a constraint, a crash window, a surprising trade-off — never what the next line
  does.
- English everywhere in code and docs.
