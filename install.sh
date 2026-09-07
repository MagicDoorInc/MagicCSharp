#!/usr/bin/env bash
#
# MagicCSharp tools installer.
#
#   curl -fsSL https://raw.githubusercontent.com/MagicDoorInc/MagicCSharp/master/install.sh | bash
#
# Installs the scaffolding scripts to ~/.magiccsharp/tools and a dispatcher named mcs to
# ~/.magiccsharp/bin. Re-running upgrades in place; nothing outside ~/.magiccsharp is written except one
# PATH line in your shell profile.
#
# Deliberately not ~/.magicdoor: MagicDoor's own `md` CLI already keeps state there, and MagicCSharp is a
# framework other teams use — including, eventually, MagicDoor's backend as one consumer among others.
#
# Environment:
#   MAGICCSHARP_HOME     install root                     (default: ~/.magiccsharp)
#   MAGICCSHARP_REF    branch, tag or commit to install (default: master)
#   MAGICCSHARP_REPO   owner/name                       (default: MagicDoorInc/MagicCSharp)
#   NO_MODIFY_PATH=1   skip touching the shell profile

set -euo pipefail

MAGICCSHARP_HOME="${MAGICCSHARP_HOME:-$HOME/.magiccsharp}"
MAGICCSHARP_REF="${MAGICCSHARP_REF:-master}"
MAGICCSHARP_REPO="${MAGICCSHARP_REPO:-MagicDoorInc/MagicCSharp}"

TOOLS_DIR="$MAGICCSHARP_HOME/tools"
TEMPLATES_DIR="$MAGICCSHARP_HOME/templates"
BIN_DIR="$MAGICCSHARP_HOME/bin"

bold=$'\033[1m'; red=$'\033[31m'; green=$'\033[32m'; yellow=$'\033[33m'; dim=$'\033[2m'; off=$'\033[0m'
say()  { printf '%s\n' "$*"; }
ok()   { printf '%s✓%s %s\n' "$green" "$off" "$*"; }
warn() { printf '%s!%s %s\n' "$yellow" "$off" "$*"; }
die()  { printf '%s✗%s %s\n' "$red" "$off" "$*" >&2; exit 1; }

# ── Preconditions ────────────────────────────────────────────────────────────────────────────────────

need() { command -v "$1" >/dev/null 2>&1 || die "$1 is required but not installed."; }
need tar

if command -v curl >/dev/null 2>&1; then
  fetch() { curl -fsSL "$1"; }
  fetch_to() { curl -fsSL "$1" -o "$2"; }
elif command -v wget >/dev/null 2>&1; then
  fetch() { wget -qO- "$1"; }
  fetch_to() { wget -qO "$2" "$1"; }
else
  die "curl or wget is required but neither is installed."
fi

command -v dotnet >/dev/null 2>&1 || die "The .NET SDK is required. Install .NET 10 from https://dotnet.microsoft.com/download"

# The scripts are file-based apps with inline #:package directives, which need SDK 10 or newer. Checking
# here turns a confusing MSBuild error on first use into a clear message now.
sdk_major="$(dotnet --version 2>/dev/null | cut -d. -f1)"
if [ -z "$sdk_major" ] || [ "$sdk_major" -lt 10 ] 2>/dev/null; then
  die ".NET SDK 10 or newer is required (found ${sdk_major:-none}). https://dotnet.microsoft.com/download"
fi

# ── Download ─────────────────────────────────────────────────────────────────────────────────────────

say ""
say "${bold}Installing MagicCSharp tools${off}"
say "${dim}  repo    $MAGICCSHARP_REPO@$MAGICCSHARP_REF${off}"
say "${dim}  into    $MAGICCSHARP_HOME${off}"
say ""

staging="$(mtemp=$(mktemp -d 2>/dev/null || mktemp -d -t magiccsharp) && printf '%s' "$mtemp")"
trap 'rm -rf "$staging"' EXIT

tarball="https://codeload.github.com/$MAGICCSHARP_REPO/tar.gz/$MAGICCSHARP_REF"
fetch_to "$tarball" "$staging/src.tar.gz" || die "Could not download $tarball"
tar -xzf "$staging/src.tar.gz" -C "$staging" || die "Downloaded archive could not be extracted."

extracted="$(find "$staging" -maxdepth 1 -type d -name '*MagicCSharp*' | head -1)"
[ -n "$extracted" ] || die "Unexpected archive layout — no MagicCSharp directory inside."
[ -d "$extracted/tools" ] || die "That ref has no tools/ directory. Try MAGICCSHARP_REF=master."

version="$(cat "$extracted/scripts/version.txt" 2>/dev/null || echo unknown)"

# The tools are installed from a git ref, so the honest "which build is this" is the commit, not the
# package version — scripts/version.txt tracks NuGet releases and moves on its own schedule. Rate limiting
# or no network leaves this empty, which simply disables the update check rather than nagging wrongly.
commit="$(fetch "https://api.github.com/repos/$MAGICCSHARP_REPO/commits/$MAGICCSHARP_REF" 2>/dev/null \
  | grep -o '"sha"[[:space:]]*:[[:space:]]*"[a-f0-9]\{40\}"' | head -1 | grep -o '[a-f0-9]\{40\}' || true)"

