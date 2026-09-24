# CI/CD: build, test and deploy only the apps a change touched

A MagicCSharp repository holds several apps (each a deployable service) and the shared libraries under `Libs/`.
Rebuilding and redeploying every app on every merge is slow, and it redeploys services nothing changed in. This
guide sets up GitHub Actions so that:

- a pull request builds and tests only the apps it can affect;
- a merge to `master` builds one image per affected app, runs its database migrations, and deploys it to
  staging and then, after an approval, to production;
- the image that reaches production is the one that was tested and ran in staging, not a rebuild.

It is the pipeline MagicDoor runs for its own services, simplified to the parts every repository needs. The
workflows below assume the layout `mcs` creates; adjust the names to your own.

## How `mcs` decides what changed

Two commands do the work. Both read the `.csproj` files directly, so they take milliseconds and need no restore
or build first.

**`mcs references`** prints every project a project depends on, directly or through another project, one
folder per line:

```bash
$ mcs references --project Apps/Leasing/Leasing.App/Acme.Leasing.App.csproj
Apps/Leasing/Data/Data.EntityFramework
Apps/Leasing/Data/Data.Models
Apps/Leasing/Leasing.App
Apps/Leasing/Leasing.Domains/Leases/Default
...
Libs/Events/Default
Libs/Web/Default
```

`--files` prints the `.csproj` paths instead. A reference to a project that does not exist is an error, not a
silent gap: a graph with a hole in it would miss apps that need deploying.

**`mcs affected`** asks git which files changed between two commits and prints the apps they touch:

```bash
$ mcs affected --base origin/master
Leasing
$ mcs affected --base origin/master --json
["Leasing"]
```

An app counts as affected when a changed file is:

| Changed file | Affected apps | Example |
|---|---|---|
| inside the app's own folder, `Apps/{App}/` | that app | `Apps/Leasing/Leasing.Domains/Leases/Default/SignLeaseUseCase.cs` → Leasing |
| inside a project the app depends on | every app that depends on it | `Libs/Events/Default/LeaseSignedEvent.cs` → every app that publishes or handles events |
| a file every build reads | every app | `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig`, `.dockerignore`, `magiccsharp.json` |
| anywhere else | none | `README.md`, `docs/`, a library no app references |

"Depends on" has two meanings, and the pipeline uses both:

- **By default** it means what the app's host project (`Apps/{App}/{App}.App/`) reaches through its project
  references: the code that ends up in the image. This answers *what to deploy*.
- **With `--solution`** it also counts every project in the app's solution, `{Prefix}.{App}.slnx`, tests
  included. A change to a test-only library (a shared test base under `Libs/`, say) re-runs the tests of the
  apps that use it, but deploys nothing. This answers *what to test*.

`--all` prints every app regardless of what changed; use it for a manual full deploy. `--head` compares a
commit other than `HEAD`.

Paths are compared relative to the directory `mcs` runs in, so a MagicCSharp repository kept in a subfolder of a
bigger git repository works too; changes outside that folder are ignored.

### Why this is safe to trust

The graph is the same one the compiler uses: if an app does not reference a project, the app's build cannot
see its code, so a change there cannot change the app's binary. Two things the graph cannot see, and how the
pipeline covers them:

- **Events between apps.** Apps talk through event contracts in a shared library under `Libs/`. Changing a
  contract changes that library, so every app that references it is rebuilt and redeployed — publisher and
  handlers together.
- **Files outside any project** that a build still reads, such as a `Directory.Build.props` in a subfolder.
  The top-level ones are on the "every app" list above. If you add build files elsewhere, keep them inside the
  folder of the projects they apply to, so a change to them lands inside a project folder.

## One-time setup

### 1. Pin the tool versions in the repository

A tool manifest makes CI use the same `mcs` everyone else does, and brings `dotnet-ef` along for the migration
bundles:

```bash
dotnet new tool-manifest
dotnet tool install MagicCSharp.Cli
dotnet tool install dotnet-ef
```

Commit `.config/dotnet-tools.json`. CI then runs `dotnet tool restore` and calls `dotnet mcs` and
`dotnet ef`.

### 2. Give each app a Dockerfile

