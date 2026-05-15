# aspirec4

## 13.3.0-prerelease.4

### Minor Changes

- ### Features

  - adding first-pass source generator - very much a WIP
  - add KnownLikeC4Elements source generator
  - stable model generation across resource state changes
  - make WithRelationshipKindSpec strict param nullable with auto-detect
  - add technology support to relationship kind specifications
  - add strict mode validation to aspire c4 diagram options
  - add guards and context-aware release guide

  ### Bug Fixes

  - resolve build errors and update tests post-refactor
  - relationship kind technology uses no colon in dsl output
  - always show release guide; exit on blockers before proceeding
  - block release when branch is behind origin/main
  - validate unknown args and simplify justfile recipe
  - moved the Aspire browser token inclusion behind a AppHost options property
  - fix test extension views to use current resource element names
  - create GitHub release as draft before uploading assets to prevent immutable-release errors

  ### Changes

  - cd: releases are gated to main; workflow is enforced to reject non-main refs even on manual dispatch
  - cd: update GitHub Actions to Node.js 24-compatible versions (checkout@v6, setup-dotnet@v5, setup-node@v6)

## 13.3.0-prerelease.3

### Minor Changes

- ### Features

  - add guards and context-aware release guide

  ### Bug Fixes

  - always show release guide; exit on blockers before proceeding
  - block release when branch is behind origin/main
  - validate unknown args and simplify justfile recipe
  - moved the Aspire browser token inclusion behind a AppHost options property

  ### Changes

## 13.3.0-prerelease.2

### Patch Changes

- [#5](https://github.com/kjldev/aspirec4/pull/5) [`32b7cbd`](https://github.com/kjldev/aspirec4/commit/32b7cbdcbc7eddad915405b69cc9ee4aa47b33a1) Thanks [@kieronlanning](https://github.com/kieronlanning)! - ### Bug Fixes

  - Fix unit test `AddAspireC4_ExposesHttpAndHmrEndpoints` to always expect `null` HMR port (relay always active with `latest` image tag)
  - Fix `ValidatedDsl_*` unit tests failing in CI due to missing Node.js setup step

  ### Changes

  - Add `actions/setup-node@v6` to CI and CD workflows for reliable Node.js availability
  - Generate `.snupkg` symbol packages alongside `.nupkg` in `dotnet pack`
  - `just pack` now reads version from `package.json` instead of using the fallback from `Directory.Build.props`
  - Add `@changesets/cli` release flow: `just changeset` and `just release [prerelease]`
