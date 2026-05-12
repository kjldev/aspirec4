#!/usr/bin/env node
/**
 * Release orchestration script.
 *
 * Steps:
 *   1. Verify clean working tree
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
 */

import { execSync, spawnSync } from 'node:child_process'
import { readFileSync, writeFileSync, readdirSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import semver from 'semver'

const __dirname = dirname(fileURLToPath(import.meta.url))
const ROOT = join(__dirname, '..')

const isPrerelease = process.argv.includes('--prerelease')

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
// 1. Verify clean working tree
// ---------------------------------------------------------------------------

const dirty = run('git status --porcelain')
if (dirty) {
  console.error('Working tree is not clean. Commit or stash changes before releasing.')
  process.exit(1)
}

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

let newBase: string
if (aspireMajorMinor !== currentMajorMinor) {
  // Aspire bumped — new MAJOR.MINOR, reset patch to 0 (bump applied on top)
  console.log(`\nAspire MAJOR.MINOR changed (${currentMajorMinor} → ${aspireMajorMinor}), resetting patch`)
  const aspireBase = `${aspireVersion.split('.').slice(0, 2).join('.')}.0`
  newBase = semver.inc(aspireBase, effectiveBump === 'major' ? 'patch' : effectiveBump) ?? aspireBase
} else {
  // MAJOR.MINOR is Aspire-locked; all conventional-commit bump types only increment PATCH.
  const base = `${currentSemver.major}.${currentSemver.minor}.${currentSemver.patch}`
  newBase = semver.inc(base, 'patch') ?? base
}

// For prerelease: if the current version is already a prerelease on the same base, increment
// the prerelease counter; otherwise start at .0.
let newVersion: string
if (isPrerelease) {
  const prereleaseMatch = currentVersion.match(/^(.+)-prerelease\.(\d+)$/)
  if (prereleaseMatch && prereleaseMatch[1] === newBase) {
    // Same base — bump the prerelease counter
    newVersion = `${newBase}-prerelease.${parseInt(prereleaseMatch[2], 10) + 1}`
  } else {
    newVersion = `${newBase}-prerelease.0`
  }
} else {
  newVersion = newBase
}
console.log(`\nNew version: ${currentVersion} → ${newVersion}`)

// ---------------------------------------------------------------------------
// 8. Run `changeset version` to write CHANGELOG.md and package.json
// ---------------------------------------------------------------------------

console.log('\nRunning changeset version...')
const githubToken = run('gh auth token', { allowFailure: true })
runPassthrough('npx changeset version', githubToken ? { GITHUB_TOKEN: githubToken } : undefined)

// ---------------------------------------------------------------------------
// 9. Post-process package.json to enforce Aspire-constrained version
// ---------------------------------------------------------------------------

const updatedPackageJson = readJson<{ version: string; [key: string]: unknown }>(packageJsonPath)
if (updatedPackageJson.version !== newVersion) {
  console.log(
    `Overriding changeset version (${updatedPackageJson.version}) with Aspire-constrained version (${newVersion})`,
  )
  updatedPackageJson.version = newVersion
  writeJson(packageJsonPath, updatedPackageJson)
}

// ---------------------------------------------------------------------------
// 10. Create branch, commit, push, open PR
// ---------------------------------------------------------------------------

const branch = `release/v${newVersion}`
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
console.log('Merge it to trigger the CD pipeline and publish to NuGet.')