`mcs create-app` does not write one. Put this at `Apps/{App}/Dockerfile`, replacing `Leasing` and `Acme`
with your app and prefix:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# The whole repository: an app builds against the shared libraries under Libs/ and the
# repository-wide Directory.*.props files.
COPY . .
RUN dotnet publish "Apps/Leasing/Leasing.App/Acme.Leasing.App.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Acme.Leasing.App.dll"]
```

Add a `.dockerignore` at the root with at least `**/bin/`, `**/obj/` and `.git/`, so a local build does not send
build output to Docker. It decides what every image copies, so changing it counts as affecting every app.

### 3. Create the GitHub environments

In the repository's **Settings → Environments**, create `staging` and `production`. Give `production` a
required reviewer: that is the approval step before anything reaches production. Then add these to **each**
environment, with that environment's values. The names are the same in both, so one workflow serves both:

| Kind | Name | What it is |
|---|---|---|
| Secret | `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASSWORD` | The PostgreSQL server that environment's migrations run against. |
| Secret | `KUBE_CONFIG` | Credentials for the cluster (or whatever your deploy step needs). |
| Variable | `K8S_NAMESPACE` | Where the app runs in that environment. |
| Variable | `ASPNETCORE_ENVIRONMENT` | `Staging` or `Production`. |

At the repository level (**Settings → Secrets and variables → Actions**), add the container registry:

| Kind | Name | Example |
|---|---|---|
| Variable | `REGISTRY_HOST` | `ghcr.io` |
| Variable | `REGISTRY_NAMESPACE` | `acme` |
| Variable | `REGISTRY_USERNAME` | a service account |
| Secret | `REGISTRY_PASSWORD` | its token |

There is no `DB_NAME` among them, because every app has its own database and these secrets are shared by all
apps in the environment. The migration bundle needs none: it uses the name the app was created with (`mcs
create-app --database leasing`), which is the default in the app's generated context factory. The running app
does need it — it will not start without `DB_NAME` — so set it, with the other `DB_*` values, in the app's own
runtime configuration: the `env` of its Kubernetes Deployment, for example.

### 4. Protect `master`

In **Settings → Branches**, require pull requests for `master` and require the `CI result` check from the
pull request workflow below. That one check stands for every app the pull request touched, so the rule never
has to change as you add apps.

## Pull requests: `.github/workflows/ci.yml`

A `plan` job works out the affected apps; a matrix job then builds and tests each one in parallel; a final job
turns the matrix into one required check.

```yaml
name: CI

on:
  pull_request:
    branches: [master]

permissions:
  contents: read

concurrency:
  group: ci-${{ github.event.pull_request.number }}
  cancel-in-progress: true

