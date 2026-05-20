set quiet

[private]
_root := "./"
[private]
_solution := "src/AspireC4.slnx"

config_default := "Release"

# List available recipes
[private]
default:
    just --list

# Initialise the repo: restore packages, install tools, and wire up git hooks.
# Safe to run multiple times.
init: restore
    npm install
    lefthook install
# ── .NET ──────────────────────────────────────────────────────────────────────

# Open the solution in the default IDE (e.g., Visual Studio or VS Code)
vs:
    open {{ _solution }}
# Run all tests (unit + integration + e2e )
test-all: test test-integration test-e2e test-e2e-cli

# Restore NuGet packages and local tools
[group('dotnet')]
restore:
    dotnet tool restore
    dotnet restore {{ _solution }}
# Build the entire solution
[group('dotnet')]
build configuration=config_default:
    dotnet build {{ _solution }} --no-restore --configuration {{ configuration }}
# Cleans the solution
[group('dotnet')]
clean configuration=config_default:
    dotnet clean {{ _solution }} --configuration {{ configuration }}
# Run all tests (unit + integration)
[group('dotnet')]
test configuration=config_default:
    dotnet test --solution {{ _solution }} --configuration {{ configuration }}
# Run unit tests only
[group('dotnet')]
test-unit configuration=config_default:
    dotnet test --project src/tests/AspireC4.UnitTests --configuration {{ configuration }}
# Run integration tests only
[group('dotnet')]
test-integration configuration=config_default:
    dotnet test --project src/tests/AspireC4.IntegrationTests --configuration {{ configuration }}
# Run C# linting (CSharpier check)
[group('dotnet')]
lintcheck:
    dotnet csharpier check {{ _root }}
# Run C# linting and auto-fix (CSharpier format)
[group('dotnet')]
lintfix:
    dotnet csharpier format {{ _root }}
# Build and produce NuGet packages into artifacts/nuget (version read from package.json)
[group('dotnet')]
pack configuration=config_default: (build configuration)
    dotnet pack {{ _solution }} --no-build --no-restore --configuration {{ configuration }} --output artifacts/nuget "/p:Version=$(node -p "require('./package.json').version")"
# ── Release ───────────────────────────────────────────────────────────────────

# Add a changeset description for the current changes (interactive)
[group('release')]
changeset:
    npx changeset add
# Compute next version from conventional commits + changesets and open a release PR.
# Pass `prerelease` to produce a prerelease version (e.g. 13.3.1-prerelease.0).
[group('release')]
release prerelease="":
    node scripts/release.mts {{ prerelease }}
# Show context-aware release advice: current branch state, next version preview, and next steps.
[group('release')]
release-help:
    node scripts/release.mts --help
# ── Icon manifest ─────────────────────────────────────────────────────────────

# Regenerate the LikeC4 icon manifest from the upstream GitHub repository
[group('dotnet')]
refresh-icons:
    node scripts/generate-icon-manifest.mts
# ── LikeC4 diagram viewer ─────────────────────────────────────────────────────

# View all LikeC4 diagrams in this repository
[group('diagrams')]
diagrams:
    just _run-likec4 .
# ── Container runtime tests ───────────────────────────────────────────────────

[private]
_e2e_docker_image := "aspirec4-e2e-docker"
[private]
_e2e_podman_image := "aspirec4-e2e-podman"
[private]
_e2e_dockerfile_docker := "tests/Docker/Dockerfile.e2e"
[private]
_e2e_dockerfile_podman := "tests/Docker/Dockerfile.e2e-podman"
[private]
_e2e_dockerfile_cli := "tests/Docker/Dockerfile.e2e-cli"

# Run integration tests against the host Docker runtime (Docker Desktop or Rancher Desktop).
# Runs natively on the host so bind-mount paths are real host paths that Docker can resolve.
[group('container-tests')]
test-e2e-docker configuration=config_default:
    dotnet test \
        --project src/tests/AspireC4.IntegrationTests \
        --configuration {{ configuration }}
# Build and run integration tests inside a Podman container (rootful Podman-in-Docker, requires --privileged)
[group('container-tests')]
test-e2e-podman configuration=config_default: (_e2e-image _e2e_podman_image _e2e_dockerfile_podman)
    docker run --rm --privileged \
        -v "{{ justfile_directory() }}:/workspace" \
        -v aspirec4-nuget-cache:/root/.nuget/packages \
        -w /workspace \
        {{ _e2e_podman_image }} \
        dotnet test \
            --project src/tests/AspireC4.IntegrationTests \
            --verbosity normal \
            --configuration {{ configuration }}
