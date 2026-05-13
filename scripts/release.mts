#!/usr/bin/env node
/**
 * Release orchestration script.
 *
 * Steps:
 *   0. Show release guide (always); exit 0 on --help, exit 1 if blockers found
 *   1. Guards embedded in release guide: not detached, not main, not release/*,
 *      clean tree, up to date with main
 *   2. Read Aspire version from Directory.Packages.props
 *   3. Read current version from package.json
 *   4. Determine bump type from conventional commits since last tag
 *   5. Determine bump type from pending changesets
 *   6. Auto-create a changeset from commits if none exist
 *   7. Compute Aspire-constrained new version
 *   8. Run `changeset version` to write CHANGELOG.md and package.json
 *   9. Post-process package.json to enforce Aspire-constrained version
 *  10. Create branch release/vX.Y.Z, commit, push, open PR
 *
 * Run with:
 *   node scripts/release.mts
 *   node scripts/release.mts --prerelease
 *   node scripts/release.mts --help
 */

import { execSync, spawnSync } from 'node:child_process'
import { readFileSync, writeFileSync, readdirSync, existsSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import semver from 'semver'

const __dirname = dirname(fileURLToPath(import.meta.url))
const ROOT = join(__dirname, '..')

// Filter empty strings — just's {{ "" }} interpolation can produce a bare ""
// as an argv entry on some shells.
const args = process.argv.slice(2).filter(a => a !== '')

const isPrerelease = args.includes('--prerelease') || args.includes('prerelease')
const isHelp = args.includes('--help') || args.includes('help')

const knownArgs = new Set(['--prerelease', 'prerelease', '--help', 'help'])
const unknownArgs = args.filter(a => !knownArgs.has(a))

if (unknownArgs.length > 0) {
  console.error(
    '\n' +
      `  ✗  Unknown argument(s): ${unknownArgs.map(a => JSON.stringify(a)).join(', ')}\n` +
      '\n' +
      '  Usage:\n' +
      '    just release              — stable release\n' +
      '    just release prerelease   — prerelease\n' +
      '    just release-help         — context-aware release guide\n' +
      '\n' +
      '  Or directly:\n' +
      '    node scripts/release.mts\n' +
      '    node scripts/release.mts --prerelease\n' +
      '    node scripts/release.mts --help\n',
  )
  process.exit(1)
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function run(
  cmd: string,
  opts: { cwd?: string; stdio?: 'inherit' | 'pipe'; allowFailure?: boolean } = {},
): string {
  const result = spawnSync(cmd, { shell: true, cwd: opts.cwd ?? ROOT, encoding: 'utf8' })
  if (result.status !== 0 && !opts.allowFailure) {
    console.error(`Command failed: ${cmd}`)
    console.error(result.stderr)
    process.exit(1)
  }
  return (result.stdout ?? '').trim()
}

function runPassthrough(cmd: string, extraEnv?: Record<string, string>): void {
  const env = extraEnv ? { ...process.env, ...extraEnv } : undefined
  const result = spawnSync(cmd, { shell: true, cwd: ROOT, stdio: 'inherit', env })
  if (result.status !== 0) {
    console.error(`Command failed: ${cmd}`)
    process.exit(1)
  }
}

function readJson<T>(filePath: string): T {
  return JSON.parse(readFileSync(filePath, 'utf8')) as T
}

function writeJson(filePath: string, data: unknown): void {
  writeFileSync(filePath, JSON.stringify(data, null, '\t') + '\n', 'utf8')
}

// ---------------------------------------------------------------------------
// Help / release guide
// ---------------------------------------------------------------------------

function readStateForHelp(): {
  currentBranch: string
  dirty: string
  currentVersion: string
  aspireVersion: string
  changesetCount: number
  behindMainCount: number
} {
  const currentBranch = run('git rev-parse --abbrev-ref HEAD', { allowFailure: true })
  const dirty = run('git status --porcelain', { allowFailure: true })

  let currentVersion = '(unknown)'
  let aspireVersion = '(unknown)'
  let changesetCount = 0
  let behindMainCount = 0

  try {
    currentVersion = readJson<{ version: string }>(
      join(ROOT, 'package.json'),
    ).version
  } catch {}

  try {
    const props = readFileSync(join(ROOT, 'src', 'Directory.Packages.props'), 'utf8')
    const m = props.match(/<PackageVersion\s+Include="Aspire\.Hosting"\s+Version="([^"]+)"/)
    if (m) aspireVersion = m[1]
  } catch {}

  try {
    const csDir = join(ROOT, '.changeset')
    changesetCount = readdirSync(csDir).filter(f => f.endsWith('.md') && f !== 'README.md').length
  } catch {}

  try {
    run('git fetch origin main --quiet', { allowFailure: true })
    const mainSha = run('git rev-parse origin/main', { allowFailure: true })
    const mergeBaseSha = run('git merge-base HEAD origin/main', { allowFailure: true })
    if (mainSha && mergeBaseSha && mainSha !== mergeBaseSha) {
      const n = parseInt(
        run('git rev-list --count HEAD..origin/main', { allowFailure: true }),
        10,
      )
      behindMainCount = isNaN(n) ? 1 : n
    }
  } catch {}

  return { currentBranch, dirty, currentVersion, aspireVersion, changesetCount, behindMainCount }
}

function showHelp(): void {
  const { currentBranch, dirty, currentVersion, aspireVersion, changesetCount, behindMainCount } =
    readStateForHelp()

  const isDetached = currentBranch === 'HEAD' || currentBranch === ''
  const isOnMain = currentBranch === 'main'
  const isOnReleaseBranch = currentBranch.startsWith('release/')
  const isDirty = dirty.length > 0
  const isBehindMain = behindMainCount > 0

  const sep = '─'.repeat(52)

  const out: string[] = []
  out.push('')
  out.push(`  AspireC4 — Release Guide`)
  out.push(`  ${sep}`)
  out.push('')
  out.push('  Current state:')
  out.push(`    Branch       : ${isDetached ? '(detached HEAD)' : currentBranch}`)
  out.push(`    Working tree : ${isDirty ? 'DIRTY  ✗  — uncommitted changes present' : 'clean  ✓'}`)
  out.push(
    `    Behind main  : ${isBehindMain ? `${behindMainCount} commit(s)  ✗` : 'up to date  ✓'}`,
  )
  out.push(`    Version      : ${currentVersion}`)
  out.push(`    Aspire       : ${aspireVersion}`)
  out.push(`    Changesets   : ${changesetCount} pending`)
  out.push('')

  const issues: string[] = []
  const fixes: string[] = []

  if (isDetached) {
    issues.push('Detached HEAD — you are not on any named branch.')
    fixes.push('  git checkout chore/my-feature      # switch to an existing branch')
    fixes.push('  git checkout -b chore/my-feature   # or create a new one')
  }

  if (isOnMain) {
    issues.push("You are on 'main'. Releases must start from a development branch.")
    fixes.push("  main only receives release PRs created by 'just release'.")
    fixes.push('  Switch to (or create) a development branch:')
    fixes.push('')
    fixes.push('    git checkout -b chore/my-feature    # new branch')
    fixes.push('    git checkout chore/existing-branch  # existing branch')
  }

  if (isOnReleaseBranch) {
    issues.push(`You are on a release branch '${currentBranch}'.`)
    fixes.push("  Release branches are created and managed by 'just release'.")
    fixes.push('')
    fixes.push("  If the PR is still open → merge it on GitHub to trigger the CD pipeline.")
    fixes.push('')
    fixes.push("  If you need to redo this release:")
    fixes.push(`    git checkout chore/my-feature                   # back to dev branch`)
    fixes.push(`    git branch -D ${currentBranch}`)
    fixes.push(`    git push origin --delete ${currentBranch}       # if already pushed`)
    fixes.push(`    just release [prerelease]`)
  }

  if (isDirty) {
    issues.push('Working tree is dirty — commit or stash all changes before releasing.')
    fixes.push('  Commit:   git add -A && git commit -m "chore: …"')
    fixes.push('  Or stash: git stash')
  }

  if (isBehindMain) {
    issues.push(
      `Branch is ${behindMainCount} commit(s) behind main — merge before releasing to avoid conflicts.`,
    )
    fixes.push(`  git merge origin/main   # bring in ${behindMainCount} missing commit(s)`)
    fixes.push('')
    fixes.push('  To review what you are missing:')
    fixes.push('    git log HEAD..origin/main --oneline')
  }

  if (issues.length > 0) {
    out.push('  ✗  Issues to resolve before releasing:')
    out.push('')
    issues.forEach((i, idx) => out.push(`     ${idx + 1}. ${i}`))
    out.push('')
    if (fixes.length > 0) {
      out.push('  How to fix:')
      out.push('')
      fixes.forEach(f => out.push(f))
      out.push('')
    }
  } else {
    out.push('  ✓  Ready to release!')
    out.push('')

    // Compute next versions for the preview
    let previewPrerelease = '(computing…)'
    let previewStable = '(computing…)'
    try {
      const isCurrentPrerelease = /-prerelease\.\d+$/.test(currentVersion)
      const parsedBase = semver.parse(currentVersion.replace(/-prerelease\.\d+$/, ''))
      if (parsedBase && aspireVersion !== '(unknown)') {
        const aspireMajMin = `${semver.major(aspireVersion)}.${semver.minor(aspireVersion)}`
        const currentMajMin = `${parsedBase.major}.${parsedBase.minor}`
        let base: string
        if (aspireMajMin !== currentMajMin) {
          base = `${aspireVersion.split('.').slice(0, 2).join('.')}.0`
          base = semver.inc(base, 'patch') ?? base
        } else {
          const rawBase = `${parsedBase.major}.${parsedBase.minor}.${parsedBase.patch}`
          base = isCurrentPrerelease ? rawBase : (semver.inc(rawBase, 'patch') ?? rawBase)
        }
        if (isCurrentPrerelease) {
          const m = currentVersion.match(/^(.+)-prerelease\.(\d+)$/)
          previewPrerelease = m
            ? `${m[1]}-prerelease.${parseInt(m[2], 10) + 1}`
            : `${base}-prerelease.0`
          previewStable = base
        } else {
          const nextPatch = semver.inc(currentVersion, 'patch') ?? currentVersion
          const stableBase = `${parsedBase.major}.${parsedBase.minor}.${parsedBase.patch + 1}`
          previewPrerelease = `${stableBase}-prerelease.0`
          previewStable = nextPatch
        }
      }
    } catch {}

    out.push('  Commands:')
    out.push('')
    out.push(`    just release prerelease    — publish ${previewPrerelease}  (early access)`)
    out.push(`    just release               — publish ${previewStable}  (stable)`)
    out.push('')
    if (changesetCount === 0) {
      out.push('  ℹ  No pending changesets found.')
      out.push("     The script will auto-generate one from your conventional commits.")
      out.push("     To write a manual changeset: just changeset")
      out.push('')
    } else {
      out.push(`  ℹ  ${changesetCount} pending changeset(s) will be consumed.`)
      out.push('')
    }
  }

  out.push(`  ${sep}`)
  out.push('  Rules:')
  out.push('')
  out.push(`    • MAJOR.MINOR is always locked to Aspire.Hosting (currently ${aspireVersion}).`)
  out.push('    • Release from a development branch  (chore/*, feat/*, fix/*, …), never main.')
  out.push('    • Working tree must be clean.')
  out.push('    • Release branches (release/v*) are created automatically — never branch from one.')
  out.push('    • Add a changeset: just changeset')
  out.push('')
  out.push('  See CONTRIBUTING.md §Release guide for full details.')
  out.push('')

  console.log(out.join('\n'))
  return { hasIssues: issues.length > 0, currentBranch }
}

// ---------------------------------------------------------------------------
// 0. Always show release guide; proceed only when there are no blockers.
// ---------------------------------------------------------------------------

const { hasIssues, currentBranch } = showHelp()

if (isHelp) {
  process.exit(0)
}

if (hasIssues) {
  process.exit(1)
}

console.log(`Proceeding with ${isPrerelease ? 'prerelease' : 'stable'} release from branch '${currentBranch}'...`)

// ---------------------------------------------------------------------------
// 2. Read Aspire version from Directory.Packages.props
// ---------------------------------------------------------------------------

const packagesPropsPath = join(ROOT, 'src', 'Directory.Packages.props')
const packagesPropsContent = readFileSync(packagesPropsPath, 'utf8')
const aspireVersionMatch = packagesPropsContent.match(
  /<PackageVersion\s+Include="Aspire\.Hosting"\s+Version="([^"]+)"/,
)
if (!aspireVersionMatch) {
  console.error('Could not find Aspire.Hosting version in Directory.Packages.props')
  process.exit(1)
}
const aspireVersion = aspireVersionMatch[1]
const aspireMajorMinor = `${semver.major(aspireVersion)}.${semver.minor(aspireVersion)}`
console.log(`Aspire version: ${aspireVersion} (MAJOR.MINOR = ${aspireMajorMinor})`)

