set quiet

[private]
_solution := "src/AspireC4.slnx"

config_default := "Release"

# List available recipes
[private]
default:
    just --list
# ── .NET ──────────────────────────────────────────────────────────────────────

# Open the solution in the default IDE (e.g., Visual Studio or VS Code)
vs:
    open {{ _solution }}
# Restore NuGet packages and local tools
[group('dotnet')]
restore:
    dotnet tool restore
    dotnet restore {{ _solution }}
# Build the entire solution
[group('dotnet')]
build configuration=config_default:
    dotnet build {{ _solution }} --no-restore --configuration {{ configuration }}
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
    dotnet csharpier check src
# Run C# linting and auto-fix (CSharpier format)
[group('dotnet')]
lintfix:
    dotnet csharpier format src
# Build and produce NuGet packages into artifacts/nuget (version read from package.json)
[group('dotnet')]
pack configuration=config_default: (build configuration)
    dotnet pack {{ _solution }} --no-build --no-restore --configuration {{ configuration }} --output artifacts/nuget "/p:Version=$(node -p "require('./package.json').version")"
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

_e2e_docker_image := "aspirec4-e2e-docker"
_e2e_podman_image := "aspirec4-e2e-podman"
_e2e_dockerfile_docker := "tests/Docker/Dockerfile.e2e"
_e2e_dockerfile_podman := "tests/Docker/Dockerfile.e2e-podman"

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

# Build both e2e test images and run integration tests for Docker and Podman
[group('container-tests')]
test-e2e configuration=config_default: (test-e2e-docker configuration) (test-e2e-podman configuration)

# Build (or rebuild) a named e2e test runner image from its Dockerfile
[private]
_e2e-image image dockerfile:
    docker build -f {{ dockerfile }} -t {{ image }} .



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
