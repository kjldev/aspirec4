---
"aspirec4": patch
---

### Bug Fixes

- Fix unit test `AddAspireC4_ExposesHttpAndHmrEndpoints` to always expect `null` HMR port (relay always active with `latest` image tag)
- Fix `ValidatedDsl_*` unit tests failing in CI due to missing Node.js setup step

### Changes

- Add `actions/setup-node@v6` to CI and CD workflows for reliable Node.js availability
- Generate `.snupkg` symbol packages alongside `.nupkg` in `dotnet pack`
- `just pack` now reads version from `package.json` instead of using the fallback from `Directory.Build.props`
- Add `@changesets/cli` release flow: `just changeset` and `just release [prerelease]`