// ---------------------------------------------------------------------------
// 3. Read current version from package.json
// ---------------------------------------------------------------------------

const packageJsonPath = join(ROOT, 'package.json')
const packageJson = readJson<{ version: string; [key: string]: unknown }>(packageJsonPath)
const currentVersion = packageJson.version
console.log(`Current version: ${currentVersion}`)

// ---------------------------------------------------------------------------
// 4. Determine bump type from conventional commits since last tag
// ---------------------------------------------------------------------------

type BumpType = 'major' | 'minor' | 'patch'

function bumpPriority(b: BumpType): number {
  return b === 'major' ? 2 : b === 'minor' ? 1 : 0
}

function maxBump(a: BumpType, b: BumpType): BumpType {
  return bumpPriority(a) >= bumpPriority(b) ? a : b
}

const lastTag = run('git describe --tags --abbrev=0', { allowFailure: true })

const commitRange = lastTag ? `${lastTag}..HEAD` : 'HEAD'
const commitMessages = run(`git log ${commitRange} --no-merges --pretty=format:"%s"`)
  .split('\n')
  .filter(Boolean)

console.log(`\nCommits since ${lastTag || 'beginning'} (${commitMessages.length}):`)
commitMessages.forEach(m => console.log(`  ${m}`))

let commitBump: BumpType = 'patch'
const featCommits: string[] = []
const fixCommits: string[] = []
const otherCommits: string[] = []

