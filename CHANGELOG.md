# aspirec4

## 13.3.0

### Patch Changes

- [#5](https://github.com/kjldev/aspirec4/pull/5) [`3e2f953`](https://github.com/kjldev/aspirec4/commit/3e2f9539f079ce8070e7bd90752451847ba05c7a) Thanks [@kieronlanning](https://github.com/kieronlanning)! - ### Bug Fixes

  - Fix unit test `AddAspireC4_ExposesHttpAndHmrEndpoints` to always expect `null` HMR port (relay always active with `latest` image tag)
  - Fix `ValidatedDsl_*` unit tests failing in CI due to missing Node.js setup step

  ### Changes

  - Add `actions/setup-node@v6` to CI and CD workflows for reliable Node.js availability
  - Generate `.snupkg` symbol packages alongside `.nupkg` in `dotnet pack`
  - `just pack` now reads version from `package.json` instead of using the fallback from `Directory.Build.props`
  - Add `@changesets/cli` release flow: `just changeset` and `just release [prerelease]`
