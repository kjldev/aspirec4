# Copilot Instructions

## Non-negotiables

- **Always run `just test` (unit + integration) to verify changes** — running only `just test-unit` is not acceptable. Unit tests prove nothing about the wider system; integration tests exercise Docker, container startup, bind mounts, and the full Aspire lifecycle.
- **Never claim a fix or task is complete unless ALL tests have passed** — every single test, not just those you believe are related to your change. All tests always are. A task is not done, a fix is not done, nothing is done until `just test` exits green with zero failures.
- **Never skip tests without explicit user permission** — do not use `--filter`, `[Skip]`, or any other mechanism to exclude tests unless the user has explicitly said so for that specific case.
- **Always run `just lintcheck` (or `just lintfix`) before committing** — CSharpier formatting is enforced and CI will reject unformatted code.
- **Never add `Co-authored-by` trailers to commit messages** — do not include Copilot or any other co-author attribution.

## Build, test, and lint

This repo uses [just](https://just.systems/) as a task runner. All commands operate on `src/AspireC4.slnx`.

```sh
just restore          # Restore NuGet packages and local tools
just build            # Build in Release (default)
just build Debug      # Build in Debug

just test             # Run all tests (unit + integration)
just test-unit        # Run unit tests only
just test-integration # Run integration tests only

just lintcheck        # Check formatting with CSharpier
just lintfix          # Auto-fix formatting with CSharpier
```

**Running a single test** — use `--filter` with the Microsoft.Testing.Platform style:
```sh
dotnet test src/tests/AspireC4.UnitTests/AspireC4.UnitTests.csproj -- --filter "FullyQualifiedName~Generate_EmptyModel"
dotnet test src/tests/AspireC4.IntegrationTests/AspireC4.IntegrationTests.csproj -- --filter "FullyQualifiedName~MethodName"
```

Integration tests require Docker to be running and pull `ghcr.io/likec4/likec4`.

## Architecture

This is a .NET Aspire extension library that auto-generates live [LikeC4](https://likec4.dev) architecture diagrams from the Aspire resource graph.

### Projects

| Project | Purpose |
|---|---|
| `src/src/AspireC4` | Aspire integration: lifecycle hook, Docker/CLI server resources, dashboard integration |
| `src/src/AspireC4.Core` | Pure model & DSL: `LikeC4ModelBuilder`, `LikeC4DslGenerator`, annotations |
| `src/tests/AspireC4.UnitTests` | Unit tests for model builder, DSL generator, annotations |
| `src/tests/AspireC4.IntegrationTests` | Integration tests using `DistributedApplication` (requires Docker) |
| `src/tests/AspireC4.TestAppHost` | AppHost used by integration tests |

### Data flow

1. `AddLikeC4Visualization()` (in `LikeC4VisualizationExtensions`) registers `LikeC4VisualizationLifecycleHook` and a `LikeC4ServerResource` (Docker container: `ghcr.io/likec4/likec4`).
2. On `BeforeStartEvent`, the lifecycle hook calls `LikeC4ModelBuilder.Build()` to traverse the Aspire resource graph into a `LikeC4Model`, then `LikeC4DslGenerator.Generate()` to write a `.c4` file to disk (default: `./likec4/model.c4` relative to the AppHost).
3. The LikeC4 container mounts the output directory via a named Docker volume (name derived from a SHA-256 hash of the AppHost path) and hot-reloads when the file changes.
4. A debounced background watcher calls `ResourceNotificationService` to detect state changes and regenerate the file, updating element colours to reflect live resource states.
5. **HMR relay**: On Windows (or for pre-1.56 LikeC4 images with a fixed HMR port), a TCP relay listens on port 24678 on the host and proxies connections to the dynamic Docker-allocated port.

**Alternative server mode**: `.WithLocalCli()` swaps the Docker container for a local Node.js CLI (npx/pnpm/yarn/bun/deno). `.WithHideFromDashboard()` removes the sidecar from the Aspire dashboard and surfaces a link+command on each `ProjectResource` instead.

### Exclusion logic

Resources are excluded from the diagram if they carry `ExcludeFromLikeC4Annotation` or if their `ResourceSnapshotAnnotation.InitialSnapshot.IsHidden == true`. The LikeC4 sidecar resource itself is always excluded.

## Key conventions

### Namespace placement
Public extension methods live in `namespace Aspire.Hosting` (matching Aspire's own namespace) and require suppressing the IDE0130 warning:
```csharp
#pragma warning disable IDE0130
namespace Aspire.Hosting;
#pragma warning restore IDE0130
```
Internal implementation classes use `namespace Aspire.Hosting.AspireC4`.

### Test framework
Tests use **TUnit** (not xUnit/MSTest/NUnit). Key patterns:
```csharp
[Test]
public async Task SomeTest()
{
    await Assert.That(value).IsEqualTo(expected);
}

[Before(Test)]
public async Task SetUpAsync() { ... }
```
Mocking uses **NSubstitute**. Both are globally imported in all test projects via `Directory.Build.props`.

## Test authoring requirements

The following test authoring rules are strict and non-negotiable. All generated tests must comply exactly.

### Required test structure
- Every test must use the AAA pattern.
- Every test must include explicit section comments:
  - `// Arrange`
  - `// Act`
  - `// Assert`

### Required test method signature and naming
- Every test method must use the exact naming pattern:
  - `public async Task {SubjectUnderTest}_{Scenario}_{Expectation}()`
- `{SubjectUnderTest}` must normally be the name of the method being exercised.
- Do not use alternative naming conventions.

### Required class and namespace conventions
- Every class under test must have its own dedicated test class.
- Every test class must be named exactly `{Class}Tests`.
- Every test class must be placed in the same namespace as the class under test.

### Required dependency creation pattern
- Never instantiate dependencies directly inside a test method.
- Dependencies, services, and SUTs must be created through helper methods using the `CreateXXX` pattern.
- SUT factory methods must allow optional dependencies so individual tests can override only the collaborators they need.
- This pattern must be used consistently to support extension and maintenance.

### Required fixture reuse
- Reuse common setup through shared fixtures whenever appropriate.
- Do not duplicate setup that can be centralized safely.

### Required CancellationToken usage
- If any downstream or invoked method accepts a `CancellationToken`, the test must include and pass a `CancellationToken`.
- This requirement is mandatory and exists to preserve correct cancellation flow and improve task-cancellation coverage.

### Required libraries and mocking conventions
- All assertion usage for the assertion library must follow TUnit best practices.
- All record/validate style tests must use NSubstitute.
- Where collaborator verification is required, use NSubstitute consistently.

### Behavioral expectations
- Tests must be focused, deterministic, and readable.
- Each test should validate a single behavior or outcome.
- Avoid hidden setup and avoid ad hoc dependency construction.

### Required example shape
Use this pattern unless a stronger existing local convention already matches all requirements above:

```csharp
public class {Class}Tests
{
    [Test]
    public async Task {SubjectUnderTest}_{Scenario}_{Expectation}()
    {
        // Arrange
        var dependency = CreateDependency();
        var cancellationToken = new CancellationTokenSource().Token;
        var sut = CreateSut(dependency: dependency);

        // Act
        var result = await sut.{SubjectUnderTest}(..., cancellationToken);

        // Assert
        await Assert.That(result).IsNotNull();
    }

    private static IDependency CreateDependency()
    {
        return Substitute.For<IDependency>();
    }

    private static {Class} CreateSut(
        IDependency? dependency = null)
    {
        return new {Class}(
            dependency ?? CreateDependency());
    }
}
```

### Enforcement
If a generated test conflicts with any rule above, the rule above wins.
These requirements are mandatory and must not be relaxed, omitted, or replaced.

### Central package management
All NuGet versions are in `src/Directory.Packages.props`. Never specify a `Version` attribute directly in a `.csproj` — add to `Directory.Packages.props` first.

### Directory.Build.props conventions
- `NamespacePrefix` must be set per project (in `src/Directory.Build.props` it is `Aspire.Hosting`).
- Test projects automatically get a `ProjectReference` to their matching source project (by stripping the `UnitTests`/`IntegrationTests` suffix from the project name).
- Non-test, non-excluded projects automatically get `Purview.Telemetry.SourceGenerator` for telemetry.
- Resources in `Resources/**/*` are automatically embedded.

### Commit messages
Conventional commits are enforced (via `commitlint.config.mts`). Types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style`, `test`. Subject must be lower-case, no trailing period, max 100 chars.

### LikeC4 icon inference
`LikeC4ModelBuilder` infers icons automatically from resource type names/annotations. Azure resources map to `azure:*` icons; generic tech (postgres, redis, node, docker, etc.) maps to `tech:*` icons. Override per-resource with `.WithLikeC4Details(icon: "tech:redis")`.

### Container runtime override
Set `ASPIRE_CONTAINER_RUNTIME` environment variable to use an alternative to `docker` (e.g., `podman`).