for (const msg of commitMessages) {
  if (/BREAKING.CHANGE/i.test(msg) || /^[^:]+!:/.test(msg)) {
    commitBump = 'major'
    otherCommits.push(msg)
  } else if (/^feat(\(.+?\))?:/.test(msg)) {
    commitBump = maxBump(commitBump, 'minor')
    featCommits.push(msg)
  } else if (/^fix(\(.+?\))?:/.test(msg)) {
    fixCommits.push(msg)
  } else {
    otherCommits.push(msg)
  }
}

console.log(`\nCommit bump type: ${commitBump}`)

// ---------------------------------------------------------------------------
// 5. Scan pending changesets
// ---------------------------------------------------------------------------

const changesetDir = join(ROOT, '.changeset')
const changesetFiles = readdirSync(changesetDir).filter(
  f => f.endsWith('.md') && f !== 'README.md',
)

let changesetBump: BumpType = 'patch'
for (const file of changesetFiles) {
  const content = readFileSync(join(changesetDir, file), 'utf8')
  if (content.includes(`"aspirec4": major`)) changesetBump = maxBump(changesetBump, 'major')
  else if (content.includes(`"aspirec4": minor`)) changesetBump = maxBump(changesetBump, 'minor')
}

console.log(
  `Changeset bump type: ${changesetBump} (${changesetFiles.length} pending changeset(s))`,
)

