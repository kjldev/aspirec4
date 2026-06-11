# AspireC4.Hosting

**AspireC4.Hosting** is an [Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) extension library that generates live [LikeC4](https://likec4.dev) diagrams from the Aspire resource graph.

## Prerequisites

- **Docker** is used by default to run the LikeC4 sidecar container.
- Optional: a local Node.js CLI runtime (`npx`, `pnpm`, `yarn`, `bun`, or `deno`) if you call `.WithLocalCLI()`.

## Quick start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddAspireC4();

builder.Build().Run();
```

This writes `./likec4/gen/model.gen.c4`, starts the LikeC4 server, and refreshes the diagram as the Aspire app changes.

## Configuration

Configure the diagram through `AspireC4DiagramOptions`:

| Property | Default | Description |
|---|---|---|
| `Title` | `null` | Title shown in the LikeC4 app |
| `ViewTitle` | `"Architecture"` | Title shown in the generated view |
| `ViewDescription` | `null` | Optional view description |
| `OutputDirectory` | `"./likec4/gen/"` | Directory where the generated `.c4` file is written |
| `FileName` | `"model.gen"` | Generated file name without extension |
| `DisableHMR` | `false` | Disable Hot Module Replacement |
| `HMRPort` | `24678` | HMR port used by the LikeC4 server and browser |
| `ContainerImageTag` | `null` (`latest`) | Pin the `ghcr.io/likec4/likec4` image tag |
| `AutoIconsEnabled` | `true` | Infer LikeC4 icons from resource type and name |
| `HideFromDashboard` | `false` | Hide the LikeC4 sidecar from the Aspire dashboard |
| `DashboardLinkDisplayName` | `"Architecture Diagram"` | Name used for the diagram link when hidden from the dashboard |
| `IncludeAspireDashboardLinks` | `true` | Add Aspire dashboard links to diagram elements |

## Common options

### Local CLI

```csharp
builder.AddAspireC4().WithLocalCLI();
```

### Hide from the dashboard

```csharp
builder.AddAspireC4().WithHideFromDashboard();
```

### Disable HMR

```csharp
builder.AddAspireC4(options => options.WithHMRDisabled());
```

### Exclude the sidecar from the diagram

The LikeC4 sidecar is excluded automatically. Use `WithIncludeAspireC4InternalResource(true)` if you want to inspect it.