jobs:
  plan:
    runs-on: ubuntu-latest
    outputs:
      apps: ${{ steps.affected.outputs.apps }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet tool restore

      # The checkout holds only the pull request's commit; fetch the base so git can compare the two.
      - name: Fetch the base commit
        run: git fetch --no-tags --depth=1 origin ${{ github.event.pull_request.base.sha }}

      - name: Find the affected apps
        id: affected
        run: echo "apps=$(dotnet mcs affected --base ${{ github.event.pull_request.base.sha }} --solution --json)" >> "$GITHUB_OUTPUT"

      - name: Check the conventions
        run: dotnet mcs validate --path .

  test:
    needs: plan
    if: needs.plan.outputs.apps != '[]'
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        app: ${{ fromJSON(needs.plan.outputs.apps) }}
    name: Test (${{ matrix.app }})
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build Acme.${{ matrix.app }}.slnx --configuration Release
      # The repository tests start their own PostgreSQL through Testcontainers; the runner has Docker.
      - run: dotnet test Acme.${{ matrix.app }}.slnx --configuration Release --no-build

  result:
    needs: [plan, test]
    if: always()
    runs-on: ubuntu-latest
    name: CI result
    steps:
      - name: Fail if planning or any app failed
        run: |
          [ "${{ needs.plan.result }}" = "success" ] || exit 1
          [ "${{ needs.test.result }}" = "success" ] || [ "${{ needs.test.result }}" = "skipped" ] || exit 1
```

A pull request that only touches documentation plans `[]`, skips the tests, and passes `CI result` in seconds.

Comparing with the base commit directly (`base..head`) rather than with the merge base can over-report: if
`master` moved on after the branch was cut, the files that changed on `master` count too. That errs on the side
of testing more, never less. It also means the fetch above can stay shallow.

## Merges to `master`: `.github/workflows/release.yml`

Each affected app gets its own chain: test, build the image and the migration bundle once, then deploy that
same image to staging and production, running the migrations before each deploy.

```yaml
name: Release

on:
  push:
    branches: [master]
  workflow_dispatch:
    inputs:
      deploy_all:
        description: Deploy every app, not only the affected ones
        type: boolean
        default: false

permissions:
  contents: read

jobs:
  plan:
    runs-on: ubuntu-latest
    outputs:
      apps: ${{ steps.affected.outputs.apps }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet tool restore

      - name: Find the affected apps
        id: affected
        env:
          BEFORE: ${{ github.event.before }}
          DEPLOY_ALL: ${{ inputs.deploy_all }}
        run: |
          set -euo pipefail
          # A manual run, or the first push of the branch (no previous commit to compare), deploys everything.
          if [ "$DEPLOY_ALL" = "true" ] || [ -z "$BEFORE" ] || [[ "$BEFORE" =~ ^0+$ ]]; then
            echo "apps=$(dotnet mcs affected --all --json)" >> "$GITHUB_OUTPUT"
          else
            git fetch --no-tags --depth=1 origin "$BEFORE"
            echo "apps=$(dotnet mcs affected --base "$BEFORE" --json)" >> "$GITHUB_OUTPUT"
          fi

  release:
    needs: plan
    if: needs.plan.outputs.apps != '[]'
    strategy:
      fail-fast: false
      matrix:
        app: ${{ fromJSON(needs.plan.outputs.apps) }}
    name: Release (${{ matrix.app }})
    uses: ./.github/workflows/release-app.yml
    with:
      app: ${{ matrix.app }}
    secrets: inherit
```

The plan here uses the default scope, not `--solution`: a change that only touches tests was tested in its pull
request and has nothing to ship.

`github.event.before` is the commit `master` pointed at before this push, so a push of several commits at once
is compared as a whole. Apps are released independently (`fail-fast: false`), so one app failing its tests does
not hold back the others.

### `.github/workflows/release-app.yml`

```yaml
name: Release app

on:
  workflow_call:
    inputs:
      app:
        required: true
        type: string

permissions:
  contents: read

env:
  IMAGE: ${{ vars.REGISTRY_HOST }}/${{ vars.REGISTRY_NAMESPACE }}/${{ inputs.app }}

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build Acme.${{ inputs.app }}.slnx --configuration Release
      - run: dotnet test Acme.${{ inputs.app }}.slnx --configuration Release --no-build

  image:
    needs: test
    runs-on: ubuntu-latest
    outputs:
      digest: ${{ steps.push.outputs.digest }}
    steps:
      - uses: actions/checkout@v4
      - uses: docker/login-action@v3
        with:
          registry: ${{ vars.REGISTRY_HOST }}
          username: ${{ vars.REGISTRY_USERNAME }}
          password: ${{ secrets.REGISTRY_PASSWORD }}
      - uses: docker/setup-buildx-action@v3
      - id: push
        uses: docker/build-push-action@v6
        with:
          file: Apps/${{ inputs.app }}/Dockerfile
          context: .
          push: true
          tags: ${{ env.IMAGE }}:${{ github.sha }}

  # A self-contained executable that applies the app's EF Core migrations. Built once, like the image, and
  # run against each environment's database before that environment gets the new image.
  migrations:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet tool restore
      - name: Build the migration bundle
        run: |
          set -euo pipefail
          mkdir -p bundle
          project=Apps/${{ inputs.app }}/Data/Data.EntityFramework
          if [ -d "$project/Migrations" ]; then
            dotnet ef migrations bundle --project "$project" --self-contained -r linux-x64 -o bundle/efbundle
          fi
      - uses: actions/upload-artifact@v4
        with:
          name: migrations-${{ inputs.app }}
          path: bundle
          if-no-files-found: ignore

  staging:
    needs: [image, migrations]
    uses: ./.github/workflows/deploy-app.yml
    with:
      app: ${{ inputs.app }}
      environment: staging
      image: ${{ vars.REGISTRY_HOST }}/${{ vars.REGISTRY_NAMESPACE }}/${{ inputs.app }}@${{ needs.image.outputs.digest }}
    secrets: inherit

  production:
    needs: [image, staging]
    uses: ./.github/workflows/deploy-app.yml
    with:
      app: ${{ inputs.app }}
      environment: production
      image: ${{ vars.REGISTRY_HOST }}/${{ vars.REGISTRY_NAMESPACE }}/${{ inputs.app }}@${{ needs.image.outputs.digest }}
    secrets: inherit
```

Both deploys use the image **digest**, not the tag, so production runs the exact bytes staging ran even if the
tag is pushed again.

### `.github/workflows/deploy-app.yml`

The only part that depends on where you run. This version targets Kubernetes, with a Deployment named after
the app whose container is named `app`; swap the last step for your platform.

```yaml
name: Deploy app

on:
  workflow_call:
    inputs:
      app:
        required: true
        type: string
      environment:
        required: true
        type: string
      image:
        required: true
        type: string

permissions:
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-latest
    # The environment supplies the secrets, and production's required reviewer pauses the job here until
    # someone approves.
    environment: ${{ inputs.environment }}
    # Never cancel a deploy halfway through a migration; queue the next one behind it instead.
    concurrency:
      group: deploy-${{ inputs.app }}-${{ inputs.environment }}
      cancel-in-progress: false
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: migrations-${{ inputs.app }}
          path: bundle
        continue-on-error: true   # an app without a database has no bundle

      # Migrations first: if they fail, the old version keeps running against the old schema.
      - name: Apply migrations
        env:
          DB_HOST: ${{ secrets.DB_HOST }}
          DB_PORT: ${{ secrets.DB_PORT }}
          DB_USER: ${{ secrets.DB_USER }}
          DB_PASSWORD: ${{ secrets.DB_PASSWORD }}
        run: |
          if [ -f bundle/efbundle ]; then
            chmod +x bundle/efbundle
            ./bundle/efbundle
          fi

      - name: Deploy
        env:
          KUBE_CONFIG: ${{ secrets.KUBE_CONFIG }}
        run: |
          set -euo pipefail
          echo "$KUBE_CONFIG" > "$RUNNER_TEMP/kubeconfig"
          export KUBECONFIG="$RUNNER_TEMP/kubeconfig"
          deployment=$(echo "${{ inputs.app }}" | tr '[:upper:]' '[:lower:]')
          kubectl -n "${{ vars.K8S_NAMESPACE }}" set image "deployment/$deployment" app="${{ inputs.image }}"
          kubectl -n "${{ vars.K8S_NAMESPACE }}" rollout status "deployment/$deployment" --timeout=10m
```

The migration bundle needs no connection string: it builds the context through the app's design-time factory
(`MagicDbContextFactory`, which `mcs create-app --database` generates), and that factory reads `DB_HOST`,
`DB_PORT`, `DB_NAME`, `DB_USER` and `DB_PASSWORD` from the environment, using the app's own database name when `DB_NAME` is
not set. Set `DB_NAME` here too if an environment names its databases differently from the default.

## Things that go wrong

- **`fatal: ambiguous argument 'abc123..HEAD'`.** The base commit is not in the checkout. Fetch it first, as
  the workflows above do; `actions/checkout` fetches only the one commit it checks out.
- **An app is missing from the plan.** Run `mcs references --project <its host project>` and check the library
  you changed is in the list. If it is not, the app does not reference it, and its build could not have seen
  the change.
- **A new app is never deployed.** `mcs affected` finds an app by its host project,
  `Apps/{App}/{App}.App/*.csproj`; an app created with `mcs create-app` has one. Its first release is triggered
  by the push that adds it, since every file in it is new.
- **Migrations and the running version.** The old version keeps serving while the new migrations run, so a
  migration must leave the schema usable by both: add a column in one release, start using it in the next,
  drop the old one in a third.