const effectiveBump: BumpType = maxBump(commitBump, changesetBump)
console.log(`Effective bump type: ${effectiveBump}`)

// ---------------------------------------------------------------------------
// 6. Auto-create a changeset from commits if none exist
// ---------------------------------------------------------------------------

if (changesetFiles.length === 0) {
  console.log('\nNo pending changesets — auto-generating from conventional commits...')

  const lines: string[] = []
  if (featCommits.length > 0) {
    lines.push('### Features\n')
    featCommits.forEach(m => lines.push(`- ${m.replace(/^feat(\(.+?\))?:\s*/, '')}`))
    lines.push('')
  }
  if (fixCommits.length > 0) {
    lines.push('### Bug Fixes\n')
    fixCommits.forEach(m => lines.push(`- ${m.replace(/^fix(\(.+?\))?:\s*/, '')}`))
    lines.push('')
  }
  if (otherCommits.length > 0) {
    lines.push('### Changes\n')
    otherCommits
      .filter(m => !/^(chore|ci|docs|style|refactor|test|build)(\(.+?\))?:/.test(m))
      .forEach(m => lines.push(`- ${m}`))
    lines.push('')
  }

  if (lines.length === 0) {
    lines.push('- Maintenance and dependency updates')
  }

  // Unique short hash for the changeset filename
  const hash = run('git rev-parse --short HEAD')
  const changesetContent = `---
"aspirec4": ${effectiveBump}
---

${lines.join('\n').trim()}
`
  const autoChangesetPath = join(changesetDir, `auto-${hash}.md`)
  writeFileSync(autoChangesetPath, changesetContent, 'utf8')
  console.log(`  Created ${autoChangesetPath}`)
}

