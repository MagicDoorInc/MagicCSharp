# Overriding the templates

Every file `mcs` generates comes from a Scriban template — nineteen of them. You can replace any one with
your own, and the rest keep coming from the tool.

```bash
mcs templates list      # what exists, and where each comes from
mcs templates where     # the two layers, in lookup order
mcs templates eject Entities/dal.cs.hbs
```

## How lookup works

Two layers, first match winning:

1. Your override directory — `.magiccsharp/templates/` unless `magiccsharp.json` says otherwise
2. The built-ins, embedded in `mcs` itself

That is the whole mechanism: a file-exists check per template name.

Because it is **per file**, overriding `Entities/dal.cs.hbs` leaves the other eighteen built-in and still
receiving upstream improvements. Reverting is deleting your copy — there is no registry, no cache, and
nothing else to unset.

---

## One repository, one-off change

For a change that belongs to a single repository:

```bash
mcs templates eject Entities/dal.cs.hbs
$EDITOR .magiccsharp/templates/Entities/dal.cs.hbs
git add .magiccsharp && git commit -m "Our DAL template records who created the row"
```

Committed, so everyone on that repository gets it. Nothing else to install.

---

## Several repositories: a shared template repository

**This is what to do once you have more than one repository.** Copying overrides between them means house
style drifts, and a fix to one never reaches the others. Put the templates in a repository of their own and
consume it as a submodule.

### 1. Create the template repository

It is a plain git repository whose layout mirrors the template names. Start from the built-ins rather than
from scratch, so you inherit whatever the tool already does well:

```bash
mkdir acme-magiccsharp-templates && cd acme-magiccsharp-templates
git init

# eject the ones you want to own, from any MagicCSharp repository
mcs templates eject Entities/dal.cs.hbs
mcs templates eject Apps/Program.cs.hbs

# move them here, keeping the directory structure
mv .magiccsharp/templates/* .
rmdir -p .magiccsharp/templates 2>/dev/null

git add -A
git commit -m "Acme house style for MagicCSharp templates"
git remote add origin git@github.com:acme/acme-magiccsharp-templates.git
git push -u origin main
```

The result holds only what you override:

```
acme-magiccsharp-templates/
  Entities/dal.cs.hbs
  Apps/Program.cs.hbs
  README.md            ← say why each one differs from the built-in
```

Only include templates you have actually changed. A copy identical to the built-in is a file that will
silently go stale.

### 2. Add it to a repository

```bash
cd my-service-repo
git submodule add git@github.com:acme/acme-magiccsharp-templates.git .magiccsharp/templates
git commit -m "Use Acme's shared MagicCSharp templates"
```

`.magiccsharp/templates` is already the default override path, so nothing else is needed. Confirm:

```bash
mcs templates list --overridden
```

```
Entities/dal.cs.hbs   this repository (.magiccsharp/templates/Entities/dal.cs.hbs)
Apps/Program.cs.hbs   this repository (.magiccsharp/templates/Apps/Program.cs.hbs)
19 templates, 2 overridden
```

Everything generated from then on uses your templates for those two and the built-ins for the rest.

### 3. Teammates and CI

A submodule is not cloned by default:

```bash
git clone --recurse-submodules git@github.com:acme/my-service-repo.git

# or, in an existing clone
git submodule update --init
```

Worth putting in the repository's README, because a missing submodule fails quietly — the override directory
is simply empty and `mcs` falls back to the built-ins. `mcs templates list --overridden` showing nothing when
you expect two is the symptom.

CI needs it too:

```yaml
- uses: actions/checkout@v4
  with:
    submodules: true
```

### 4. Changing a shared template

```bash
cd .magiccsharp/templates
git pull origin main
$EDITOR Entities/dal.cs.hbs
git commit -am "Add a soft-delete column to the DAL template"
git push

cd ../..
git commit -am "Bump shared templates"    # the submodule pointer
```

The pointer is the useful part: each repository moves to the new templates when it chooses, so a change to
house style does not silently alter what every repository generates tomorrow.

### If you would rather not use submodules

`templates` takes any path:

```json
{ "prefix": "Acme", "templates": "../acme-magiccsharp-templates" }
```

Fine for a fixed layout on developer machines; it will not work in CI unless that path exists there too.
The submodule is the version that survives a fresh clone.

---

## Configuration

```json
{
  "prefix": "Acme",
  "templates": ".magiccsharp/templates"
}
```

| Value | |
|---|---|
| absent | `.magiccsharp/templates` |
| a path | that directory, relative to the repository root or absolute |
| `""` | overrides off — `mcs templates eject` refuses rather than writing somewhere ignored |

---

## Writing a template

Templates are [Scriban](https://github.com/scriban/scriban). The built-ins are the best reference — eject one
and read it.

What is available depends on which command renders the template:

| Template family | Variables |
|---|---|
| `Repo/` — `mcs init` | `version`, `target_framework` |
| `Apps/` — `mcs create-app` | `prefix`, `name`, `port`, `database.enabled`, `database.name` |
| `Libraries/` — `create-domain`, `create-lib` | `prefix`, `name`, `assembly_name` |
| `Entities/` — `mcs add-entity` | `prefix`, `app_name`, `entity_name`, `plural`, `table_name`, `entity_namespace`, `use_key`, `is_paginated` |

For an entity named `Order` in service `Shop`: `entity_name` is `Order`, `plural` is `Orders`, `table_name`
is `orders`, and `entity_namespace` is `Acme.Shop.Domains.Orders.Models.Entities`. `use_key` and
`is_paginated` are the `--use-key` and `--paginated` flags.

```handlebars
[Table("{{ table_name }}")]
public class {{ entity_name }}Dal : Base{{ if use_key }}Key{{ else }}Id{{ end }}Dal<{{ entity_name }}, {{ entity_name }}Edit>
```

A template that fails to parse is reported with the template name and the parse error, rather than producing
a broken file.

---

## What to override, and what it costs

Good candidates are house style the framework has no opinion about: a licence header on generated files,
audit columns every table carries, a different default filter shape, a test file that starts from your
fixtures.

The cost is the usual one for a fork. Your copy stops tracking upstream, so a fix to the built-in does not
reach it. `mcs templates list` shows which files you have taken on, and `mcs templates eject --force` writes
the current built-in over yours so you can diff and merge.

If what you want changed would help everyone, send a pull request instead of overriding — then you get the
improvement without owning the file.
