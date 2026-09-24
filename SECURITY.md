# Security

## Reporting a vulnerability

Use GitHub's private vulnerability reporting: **Security → Report a vulnerability** on
[this repository](https://github.com/MagicDoorInc/MagicCSharp/security/advisories/new). That opens a
private thread visible only to the maintainers.

Please do not open a public issue for a vulnerability.

What to expect: an acknowledgement within a week, and an honest answer about whether and when it will be
fixed. MagicDoor's engineers maintain the project, but a same-day patch is not something to rely on. If a fix
will take a while, you will be told that rather than left waiting.

## What is in scope

The published packages and the `mcs` CLI. Most relevant:

- **`mcs` writes files into your repository.** A path in `magiccsharp.json`, a library name, or a template
  override that escapes the repository root would be a real finding.
- **Templates are executed as Scriban and their output is compiled.** A template override is code you are
  choosing to run, but a built-in template producing something surprising is a finding.
- **`MagicCSharp.AspNetCore` decides what an error response says.** Anything that leaks an internal detail
  to a caller outside Development belongs here.
- **The data packages build SQL from filters.** A filter value reaching a query unparameterised is a
  finding.

Out of scope: vulnerabilities in dependencies with no MagicCSharp-specific exploit path — report those
upstream, though telling us is welcome so the reference can be bumped. Dependency advisories fail this
build (`NU1901`–`NU1904` are errors), so they are usually caught here first.

## Supported versions

The most recent `1.x` release is supported. A security fix ships as a new patch or minor version on `1.x`; there
are no backports to older releases.