// ---------------------------------------------------------------------------
// 7. Compute Aspire-constrained new version
// ---------------------------------------------------------------------------

const currentSemver = semver.parse(currentVersion.replace(/-prerelease\.\d+$/, ''))!
const currentMajorMinor = `${currentSemver.major}.${currentSemver.minor}`
const currentIsPrerelease = /-prerelease\.\d+$/.test(currentVersion)

// For prerelease: if already on a prerelease, just increment the counter — no PATCH bump.
// PATCH only increments when going from a stable release (or Aspire MAJOR.MINOR change).
let newVersion: string
if (isPrerelease && currentIsPrerelease && aspireMajorMinor === currentMajorMinor) {
  const prereleaseMatch = currentVersion.match(/^(.+)-prerelease\.(\d+)$/)!
  newVersion = `${prereleaseMatch[1]}-prerelease.${parseInt(prereleaseMatch[2], 10) + 1}`
} else {
  // Compute new base: bump PATCH (all conventional commit types map to PATCH since MAJOR.MINOR is Aspire-locked)
  let newBase: string
  if (aspireMajorMinor !== currentMajorMinor) {
    console.log(`\nAspire MAJOR.MINOR changed (${currentMajorMinor} → ${aspireMajorMinor}), resetting patch`)
    const aspireBase = `${aspireVersion.split('.').slice(0, 2).join('.')}.0`
    newBase = semver.inc(aspireBase, 'patch') ?? aspireBase
  } else {
    const base = `${currentSemver.major}.${currentSemver.minor}.${currentSemver.patch}`
    newBase = semver.inc(base, 'patch') ?? base
  }
  newVersion = isPrerelease ? `${newBase}-prerelease.0` : newBase
}
console.log(`\nNew version: ${currentVersion} → ${newVersion}`)

