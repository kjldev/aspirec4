---
name: pre-release
description: Creates a pre-release for AspireC4. Use this skill when asked to create a pre-release, publish an early-access version, increment the pre-release counter, or run the pre-release process.
---

## AspireC4 Pre-Release Process

Use `just release prerelease` (backed by `scripts/release.mts`) to create a pre-release.

### Prerequisites (all must pass)

- On a **development branch** — NOT `main` and NOT `release/*`
- **Working tree is clean** — all changes committed
- **Up to date with `origin/main`**

Check readiness first:
```sh
just release-help
```

### Steps

1. Commit all changes to the development branch:
   ```sh
   just lintfix
   just test
   git add -A
   git commit -m "fix: <description>"
   ```

2. Run the pre-release:
   ```sh
   just release prerelease
   ```

The script will:
- Read the Aspire version from `src/Directory.Packages.props` (MAJOR.MINOR locked to `Aspire.Hosting`)
- If already on a pre-release, increment the counter (e.g. `13.3.0-prerelease.7` → `13.3.0-prerelease.8`)
- If on a stable version, create `X.Y.(Z+1)-prerelease.0`
- Auto-generate a changeset from conventional commits if none exist
- Run `npx changeset version` to write `CHANGELOG.md` and `package.json`
- Create `release/vX.Y.Z-prerelease.N` branch, commit (`chore: release vX.Y.Z-prerelease.N`), push, and open a PR to `main`

### After the PR is merged

The CD pipeline (`.github/workflows/cd.yml`) runs automatically on `main` and:
1. Packs the NuGet package with the pre-release version (marked as pre-release on GitHub)
2. Creates a GitHub Release with `.nupkg` / `.snupkg` assets

### Version rules

- `MAJOR.MINOR` is always locked to `Aspire.Hosting` (check `src/Directory.Packages.props`)
- Pre-release counter increments within the same `X.Y.Z` base (e.g. `.7` → `.8`)
- A new PATCH base is used only when moving from stable → pre-release or when Aspire MAJOR.MINOR changes
- Never manually edit `package.json` version — always let `just release prerelease` manage it

### Local NuGet testing (optional)

To test a pre-release package locally before pushing:
```sh
dotnet pack src/src/AspireC4/AspireC4.csproj -c Release -o "p:\_sync-projects\.local-nuget\" "/p:Version=X.Y.Z-prerelease.N"
```

Then clear the NuGet global cache for that version:
```sh
# Remove from NuGet global-packages cache
$cache = dotnet nuget locals global-packages --list | ForEach-Object { $_ -replace "global-packages: ", "" }
Remove-Item "$cache\aspirec4.hosting\X.Y.Z-prerelease.N" -Recurse -Force -ErrorAction SilentlyContinue

# Remove Aspire restore cache (if cached with stale DLL)
Get-ChildItem "C:\Users\$env:USERNAME\.aspire\packages\restore\" | ForEach-Object {
  $libs = Join-Path $_.FullName "libs"
  if ((Test-Path $libs) -and (Get-ChildItem $libs -Filter "AspireC4.dll" -ErrorAction SilentlyContinue)) {
    Remove-Item $_.FullName -Recurse -Force
  }
}
```

### Troubleshooting

If the release branch already exists:
```sh
git branch -D release/vX.Y.Z-prerelease.N
git push origin --delete release/vX.Y.Z-prerelease.N   # if pushed
just release prerelease
```