# ── Install ──────────────────────────────────────────────────────────────────────────────────────────

# Replace rather than merge, so a script deleted upstream does not linger and shadow a renamed one.
rm -rf "$TOOLS_DIR" "$TEMPLATES_DIR"
mkdir -p "$TOOLS_DIR" "$TEMPLATES_DIR" "$BIN_DIR"
cp -R "$extracted/tools/." "$TOOLS_DIR/"

# Templates live beside the tools rather than inside them, so they are somewhere a person can read, diff
# against their own overrides, and copy from. Nothing reads this copy except as the fallback layer.
cp -R "$extracted/tools/Templates/." "$TEMPLATES_DIR/"

printf '%s\n' "$version" > "$MAGICCSHARP_HOME/VERSION"
printf '%s\n' "$MAGICCSHARP_REF" > "$MAGICCSHARP_HOME/REF"
printf '%s\n' "${commit:-unknown}" > "$MAGICCSHARP_HOME/COMMIT"
date +%s > "$MAGICCSHARP_HOME/.last-update-check"
rm -f "$MAGICCSHARP_HOME/.update-available"

ok "tools installed ($(find "$TOOLS_DIR" -name '*.cs' -maxdepth 1 | wc -l | tr -d ' ') scripts, packages $version${commit:+, build ${commit:0:7}})"
ok "templates installed ($(find "$TEMPLATES_DIR" -name '*.hbs' | wc -l | tr -d ' ') files in $TEMPLATES_DIR)"

# ── Dispatcher ───────────────────────────────────────────────────────────────────────────────────────

cat > "$BIN_DIR/mcs" <<'MCS_DISPATCHER'
#!/usr/bin/env bash
#
# mcs — MagicCSharp scaffolding. Generated by install.sh; edits are lost on upgrade.

set -euo pipefail

MAGICCSHARP_HOME="${MAGICCSHARP_HOME:-$HOME/.magiccsharp}"
TOOLS_DIR="$MAGICCSHARP_HOME/tools"
TEMPLATES_DIR="$MAGICCSHARP_HOME/templates"
REPO="${MAGICCSHARP_REPO:-MagicDoorInc/MagicCSharp}"
REF="$(tr -d '[:space:]' < "$MAGICCSHARP_HOME/REF" 2>/dev/null || true)"; REF="${REF:-master}"

bold=$'\033[1m'; dim=$'\033[2m'; yellow=$'\033[33m'; off=$'\033[0m'

# Where the generators look for templates. A repository's own .magiccsharp/templates is searched first,
# so this is the fallback layer, not the only one.
export MAGICCSHARP_TOOLS_DIR="$TOOLS_DIR"
export MAGICCSHARP_TEMPLATES_DIR="$TEMPLATES_DIR"

# Reads a state file, empty when absent. `cat missing | tr` would fail the pipeline, and under
# `set -e` with `pipefail` an assignment inherits that status and kills the script — which only shows
# up on a terminal, because the guards above return first when output is piped.
state() { tr -d '[:space:]' < "$MAGICCSHARP_HOME/$1" 2>/dev/null || true; }

run() { exec dotnet run "$TOOLS_DIR/$1.cs" -- "${@:2}"; }

usage() {
  cat <<USAGE
${bold}mcs${off} — scaffolding for a MagicCSharp repository

${bold}Setup${off}
  mcs init --prefix Acme              set this directory up as a repository
  mcs create-app --name Shop --database shop
  mcs create-lib --name Events --tests

${bold}Inside a service${off}
  mcs create-domain --solution Acme.Shop.slnx --name Domains.Orders --models --tests
  mcs add-entity --solution Acme.Shop.slnx --domain Orders --name Order --paginated

${bold}Templates${off}
  mcs templates list                  every template, and which layer provides it
  mcs templates eject Entities/dal.cs.hbs
                                      copy one into this repository to customise
  mcs templates where                 the layers, in search order

${bold}Maintenance${off}
  mcs sync                            rebuild the all-projects solution
  mcs validate [--path .]             lint the conventions the compiler cannot
  mcs update                          upgrade the tools now
  mcs version                         installed version
  mcs which                           where the tools live

Pass --help to any command for its options.
USAGE
}

