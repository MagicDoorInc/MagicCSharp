# AIAgents

The conventions of a MagicCSharp repository, written for AI coding agents — and useful to any person new to
one. This folder is their source:

- `AGENTS.md` — where an agent starts; the name most coding agents look for.
- `CLAUDE.md` — imports `AGENTS.md` (`@AGENTS.md`), so Claude Code reads the same text.
- `.ai-knowledge/` — one guide per topic, indexed by `INDEX.md`, and `project.md`, the stub a repository fills
  in with what is specific to it.

`mcs` embeds these files at build time. `mcs init` writes them into every new repository (unless
`--no-ai-knowledge`), and `mcs update ai-files` refreshes them in an existing one — overwriting the guides it
ships, never touching `project.md` or a file a repository added itself. `{{ prefix }}` is replaced with the
repository's prefix; nothing else is templated.

To change a convention, edit it here. The next `mcs` release carries it, and every repository picks it up with
`mcs update ai-files`. Examples in the guides come from
[`examples/PropertyManagement`](../examples/PropertyManagement), whose own copies are generated from these —
refresh them with `mcs update ai-files` after an edit, so the example stays the proof that the guides describe
real code.