# Build and run integration tests with npx as the LikeC4 server (WithLocalCLI Npx)
[group('container-tests')]
test-e2e-npm configuration=config_default: (_e2e-cli-image "aspirec4-e2e-npm" "npm")
    just _e2e-cli-run aspirec4-e2e-npm {{ configuration }}
# Build and run integration tests with pnpm dlx as the LikeC4 server (WithLocalCLI Pnpm)
[group('container-tests')]
test-e2e-pnpm configuration=config_default: (_e2e-cli-image "aspirec4-e2e-pnpm" "pnpm")
    just _e2e-cli-run aspirec4-e2e-pnpm {{ configuration }}
# Build and run integration tests with yarn dlx as the LikeC4 server (WithLocalCLI Yarn)
[group('container-tests')]
test-e2e-yarn configuration=config_default: (_e2e-cli-image "aspirec4-e2e-yarn" "yarn")
    just _e2e-cli-run aspirec4-e2e-yarn {{ configuration }}
# Build and run integration tests with bunx as the LikeC4 server (WithLocalCLI Bun)
[group('container-tests')]
test-e2e-bun configuration=config_default: (_e2e-cli-image "aspirec4-e2e-bun" "bun")
    just _e2e-cli-run aspirec4-e2e-bun {{ configuration }}
# Build and run integration tests with deno as the LikeC4 server (WithLocalCLI Deno)
[group('container-tests')]
test-e2e-deno configuration=config_default: (_e2e-cli-image "aspirec4-e2e-deno" "deno")
    just _e2e-cli-run aspirec4-e2e-deno {{ configuration }}
# Build both e2e test images and run integration tests for Docker and Podman
[group('container-tests')]
test-e2e configuration=config_default: (test-e2e-docker configuration) (test-e2e-podman configuration)
# Build and run integration tests for all local CLI runtimes (npm, pnpm, yarn, bun, deno)
[group('container-tests')]
test-e2e-cli configuration=config_default: (test-e2e-npm configuration) (test-e2e-pnpm configuration) (test-e2e-yarn configuration) (test-e2e-bun configuration) (test-e2e-deno configuration)

# Build (or rebuild) a named e2e test runner image from its Dockerfile
[private]
_e2e-image image dockerfile:
    docker build -f {{ dockerfile }} -t {{ image }} .
# Build (or rebuild) a named local-CLI e2e image from the shared Dockerfile.e2e-cli
[private]
_e2e-cli-image image target:
    docker build --target {{ target }} -f {{ _e2e_dockerfile_cli }} -t {{ image }} .
# Run a pre-built local-CLI e2e image.
# Workspace is writable (needed by NuGet restore). Named volumes redirect build artefacts
# and package manager caches so they don't accumulate on the host across runs.
# Lock-file isolation is enforced in the entrypoint (pre-installs run from /tmp).
[private]
_e2e-cli-run image configuration:
    docker run --rm --privileged \
        -v "{{ justfile_directory() }}:/workspace" \
        -v aspirec4-nuget-cache:/root/.nuget/packages \
        -v aspirec4-testbin:/workspace/src/tests/AspireC4.IntegrationTests/bin \
        -v aspirec4-testobj:/workspace/src/tests/AspireC4.IntegrationTests/obj \
        -v aspirec4-testhost-bin:/workspace/src/src/AspireC4.TestAppHost/bin \
        -v aspirec4-testhost-obj:/workspace/src/src/AspireC4.TestAppHost/obj \
        -v aspirec4-nodeapp-modules:/workspace/samples/node-app/node_modules \
        -w /workspace \
        {{ image }} \
        dotnet test \
            --project src/tests/AspireC4.IntegrationTests \
            --verbosity normal \
            --configuration {{ configuration }}
[private]
_run-likec4 path=justfile_dir():
    just _try-docker {{ path }} || just _try-node {{ path }}
[private]
_try-docker path=justfile_dir():
    echo "Using Docker..."
    docker run --rm \
        -v "{{ justfile_directory() }}:/data" \
        --init -t \
        -p 5173:5173 -p 24678:24678 \
        -e CHOKIDAR_USEPOLLING=1 \
        -e CHOKIDAR_INTERVAL=200 \
        ghcr.io/likec4/likec4 serve {{ path }}
[private]
_try-node path=justfile_dir():
    echo "Docker not available, falling back to Node..."
    sh -c 'set -- --use-hash-history; if command -v dot >/dev/null 2>&1; then set -- "$@" --use-dot-bin; fi; npx likec4 serve "{{ path }}" "$@"'
