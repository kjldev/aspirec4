# CLI Reference

`likec4` is npm package that provides a CLI tool for working with LikeC4 models.
Only essential commands/parameters are listed here, for full documentation use `likec4 help`, `likec4 <command> --help`.
Examples use `bunx` to run the CLI, but you should use workspace's package manager (e.g. `bun`, `pnpm`, `npm`).
If workspace is not a npm project, use `bunx` (if available) -> `pnpx` (if available) -> `npx` as a fallback.

If workspace already has `likec4` as a dependency, check its version from package.json, make sure it is at least 1.53.0. Pin the version `bunx likec4@1.53.0 ...` otherwise.

## Common Commands and Frequent Mistakes

### ✅ Correct commands (use these)

| Task              | Correct Command                                                       |
| ----------------- | --------------------------------------------------------------------- |
| Validate files    | `bunx likec4 validate --json --no-layout --file <file> [project-dir]` |
| Start dev server  | `bunx likec4 serve [project-dir]`                                     |
| Export PNG        | `bunx likec4 export png -o ./images [project-dir]`                    |
| Build static site | `bunx likec4 build -o ./dist [project-dir]`                           |
| List icons        | `bunx likec4 list-icons` or `bunx likec4 list-icons --group tech`     |

### ❌ Common mistakes (avoid these)

| Incorrect                                  | Why it fails                              | Correct                                  |
| ------------------------------------------ | ----------------------------------------- | ---------------------------------------- |
| `bunx likec4 check ...`                    | Command doesn't exist                     | Use `bunx likec4 validate ...`           |
| `bunx likec4 lint ...`                     | Command doesn't exist                     | Use `bunx likec4 validate ...`           |
| `bunx likec4 verify ...`                   | Command doesn't exist                     | Use `bunx likec4 validate ...`           |
| `bunx likec4 export png --out-dir ./images` | Unknown flag (`--out-dir`)                | Use `-o ./images` or `--outdir ./images` |

## `serve` (aliases: `start`, `dev`)

Starts local server with live reload to preview diagrams (default port is 5173).

```bash
bunx likec4 serve [project-dir]
bunx likec4 serve --port 3000 [project-dir]
```

When started, you can show the diagram to user in the browser by following the URL displayed in the console.
To navigate to specific view, use the URL path `/view/<view-id>`.

## `build` (alias: `bundle`)

Build a static website for deployment.

```bash
bunx likec4 build -o ./dist [project-dir]
```

## `export`

Export diagrams to various formats.

```bash
# PNG (requires Playwright)
bunx likec4 export png -o ./images [project-dir]
bunx likec4 export png --theme dark --flat -f "overview*" -o ./images [project-dir]

# JSON model
bunx likec4 export json -o model.json --pretty --skip-layout [project-dir]

# DrawIO
bunx likec4 export drawio --all-in-one -o ./diagrams [project-dir]
```

**export png** options: `--outdir` (`-o`), `--theme` [light|dark], `--flat`, `--filter` (`-f`, glob patterns), `--seq` (sequence layout for dynamic views), `--timeout` (default 15s)
**export json** options: `--outfile` (`-o`, default "likec4.json"), `--pretty`, `--skip-layout`
**export drawio** options: `--outdir` (`-o`), `--all-in-one`, `--roundtrip`, `--uncompressed`, `--profile` [default|leanix]

### LeanIX bridge and Draw.io (LeanIX profile)

Use these when the task is **LeanIX inventory**, **bridge artifacts**, or **Draw.io with stable bridge-managed ids** — not for generic DSL editing alone.

| Task                                           | Command                                                                  |
| ---------------------------------------------- | ------------------------------------------------------------------------ |
| Manifest + LeanIX dry-run + report             | `bunx likec4 gen leanix dry-run -o out/bridge [project-dir]`             |
| Sync workflow (review / apply)                 | `bunx likec4 sync leanix --dry-run -o out/bridge [project-dir]` / `bunx likec4 sync leanix --apply -o out/bridge [project-dir]` |
| Draw.io with `likec4Id`, `bridgeManaged`, etc. | `bunx likec4 export drawio --profile leanix -o ./diagrams [project-dir]` |