// ---------------------------------------------------------------------------
// 8. Run `changeset version` to write CHANGELOG.md and package.json
// ---------------------------------------------------------------------------

console.log('\nRunning changeset version...')
const githubToken = run('gh auth token', { allowFailure: true })
runPassthrough('npx changeset version', githubToken ? { GITHUB_TOKEN: githubToken } : undefined)

// ---------------------------------------------------------------------------
// 9. Post-process package.json and CHANGELOG.md to enforce Aspire-constrained version
// ---------------------------------------------------------------------------

const updatedPackageJson = readJson<{ version: string; [key: string]: unknown }>(packageJsonPath)
if (updatedPackageJson.version !== newVersion) {
  console.log(
    `Overriding changeset version (${updatedPackageJson.version}) with Aspire-constrained version (${newVersion})`,
  )
  updatedPackageJson.version = newVersion
  writeJson(packageJsonPath, updatedPackageJson)
}

const changelogPath = join(ROOT, 'CHANGELOG.md')
if (existsSync(changelogPath)) {
  const changelogContent = readFileSync(changelogPath, 'utf8')
  // Replace the first ## heading (which changeset writes with its own computed version)
  // with the correct Aspire-constrained version.
  const fixedChangelog = changelogContent.replace(/^## .+$/m, `## ${newVersion}`)
  if (fixedChangelog !== changelogContent) {
    console.log(`Updating CHANGELOG.md header to ## ${newVersion}`)
    writeFileSync(changelogPath, fixedChangelog, 'utf8')
  }
}

// ---------------------------------------------------------------------------
// 10. Create branch, commit, push, open PR
// ---------------------------------------------------------------------------

const branch = `release/v${newVersion}`

// Detect an already-existing release branch and fail fast with a clear message.
const existingBranch = run(`git branch --list ${branch}`, { allowFailure: true })
if (existingBranch) {
  console.error(
    '\n' +
      `  ✗  Branch '${branch}' already exists locally.\n` +
      '\n' +
      '  This can happen after a previously interrupted release run.\n' +
      '  Remove the stale branch and retry:\n' +
      '\n' +
      `    git branch -D ${branch}\n` +
      `    git push origin --delete ${branch}   # if already pushed\n` +
      '    just release [prerelease]\n',
  )
  process.exit(1)
}
const existingRemoteBranch = run(`git ls-remote --heads origin ${branch}`, { allowFailure: true })
if (existingRemoteBranch) {
  console.error(
    '\n' +
      `  ✗  Branch '${branch}' already exists on origin.\n` +
      '\n' +
      '  If the release PR was already opened, check its status on GitHub.\n' +
      '  To start fresh:\n' +
      '\n' +
      `    git push origin --delete ${branch}\n` +
      '    just release [prerelease]\n',
  )
  process.exit(1)
}

console.log(`\nCreating branch ${branch}...`)
run(`git checkout -b ${branch}`)
run('git add -A')
run(`git commit -m "chore: release v${newVersion}"`)

console.log('Pushing branch...')
run(`git push -u origin ${branch}`)

console.log('Opening PR...')
runPassthrough(
  `gh pr create --title "chore: release v${newVersion}" --body "Release v${newVersion}" --base main`,
)

console.log(`\nDone! PR opened for release v${newVersion}.`)
console.log('')
console.log('  Next steps:')
console.log('    1. Wait for the CI Gate to pass on the PR.')
console.log('    2. Merge the PR — this triggers the CD pipeline.')
console.log('    3. The CD pipeline creates a GitHub Release with .nupkg / .snupkg artifacts.')
console.log('    4. Download the .nupkg from the GitHub Release and push to NuGet.org.')
console.log('')
console.log('  Run  just release-help  at any time to review the release guide.')
