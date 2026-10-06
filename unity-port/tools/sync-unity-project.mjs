#!/usr/bin/env node
// Drop the ported battle core into the real Unity project's Assets/ tree.
//
// The core lives once, here, in `unity-port/BattleCore/*.cs`, and is proven by
// `npm run parity:check`. The Unity project is a *consumer* of that source: this
// script copies it (plus the kit's asmdefs and View skeleton) into Assets/, so
// the Unity side never becomes a second, drifting copy.
//
// Re-run it whenever the C# core changes. It is idempotent: identical files are
// left alone (so Unity does not reimport them and .meta files stay stable).
//
// Usage:
//   node unity-port/tools/sync-unity-project.mjs [projectPath] [--dry-run] [--no-polyfill] [--with-parity]
//
//   --no-polyfill   skip IsExternalInit.cs (use if Unity reports a duplicate definition)
//   --with-parity   also copy ParityTests.cs + fixture (needs System.Text.Json — see README)
//   npm run unity:sync -- --dry-run
//
// Project path resolution: CLI argument > UNITY_PROJECT_PATH env var > DEFAULT_PROJECT
// (unity-sync-plan.mjs, which also holds the file map).

import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { buildSyncPlan, resolveProjectPath, toSlash } from "./unity-sync-plan.mjs";

const args = process.argv.slice(2);
const dryRun = args.includes("--dry-run");
const noPolyfill = args.includes("--no-polyfill");
const withParity = args.includes("--with-parity");
const positional = args.filter((a) => !a.startsWith("-"));

const projectPath = resolveProjectPath(positional[0]);
const repoRoot = toSlash(process.cwd());
const kit = `${repoRoot}/unity-port/unity-project-kit`;

// --- guards -----------------------------------------------------------------

for (const marker of ["Assets", "ProjectSettings"]) {
  if (!existsSync(join(projectPath, marker))) {
    console.error(`✗ Not a Unity project (no ${marker}/): ${projectPath}`);
    console.error("  Pass the path explicitly, or set UNITY_PROJECT_PATH.");
    process.exit(1);
  }
}
if (!existsSync(kit)) {
  console.error(`✗ Run this from the repo root — kit not found at ${kit}`);
  process.exit(1);
}

// --- what goes where (unity-sync-plan.mjs, shared with unity:check) ------------

const plan = buildSyncPlan({ repoRoot, projectPath, noPolyfill, withParity });

// --- copy -------------------------------------------------------------------

let written = 0;
let unchanged = 0;

// Every synced file is text. The two repos check the same content out with different line
// endings (core.autocrlf here; there `.asmdef` is `eol=lf` and `.cs` is plain `text`), so a
// byte compare would rewrite unchanged files on every run. Compare with line endings folded to LF.
const toLf = (text) => text.replace(/\r\n/g, "\n");
const toCrlf = (text) => toLf(text).replace(/\n/g, "\r\n");

for (const [src, dest] of plan) {
  const body = readFileSync(src, "utf8");
  const current = existsSync(dest) ? readFileSync(dest, "utf8") : null;
  if (current !== null && toLf(current) === toLf(body)) {
    unchanged += 1;
    continue;
  }
  console.log(`  ${current !== null ? "update" : "add   "}  ${dest.slice(projectPath.length + 1)}`);
  if (!dryRun) {
    mkdirSync(dirname(dest), { recursive: true });
    // Keep the destination's own line-ending style so a real change stays a content-only diff.
    // A new file keeps the source's style; git normalises it on the first add.
    const styled = current === null ? body : current.includes("\r\n") ? toCrlf(body) : toLf(body);
    writeFileSync(dest, styled);
  }
  written += 1;
}

// The Unity project needs Unity's own .gitignore; the Hub template ships one,
// so only drop the kit's copy in when the project has none.
const gitignore = `${projectPath}/.gitignore`;
if (!existsSync(gitignore)) {
  console.log("  add     .gitignore (from unity.gitignore)");
  if (!dryRun) writeFileSync(gitignore, readFileSync(`${kit}/unity.gitignore`));
  written += 1;
}

console.log(
  `\n${dryRun ? "[dry run] " : ""}${written} file(s) ${dryRun ? "would change" : "written"}, ${unchanged} already current.`,
);
console.log(`Project: ${projectPath}`);
if (written > 0 && !dryRun) {
  console.log("Next: focus the Unity Editor so it reimports, then run the EditMode tests.");
}
