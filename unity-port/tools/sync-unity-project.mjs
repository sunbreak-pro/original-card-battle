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
// Project path resolution: CLI argument > UNITY_PROJECT_PATH env var > DEFAULT_PROJECT.

import { readFileSync, writeFileSync, mkdirSync, existsSync, readdirSync } from "node:fs";
import { join, dirname, basename } from "node:path";

// The confirmed development machine's Unity project (Universal 2D, Unity 6000.5.5f1).
const DEFAULT_PROJECT = "C:/Users/user/Unity/RPG-by-card";

const BS = String.fromCharCode(92); // Windows path separator, kept escape-free
const args = process.argv.slice(2);
const dryRun = args.includes("--dry-run");
const noPolyfill = args.includes("--no-polyfill");
const withParity = args.includes("--with-parity");
const positional = args.filter((a) => !a.startsWith("-"));

const projectPath = (positional[0] ?? process.env.UNITY_PROJECT_PATH ?? DEFAULT_PROJECT).split(BS).join("/");
const repoRoot = process.cwd().split(BS).join("/");
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

// --- what goes where --------------------------------------------------------

const csFiles = (dir) =>
  readdirSync(dir)
    .filter((f) => f.endsWith(".cs"))
    .map((f) => join(dir, f));

const plan = [
  // engine-free core -> Assets/Core (asmdef sets noEngineReferences)
  ...csFiles(`${repoRoot}/unity-port/BattleCore`)
    .filter((f) => !(noPolyfill && basename(f) === "IsExternalInit.cs"))
    .map((src) => [src, `${projectPath}/Assets/Core/${basename(src)}`]),
  [`${kit}/Assets/Core/BattleCore.asmdef`, `${projectPath}/Assets/Core/BattleCore.asmdef`],

  // EditMode tests -> Assets/Tests
  ...csFiles(`${repoRoot}/unity-port/BattleCore.Tests`)
    // ParityTests needs System.Text.Json, which Unity does not ship. Parity is
    // already proven headlessly by `npm run parity:check`, so it stays out of
    // Unity unless asked for (then port its JSON reading to a TextAsset first).
    .filter((f) => withParity || basename(f) !== "ParityTests.cs")
    .map((src) => [src, `${projectPath}/Assets/Tests/${basename(src)}`]),
  [`${kit}/Assets/Tests/BattleCore.Tests.asmdef`, `${projectPath}/Assets/Tests/BattleCore.Tests.asmdef`],
  ...(withParity
    ? [[
        `${repoRoot}/unity-port/BattleCore.Tests/Fixtures/parity-fixture.json`,
        `${projectPath}/Assets/Tests/Fixtures/parity-fixture.json`,
      ]]
    : []),

  // MonoBehaviour skeleton -> Assets/View
  [`${kit}/Assets/View/BattleScreenView.cs`, `${projectPath}/Assets/View/BattleScreenView.cs`],
  // parity-fixture replay script (npm run unity:trace) -> Resources so the View can load it
  [`${kit}/Assets/View/Resources/trace-actions.txt`, `${projectPath}/Assets/View/Resources/trace-actions.txt`],
];

// --- copy -------------------------------------------------------------------

let written = 0;
let unchanged = 0;

for (const [src, dest] of plan) {
  const body = readFileSync(src);
  if (existsSync(dest) && readFileSync(dest).equals(body)) {
    unchanged += 1;
    continue;
  }
  console.log(`  ${existsSync(dest) ? "update" : "add   "}  ${dest.slice(projectPath.length + 1)}`);
  if (!dryRun) {
    mkdirSync(dirname(dest), { recursive: true });
    writeFileSync(dest, body);
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