# ── Update check ─────────────────────────────────────────────────────────────
# Once a day, in the background, never blocking the command being run. Set
# MAGICCSHARP_NO_UPDATE_CHECK=1 to turn it off entirely.
check_for_updates() {
  [ -n "${MAGICCSHARP_NO_UPDATE_CHECK:-}" ] && return 0
  [ -t 1 ] || return 0   # only when talking to a terminal

  local now last
  now="$(date +%s)"
  last="$(state .last-update-check)"; last="${last:-0}"
  [ $((now - last)) -lt 86400 ] && return 0

  # Written first, so a network failure does not retry on every invocation.
  printf '%s\n' "$now" > "$MAGICCSHARP_HOME/.last-update-check" 2>/dev/null || true

  (
    latest="$(curl -fsSL --max-time 5 "https://api.github.com/repos/$REPO/commits/$REF" 2>/dev/null \
      | grep -o '"sha"[[:space:]]*:[[:space:]]*"[a-f0-9]\{40\}"' | head -1 | grep -o '[a-f0-9]\{40\}')"
    current="$(state COMMIT)"
    # An unknown local commit means the install predates commit tracking, or the API was unreachable then;
    # either way there is nothing trustworthy to compare, so say nothing.
    if [ -n "$latest" ] && [ -n "$current" ] && [ "$current" != "unknown" ] && [ "$latest" != "$current" ]; then
      printf '%s\n' "$latest" > "$MAGICCSHARP_HOME/.update-available" 2>/dev/null || true
    else
      rm -f "$MAGICCSHARP_HOME/.update-available" 2>/dev/null || true
    fi
  ) >/dev/null 2>&1 &
  disown 2>/dev/null || true
}

# Reports what yesterday's background check found, so the notice costs nothing at run time.
notify_if_update_available() {
  [ -n "${MAGICCSHARP_NO_UPDATE_CHECK:-}" ] && return 0
  [ -t 1 ] || return 0
  local pending current
  pending="$(state .update-available)"
  [ -z "$pending" ] && return 0
  current="$(state COMMIT)"
  [ "$pending" = "$current" ] && { rm -f "$MAGICCSHARP_HOME/.update-available"; return 0; }
  printf '%s\n' "${yellow}A newer mcs is available${off} ${dim}(${current:0:7} → ${pending:0:7}) — run: mcs update${off}" >&2
}

command="${1:-}"
[ $# -gt 0 ] && shift || true

case "$command" in
  ""|-h|--help|help)  usage; exit 0 ;;
  version|--version|-v)
    v="$(state VERSION)"; c="$(state COMMIT)"
    printf 'mcs — packages %s, build %s (%s@%s)\n' "${v:-unknown}" "${c:0:7}" "$REPO" "$REF"
    exit 0 ;;
  which)
    printf '%s\n' "$TOOLS_DIR"; exit 0 ;;
  update)
    exec bash -c "$(curl -fsSL "https://raw.githubusercontent.com/$REPO/$REF/install.sh")" ;;
esac

[ -d "$TOOLS_DIR" ] || { echo "mcs: tools are missing from $TOOLS_DIR — run: mcs update" >&2; exit 1; }

notify_if_update_available
check_for_updates

case "$command" in
  init)           run InitRepo "$@" ;;
  create-app)     run CreateApp "$@" ;;
  create-domain)  run CreateAppLib "$@" ;;
  create-lib)     run CreateLib "$@" ;;
  add-entity)     run AddEntity "$@" ;;
  templates)      run Templates "$@" ;;
  sync)           run SyncAllProjects "$@" ;;
  validate)       run ValidateConventions "$@" ;;
  *)
    echo "mcs: unknown command '$command'" >&2
    echo "" >&2
    usage >&2
    exit 1 ;;
esac
MCS_DISPATCHER

chmod +x "$BIN_DIR/mcs"
ok "mcs installed to $BIN_DIR/mcs"

# ── PATH ─────────────────────────────────────────────────────────────────────────────────────────────

path_line="export PATH=\"\$HOME/.magiccsharp/bin:\$PATH\""

if [ -n "${NO_MODIFY_PATH:-}" ]; then
  warn "Skipped shell profile. Add this yourself:"
  say "    $path_line"
elif command -v mcs >/dev/null 2>&1 && [ "$(command -v mcs)" = "$BIN_DIR/mcs" ]; then
  ok "already on PATH"
else
  case "${SHELL:-}" in
    */zsh)  profile="$HOME/.zshrc" ;;
    */bash) [ -f "$HOME/.bash_profile" ] && profile="$HOME/.bash_profile" || profile="$HOME/.bashrc" ;;
    *)      profile="" ;;
  esac

  if [ -n "$profile" ]; then
    if [ -f "$profile" ] && grep -qF '.magiccsharp/bin' "$profile"; then
      ok "PATH already set in $(basename "$profile")"
    else
      printf '\n# MagicCSharp tools\n%s\n' "$path_line" >> "$profile"
      ok "added to $(basename "$profile")"
    fi
    say "${dim}  open a new shell, or: source $profile${off}"
  else
    warn "Unrecognised shell. Add this to your profile:"
    say "    $path_line"
  fi
fi

say ""
say "${bold}Done.${off} Start a repository with:"
say "    ${dim}mkdir my-repo && cd my-repo${off}"
say "    mcs init --prefix Acme"
say "    mcs create-app --name Shop --database shop"
say ""
say "${dim}mcs --help for everything else${off}"
say ""
