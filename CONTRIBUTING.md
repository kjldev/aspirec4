# Contributing to AspireC4.Hosting

Thank you for contributing! This guide covers the tools, conventions, and processes used in this repository.

---

## Table of contents

- [Prerequisites](#prerequisites)
- [Getting started](#getting-started)
- [Branding](#branding)
- [Just — task runner](#just--task-runner)
- [Code style — CSharpier](#code-style--csharpier)
- [Git hooks — Lefthook](#git-hooks--lefthook)
- [Commit messages](#commit-messages)
- [Tests](#tests)
- [Changesets](#changesets)
- [Release guide](#release-guide)

---

## Prerequisites

| Tool | Purpose |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) (version from `global.json`) | Build and test |
| [Node.js](https://nodejs.org/) (version from `package.json` → `engines.node`) | Release scripts, changesets |
| [just](https://just.systems/man/en/packages.html) | Task runner |
| [Docker](https://www.docker.com/) | Integration tests, local diagram viewer |

After cloning, install all dependencies:

```sh
npm install       # Node dependencies (changesets, release tooling)
just restore      # NuGet packages + local .NET tools (CSharpier, dotnet-inspect)
```

Lefthook hooks install automatically when Node dependencies are installed. See [Git hooks — Lefthook](#git-hooks--lefthook).

---

## Getting started

```sh
just build        # Build the solution (Release by default)
just test         # Run all tests (unit + integration)
just lintcheck    # Check formatting
```

---

## Branding

Two distinct brands exist in this repository. Use them consistently:

| Brand | What it is | Examples |
|---|---|---|
| **AspireC4** | This library / plugin | `AspireC4.Hosting` NuGet package, `AspireC4DiagramOptions`, `IAspireC4Builder`, `AddAspireC4()` |
| **LikeC4** | The third-party visualisation tool this library integrates | `ghcr.io/likec4/likec4` container, `LikeC4Model`, `LikeC4DslGenerator`, `.c4` file format |

**Rules:**
- Public extension methods and user-facing types use the `AspireC4` prefix.
- Types that directly represent LikeC4 DSL concepts keep the `LikeC4` prefix.
- Never use `LikeC4` to refer to this library, and never use `AspireC4` to refer to the third-party tool.

---

## Just — task runner

`just` is the single entry point for all development tasks. Run `just` with no arguments to list all recipes.

### .NET

| Recipe | Description |
|---|---|
| `just restore` | Restore NuGet packages and local .NET tools |
| `just build [Debug\|Release]` | Build the solution (default: `Release`) |
| `just clean` | Clean build outputs |
| `just test` | **Run all tests** (unit + integration) |
| `just test-unit` | Run unit tests only |
| `just test-integration` | Run integration tests only |
| `just lintcheck` | Check formatting with CSharpier |
| `just lintfix` | Auto-fix formatting with CSharpier |
| `just pack` | Build and pack NuGet artifacts into `artifacts/nuget/` |

### Release

| Recipe | Description |
|---|---|
| `just changeset` | Open the interactive changeset prompt to describe your changes |
| `just release` | Cut a full release (creates PR from `release/vX.Y.Z` branch) |
| `just release prerelease` | Cut a prerelease (creates PR from `release/vX.Y.Z-prerelease.N` branch) |

### Container runtime tests (local only)

| Recipe | Description |
|---|---|
| `just test-e2e-docker` | Integration tests against the host Docker daemon |
| `just test-e2e-podman` | Integration tests inside a Podman container (requires Docker) |
| `just test-e2e` | Both of the above |

### Diagrams

| Recipe | Description |
|---|---|
| `just diagrams` | Open the live LikeC4 diagram viewer for this repository |

### Filtering a single test

```sh
dotnet test src/tests/AspireC4.UnitTests/AspireC4.UnitTests.csproj \
  -- --filter "FullyQualifiedName~MyTestMethod"

dotnet test src/tests/AspireC4.IntegrationTests/AspireC4.IntegrationTests.csproj \
  -- --filter "FullyQualifiedName~MyTestMethod"
```

---

## Code style — CSharpier

All C# code is formatted with [CSharpier](https://csharpier.com/), pinned to the version in `.config/dotnet-tools.json`. It is installed as a local .NET tool via `just restore`.

```sh
just lintcheck    # Report formatting violations
just lintfix      # Auto-fix formatting violations
```

CSharpier runs automatically on every `git commit` via Lefthook. Commits with formatting violations are rejected. Always run `just lintfix` before committing if you have unsaved format changes, or configure your editor to format on save using the CSharpier extension.

**Do not pin a specific CSharpier version in `.csproj` files.** The version lives exclusively in `.config/dotnet-tools.json` and `Directory.Packages.props`.

---

## Git hooks — Lefthook

[Lefthook](https://github.com/evilmartians/lefthook) manages two hooks, configured in `.config/lefthook.yml`:

| Hook | What it does |
|---|---|
| `pre-commit` | Runs `csharpier check` across the entire `src/` tree. Rejects the commit if any file is mis-formatted. |
| `commit-msg` | Runs `commitlint` to enforce [conventional commit](#commit-messages) format. |

Lefthook installs automatically when you run `npm install`. To verify it is active:

```sh
npx lefthook install
```

If you need to bypass a hook temporarily (e.g., a work-in-progress commit you will amend):

```sh
git commit --no-verify -m "wip: ..."
```

Do not bypass hooks on commits intended for `main`.

---

## Commit messages

Commit messages must follow [Conventional Commits](https://www.conventionalcommits.org/) and are enforced by `commitlint` (via Lefthook). Rules are in `commitlint.config.mts`.

**Format:**

```
<type>(<optional scope>): <subject>

<optional body>

<optional footer>
```

**Allowed types:**

| Type | When to use |
|---|---|
| `feat` | New user-facing feature |
| `fix` | Bug fix |
| `refactor` | Code restructuring with no behaviour change |
| `perf` | Performance improvement |
| `test` | Adding or fixing tests |
| `docs` | Documentation only |
| `ci` | CI/CD workflow changes |
| `build` | Build system changes |
| `chore` | Maintenance, tooling, dependency updates |
| `style` | Formatting, whitespace (code not logic) |
| `revert` | Reverting a previous commit |

**Rules:**
- Subject must be lower-case, no trailing period, max 100 characters.
- Body lines max 100 characters.
- Breaking changes: append `!` after the type/scope, or add `BREAKING CHANGE:` in the footer.

```sh
# Good
feat(core): add image alias resolution for azure resources
fix: correct hmr port fallback on windows
chore(deps): bump aspire.hosting to 9.2.0

# Bad — upper-case subject, trailing period
Fix: Correct HMR port fallback on Windows.
```

---

## Tests

All tests in this repository **must use [TUnit](https://github.com/thomhurst/TUnit)**. Do not use xUnit, NUnit, or MSTest. TUnit is already configured as a global import in all test projects via `Directory.Build.props`.

### Key patterns

```csharp
// Test method
[Test]
public async Task Something_Should_DoX()
{
    var result = ComputeSomething();

    await Assert.That(result).IsEqualTo(expected);
}

// Setup / teardown
[Before(Test)]
public async Task SetUpAsync() { ... }

[After(Test)]
public async Task TearDownAsync() { ... }

// Shared class-level setup (used by integration tests)
[Before(Class)]
public static async Task ClassSetUpAsync(CancellationToken cancellationToken) { ... }

[After(Class)]
public static async Task ClassTearDownAsync(CancellationToken cancellationToken) { ... }
```

### Mocking

Use [NSubstitute](https://nsubstitute.github.io/) for mocking. Also globally imported.

```csharp
var myService = Substitute.For<IMyService>();
myService.DoThing().Returns("value");
```

### Project structure

| Project | What to test here |
|---|---|
| `AspireC4.UnitTests` | `LikeC4ModelBuilder`, `LikeC4DslGenerator`, annotations, options — no Docker required |
| `AspireC4.IntegrationTests` | Full Aspire lifecycle: container startup, file generation, endpoint availability |

Integration tests require Docker to be running. They pull `ghcr.io/likec4/likec4` on first run.

### CI behaviour

- **Unit + integration tests run on every PR** against both Docker and Podman runtimes in parallel.
- The **CI Gate** is a required status check — all jobs must pass before a PR can merge.

---

## Changesets

This repository uses [Changesets](https://github.com/changesets/changesets) to track what changed between releases and generate `CHANGELOG.md` entries.

### When to add a changeset

Add a changeset for every PR that changes user-facing behaviour: new features, bug fixes, breaking changes, deprecations. You do **not** need a changeset for CI, tooling, test, or documentation-only changes.

### How to add a changeset

```sh
just changeset
```

This runs the interactive `changeset add` prompt. Select the bump type and write a short description of the change. The file is saved to `.changeset/<slug>.md`.

### Changeset format

```md
---
"aspirec4": patch
---

Fix incorrect volume mount path on Windows when output directory is on a different drive.
```

The package name is always `"aspirec4"` (lower-case, matching `package.json`). Bump types:

| Type | When to use |
|---|---|
| `patch` | Bug fixes, minor improvements — **use this for almost everything** |
| `minor` | Significant new user-facing capability |
| `major` | Breaking change |

> **Note:** The MAJOR.MINOR version is always locked to the `Aspire.Hosting` package version in `src/Directory.Packages.props`. Changesets influence the CHANGELOG content and bump logic, but the release script enforces the Aspire version constraint regardless of the changeset bump type.

### What happens at release time

The `just release` script:
1. Reads all pending changesets in `.changeset/`
2. Auto-generates a changeset from conventional commits if none exist
3. Runs `npx changeset version` to write `CHANGELOG.md` and consume the changeset files
4. Overrides the computed version with the Aspire-constrained version
5. Commits everything to a `release/vX.Y.Z` branch and opens a PR

You never need to run `npx changeset version` manually.

---

## Release guide

### Versioning scheme

Versions follow `MAJOR.MINOR.PATCH[-prerelease.N]`:

- **MAJOR.MINOR** always matches the `Aspire.Hosting` package version in `src/Directory.Packages.props`. When Aspire ships a new MAJOR.MINOR, update that file and the next release resets PATCH to 0.
- **PATCH** increments with each release regardless of whether changes are features or fixes (because MAJOR.MINOR is locked).
- **Prerelease** suffix `-prerelease.N` is used for early-access builds. Each successive prerelease on the same PATCH increments N.

Examples: `13.3.0-prerelease.0` → `13.3.0-prerelease.1` → `13.3.0` → `13.3.1`

---

### Before releasing

1. Make sure your working tree is **clean** (`git status` shows nothing).
2. Make sure you are on a **development branch** (e.g., `chore/my-feature`), **not on `main`** and not on an existing `release/` branch.
3. Make sure all intended changesets are present in `.changeset/` — or the script will auto-generate one from commits.

---

### Cutting a prerelease

Use this when you want to publish an early-access build without committing to a stable API.

```sh
just release prerelease
```

What happens:
- Computes the next `-prerelease.N` version (increments N if already on a prerelease; starts at `X.Y.Z-prerelease.0` otherwise).
- Creates branch `release/vX.Y.Z-prerelease.N`.
- Commits `package.json` + `CHANGELOG.md` (changeset files consumed).
- Opens a PR against `main`.

Once the PR's CI Gate passes, merge it. The CD pipeline creates a GitHub Release with `.nupkg` / `.snupkg` artifacts.

---

### Cutting a stable release

Use this when the feature/fix set is complete and ready for general availability.

```sh
just release
```

What happens:
- Computes the next `X.Y.PATCH` version.
- Creates branch `release/vX.Y.PATCH`.
- Commits `package.json` + `CHANGELOG.md`.
- Opens a PR against `main`.

Once the PR's CI Gate passes, merge it.

---

### Branch summary

| Branch | Purpose |
|---|---|
| `main` | Always reflects the latest published state. Every merge triggers CD. |
| `chore/*`, `feat/*`, `fix/*`, etc. | Development branches. **Start releases from here.** |
| `release/vX.Y.Z[-prerelease.N]` | Automatically created by `just release [prerelease]`. Never create manually. |

> **Start all releases from a development branch, not from `main`.**
> Running `just release` on `main` itself would mean there is no development work to fold in and the release PR would have an empty diff.

---

### CD pipeline

Merging any `release/v*` branch to `main` triggers `.github/workflows/cd.yml`, which:

1. Detects whether `package.json` changed (no-op if not).
2. Checks for duplicate tags / GitHub Releases to prevent double-publishing.
3. Builds the solution, runs unit and integration tests.
4. Packs NuGet artifacts.
5. Creates a GitHub Release tagged `vX.Y.Z[-prerelease.N]` with `.nupkg` and `.snupkg` attached.

> The CD pipeline does **not** push to NuGet.org automatically. Download the `.nupkg` from the GitHub Release and push manually, or configure a NuGet push step in the workflow for your fork.

---

### Fixing a broken release branch

If a `release/vX.Y.Z` branch already exists (e.g., from a previously failed run), the release script will exit with a clear error. Clean it up and retry:

```sh
# Remove local branch
git branch -D release/vX.Y.Z

# Remove remote branch (if pushed)
git push origin --delete release/vX.Y.Z

# Retry
just release [prerelease]
```