Full boundaries, round-trip notes, and MCP vs bridge: **`references/bridge-leanix-drawio.md`**.

## `codegen` (aliases: `gen`, `generate`)

Generate code artifacts from the model.

```bash
# TypeScript model (typed, with all views and elements)
bunx likec4 gen model -o likec4-model.ts [project-dir]

# React component
bunx likec4 gen react -o dist/likec4-views.mjs [project-dir]

# Web component JS bundle
bunx likec4 gen webcomponent -o likec4.js -w c4 [project-dir]

# Diagram formats
bunx likec4 gen mermaid -o ./out      # .mmd files
bunx likec4 gen plantuml -o ./out     # .puml files
bunx likec4 gen d2 -o ./out           # .d2 files
bunx likec4 gen dot -o ./out          # .dot files (Graphviz)
```

Shared options: `--outfile`/`--outdir` (`-o`), `--project` (`-p`), `--use-dot`

## `mcp`

Start MCP (Model Context Protocol) server for AI tool integration.

```bash
bunx likec4 mcp [workspace]                # stdio transport (default)
bunx likec4 mcp --http [workspace]         # HTTP transport on port 33335
bunx likec4 mcp -p 1234 [workspace]        # HTTP transport on custom port
```

Options: `--stdio` (default), `--http`, `--port` (`-p`, default 33335), `--use-dot`

## `list-icons`

List all available built-in icons. Fast, no workspace initialization needed.

```bash
bunx likec4 list-icons                        # all icons, one group:name per line
bunx likec4 list-icons --format json          # grouped JSON object
bunx likec4 list-icons --group aws            # only AWS icons
bunx likec4 list-icons --group tech -f json   # tech icons as JSON
```

Options: `--format` (`-f`, `text` default or `json`), `--group` (`-g`, one of: `aws`, `azure`, `gcp`, `tech`, `bootstrap`)

Icon groups: `aws` (~307 icons), `azure` (~614), `gcp` (~216), `tech` (~2000), `bootstrap` (~2051).

## `format`

Format LikeC4 source files in-place. Supports CI-friendly check mode.

```bash
# Format all files in current workspace
bunx likec4 format [workspace]
bunx likec4 fmt [workspace]          # alias

# Format only specific project(s) in a multi-project workspace
bunx likec4 format --project alpha --project beta [workspace]

# Format only specific files
bunx likec4 format --files src/model.c4 --files src/views.c4 [workspace]

# CI check mode: exits with code 1 if any file needs formatting (no writes)
bunx likec4 format --check [workspace]
```

| Option | Description |
| ------ | ----------- |
| `path` | Path to workspace (default: current dir; falls back to `LIKEC4_WORKSPACE` env) |
| `--project, -p` | Format only specific project(s) by name (repeatable) |
| `--files` | Format only specific files (repeatable) |
| `--check` | Check mode — exits with code 1 if files need formatting, no writes |

## `lsp`

Start the LikeC4 Language Server for editors supporting LSP (Language Server Protocol).

```bash
bunx likec4 lsp --stdio
bunx likec4 lsp --node-ipc
bunx likec4 lsp --socket 3000
bunx likec4 lsp --pipe /tmp/likec4.pipe
```

One transport option is **required**:

| Option | Description |
| ------ | ----------- |
| `--stdio` | Use stdio transport |
| `--node-ipc` | Use node-ipc transport |
| `--socket <port>` | Use socket transport on specified port |
| `--pipe <name>` | Use pipe transport with specified pipe name |
| `--watch, -w` | Enable built-in file watcher (disabled by default) |
| `--no-manual-layouts` | Disable manual layouts (enabled by default) |
| `--use-dot` | Use local Graphviz (`dot`) instead of bundled WASM |

**Standalone LSP binary** (for non-VSCode editors like Neovim, Emacs, Zed):

```bash
npm install -g @likec4/lsp
likec4-lsp --stdio   # auto-detects transport from args
```
