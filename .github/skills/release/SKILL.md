---
name: release
description: Creates a stable release for AspireC4. Use this skill when asked to create a release, publish a new version, bump to a stable version, or run the release process.
---

## AspireC4 Stable Release Process

Use `just release` (backed by `scripts/release.mts`) to create a stable release.

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

2. Run the stable release:
   ```sh
   just release
   ```

The script will:
- Read the Aspire version from `src/Directory.Packages.props` (MAJOR.MINOR is always locked to `Aspire.Hosting`)
- Determine the next version by incrementing PATCH (e.g. `13.3.0` → `13.3.1`)
- Auto-generate a changeset from conventional commits if none exist (or consume existing `.changeset/*.md` files)
- Run `npx changeset version` to write `CHANGELOG.md` and `package.json`
- Create `release/vX.Y.Z` branch, commit (`chore: release vX.Y.Z`), push, and open a PR to `main`

### After the PR is merged

The CD pipeline (`.github/workflows/cd.yml`) runs automatically on `main` and:
1. Packs the NuGet package with the release version
2. Creates a GitHub Release with `.nupkg` / `.snupkg` assets

### Version rules

- `MAJOR.MINOR` is always locked to `Aspire.Hosting` (check `src/Directory.Packages.props`)
- `PATCH` increments on every stable release
- Never manually edit `package.json` version — always let `just release` manage it

### Manual changeset (optional)

To write a custom changelog entry before releasing:
```sh
just changeset
```

### Troubleshooting

If the release branch already exists:
```sh
git branch -D release/vX.Y.Z
git push origin --delete release/vX.Y.Z   # if pushed
just release
```
