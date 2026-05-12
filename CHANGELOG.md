# aspirec4

## 13.4.0

### Minor Changes

- ### Features

  - add changeset-based release flow and CI fixes
  - produce snupkg symbols package on release
  - multi-runtime CI and ContainerRuntime enum for bind mount normalization
  - fixed linked content in test app host csproj
  - added LikeC4 images/ info and made sure it loads in the AppHost Test
  - run likec4 format on generated .c4 file after generation
  - null DefaultViewId navigates to LikeC4 server root
  - add configurable generated view id and default view id
  - add WithAdditionalDSLFolder, WithImageAliasFolder, and config file generation
  - tag-based state colours and remove log streaming
  - surface recent error log lines in LikeC4 element description
  - add aspire dashboard deep-links to likec4 elements
  - highlight resources with error log entries as orange
  - add custom icon resolver extension point
  - normalise tag names by stripping leading '#'
  - add navigateTo support for relationships
  - add NormaliseMetadataBehaviour option for metadata key validation
  - auto-inject aspire metadata and links into likec4 elements
  - replace hardcoded icon matching with fuzzy token-overlap scoring
  - rename generated file to model.gen.c4, add header comment, bind-mount additional dirs
  - add tags, links, metadata, custom element kind specs, groups, validate, and additional files support
  - Stopping vs Exited visual distinction via opacity in LikeC4 DSL
  - dynamic resource state updates in LikeC4 diagram
  - full Aspire integration for node-app
  - add node-app service with Redis and Postgres ping endpoints
  - use ghcr.io/likec4/likec4 container as default server, add WithLocalCli() fallback
  - implement LikeC4 live diagram plugin for .NET Aspire 13.3

  ### Bug Fixes

  - pass GITHUB_TOKEN from gh cli to changeset version command
  - correct release script version bump and changeset config
  - handle missing git tags in release script
  - bundle AspireC4.Core as private assembly in NuGet package
  - produce snupkg and read version from package.json in just pack
  - install node in ci/cd workflows and correct hmr port test assertion
  - start podman system service in e2e-podman entrypoint
  - normalize bind mount path for container runtime on Windows
  - use configured JS runtime for likec4 format and validate
  - separate named volume from bind-mount paths for live updates
  - correct README, remove stale HasErrorLogs, refactor URL helper, use ILookup for nesting
  - select aspire dashboard URL by endpoint name not scheme
  - prefer https dashboard url over http otlp endpoints
  - duplicate metadata keys correctly generated
  - improve icon matcher robustness with effectiveQueryLength and cloud-phase priority
  - node icon token dedup and snapshot urls for endpoint links
  - use snapshot url for dashboard link and command handler
  - postgres false-error state and javascript installer icon
  - correct LikeC4 custom color declaration syntax in specification block
  - declare orange as custom color in spec when HasErrorLogs state is used
  - improve icon matching for node installer and azure resources
  - improve icon matching with prefix-aware scoring and stop tokens
  - correct AspireC4\_\_ env var prefix in integration tests
  - use 'Endpoint: {name}' title for auto-injected links, fix just test recipes
  - emit group blocks before include \* in generated views
  - pass container args individually to avoid WithArgs(List<T>) serialization bug
  - propagate consumer-specified resource name throughout
  - correct DisableHMR CLI flag, restore port arg, fix integration test resource name
  - correct package naming - AspireC4 is the plugin, LikeC4 is the third-party tool
  - emit diagram-only relationships from WithLikeC4Reference
  - pin HMR host port to 24678 for live diagram updates
  - add CHOKIDAR_USEPOLLING env vars for Windows Docker Desktop
  - move color overrides to views style rules
  - resolve relationships through hidden Azure surrogate resources

  ### Changes

  - Wire Summary field from annotation through model builder and DSL generator
  - Add WithHideFromDashboard: hide LikeC4 server, surface URL/command on project resources
  - Fix Azure icon inference for RunAsContainer surrogates
  - Fix generic icon inference for plain Redis and Postgres resources
  - Add WithLikeC4Reference overload with withAspireReference parameter
  - Add WithLikeC4Reference for relationship label/technology/description overrides
  - Add configurable LikeC4 auto icons

## 13.3.0

### Patch Changes

- [#1](https://github.com/kjldev/aspirec4/pull/1) [`3e2f953`](https://github.com/kjldev/aspirec4/commit/3e2f9539f079ce8070e7bd90752451847ba05c7a) Thanks [@kieronlanning](https://github.com/kieronlanning)! - ### Bug Fixes

  - Fix unit test `AddAspireC4_ExposesHttpAndHmrEndpoints` to always expect `null` HMR port (relay always active with `latest` image tag)
  - Fix `ValidatedDsl_*` unit tests failing in CI due to missing Node.js setup step

  ### Changes

  - Add `actions/setup-node@v6` to CI and CD workflows for reliable Node.js availability
  - Generate `.snupkg` symbol packages alongside `.nupkg` in `dotnet pack`
  - `just pack` now reads version from `package.json` instead of using the fallback from `Directory.Build.props`
  - Add `@changesets/cli` release flow: `just changeset` and `just release [prerelease]`
