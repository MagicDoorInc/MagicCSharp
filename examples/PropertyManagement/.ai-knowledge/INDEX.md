# `.ai-knowledge/` — Index

The conventions for this repository. Each guide owns one topic; when two seem to overlap, the one named for
the topic wins. Read the guides the task below needs, not all of them.

| You are about to… | Read |
|---|---|
| Place new code, add a domain or subdomain, decide what may call what | `architecture-and-project-structure.md` |
| Create a project, domain, entity or migration | `project-tooling.md` |
| Write or change a use case | `use-case-patterns.md` |
| Add or change an entity, DAL, repository or filter | `entities-and-database.md` |
| Publish an event or write a handler | `events-and-messaging.md` |
| Add or change an endpoint or DTO | `api-and-controller-conventions.md` |
| Add work that runs on a schedule | `background-services.md` |
| Read the current time, or handle dates and time zones | `time-and-dates.md` |
| Write tests | `testing-conventions.md` |
| Choose names, formatting, `var`, braces, records | `coding-style.md` |

## Finishing

Work is finished when `dotnet build Acme.All.slnx`, `dotnet test Acme.All.slnx` and `mcs validate --path .`
pass. Say which of them you ran; do not claim a check you could not run.
