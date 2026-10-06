#!/usr/bin/env node
// Compile every Unity assembly the way the Editor does, without opening the Editor (#344).
//
// `dotnet test` builds the C# on NUnit 4 and net10.0. Unity builds it on its own NUnit 3.5 and a
// netstandard2.1-like profile, and the View / Editor code never reaches `dotnet test` at all. An API
// that only one side has (Assert.Warn, Has.Exactly(n).Items, IsExternalInit for records) therefore
// surfaced only when someone opened Unity (#340, #342). This script replays the compiler arguments
// Unity wrote the last time it compiled (Library/Bee/artifacts/<dag>/<assembly>.rsp) through the
// csc Unity ships, with every file `unity:sync` would copy taken from this checkout instead.
// Nothing in the Unity project is written: the assemblies go to a temp folder.
//
// Usage:
//   node unity-port/tools/check-unity-compile.mjs [projectPath] [--warnings] [--keep] [--no-polyfill] [--with-parity]
//   npm run unity:check
//
//   --warnings      print every warning, not only the count
//   --keep          keep the temp folder (its path is printed)
//   --no-polyfill / --with-parity   the same switches as unity:sync, so the files match what it copies
//
// Exit code: 0 clean, 1 compile errors, 2 the check could not run (no Unity build to replay, no csc).
//
// Limits: the arguments are the ones from the last Editor compile, so a change to the arguments
// themselves (a new asmdef reference, a new define, a new assembly) shows up only after Unity has
// compiled once more; a new assembly is reported and skipped. Files in the folders the sync owns that
// this checkout no longer has are left out (they are listed): the sync never deletes them, so remove
// them from the Unity project when they are gone for good. Runtime differences (a test that compiles
// but fails on NUnit 3.5, #345) need the EditMode tests in Unity.

import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, posix } from "node:path";
import { spawnSync } from "node:child_process";
import { buildSyncPlan, resolveProjectPath, toSlash } from "./unity-sync-plan.mjs";

const args = process.argv.slice(2);
const showWarnings = args.includes("--warnings");
const keep = args.includes("--keep");
const noPolyfill = args.includes("--no-polyfill");
const withParity = args.includes("--with-parity");
const positional = args.filter((a) => !a.startsWith("-"));

const projectPath = resolveProjectPath(positional[0]);
const repoRoot = toSlash(process.cwd());

const stop = (message) => {
  console.error(`✗ ${message}`);
  process.exit(2);
};

// --- the Editor's compiler --------------------------------------------------

if (!existsSync(`${projectPath}/ProjectSettings/ProjectVersion.txt`)) {
  stop(`Not a Unity project (no ProjectSettings/ProjectVersion.txt): ${projectPath}\n  Pass the path explicitly, or set UNITY_PROJECT_PATH.`);
}
if (!existsSync(`${repoRoot}/unity-port/unity-project-kit`)) stop(`Run this from the repo root — kit not found under ${repoRoot}`);

const version = /m_EditorVersion:\s*(\S+)/.exec(readFileSync(`${projectPath}/ProjectSettings/ProjectVersion.txt`, "utf8"))?.[1];
const editor = toSlash(process.env.UNITY_EDITOR_PATH ?? `C:/Program Files/Unity/Hub/Editor/${version}/Editor`);
const sdkRoot = `${editor}/Data/DotNetSdk`;
const dotnet = `${sdkRoot}/${process.platform === "win32" ? "dotnet.exe" : "dotnet"}`;
const csc = existsSync(`${sdkRoot}/sdk`)
  ? readdirSync(`${sdkRoot}/sdk`)
      .sort()
      .reverse()
      .map((v) => `${sdkRoot}/sdk/${v}/Roslyn/bincore/csc.dll`)
      .find((p) => existsSync(p))
  : undefined;
if (!existsSync(dotnet) || !csc) {
  stop(`Unity ${version}'s compiler not found under ${sdkRoot}\n  Set UNITY_EDITOR_PATH to the Editor folder (the one holding Data/).`);
}

// --- the arguments Unity last compiled with ---------------------------------

const artifacts = `${projectPath}/Library/Bee/artifacts`;
const dag = existsSync(artifacts)
  ? readdirSync(artifacts)
      .filter((d) => d.endsWith(".dag") && existsSync(`${artifacts}/${d}/BattleCore.rsp`))
      .sort((a, b) => statSync(`${artifacts}/${b}/BattleCore.rsp`).mtimeMs - statSync(`${artifacts}/${a}/BattleCore.rsp`).mtimeMs)[0]
  : undefined;
if (!dag) stop(`No Unity build to replay under ${artifacts}\n  Open the project in Unity once (or run it with -batchmode -quit) so it writes its compiler arguments.`);
const dagRel = `Library/Bee/artifacts/${dag}`;

const SOURCE = /^"(Assets\/[^"]+\.cs)"$/;
const assemblies = new Map(); // name -> { lines, sources }
for (const file of readdirSync(`${artifacts}/${dag}`).filter((f) => f.endsWith(".rsp"))) {
  const lines = readFileSync(`${artifacts}/${dag}/${file}`, "utf8").split(/\r?\n/).filter((l) => l.length > 0);
  const sources = lines.map((l) => SOURCE.exec(l)?.[1]).filter(Boolean);
  if (sources.length > 0) assemblies.set(file.slice(0, -".rsp".length), { lines, sources });
}

// A reference to another project assembly, as Unity writes it: the ref assembly in the dag, or
// the full one in ScriptAssemblies.
const referenceTo = (line) => {
  const path = /^(?:-r|\/reference):"([^"]+)"$/.exec(line)?.[1];
  if (!path) return undefined;
  const name =
    path.startsWith(`${dagRel}/`) && path.endsWith(".ref.dll")
      ? path.slice(dagRel.length + 1, -".ref.dll".length)
      : path.startsWith("Library/ScriptAssemblies/") && path.endsWith(".dll")
        ? path.slice("Library/ScriptAssemblies/".length, -".dll".length)
        : undefined;
  return name !== undefined && assemblies.has(name) ? name : undefined;
};

// --- this checkout's files in place of the copies ---------------------------

const fromRepo = new Map(); // "Assets/..." -> file in this checkout
for (const [src, dest] of buildSyncPlan({ repoRoot, projectPath, noPolyfill, withParity })) {
  if (/\.(cs|asmdef)$/.test(dest)) fromRepo.set(dest.slice(projectPath.length + 1), toSlash(src));
}
const ownedFolders = new Set([...fromRepo.keys()].filter((p) => p.endsWith(".cs")).map((p) => posix.dirname(p)));

// The assembly a file belongs to: the nearest .asmdef above it (this checkout's copy first), else
// Unity's predefined Assembly-CSharp(-Editor).
const asmdefNames = new Map();
const asmdefIn = (folder) => {
  if (asmdefNames.has(folder)) return asmdefNames.get(folder);
  const fromCheckout = [...fromRepo.entries()].find(([rel]) => rel.endsWith(".asmdef") && posix.dirname(rel) === folder)?.[1];
  const onDisk = existsSync(`${projectPath}/${folder}`)
    ? readdirSync(`${projectPath}/${folder}`).find((f) => f.endsWith(".asmdef"))
    : undefined;
  const file = fromCheckout ?? (onDisk ? `${projectPath}/${folder}/${onDisk}` : undefined);
  const name = file ? JSON.parse(readFileSync(file, "utf8").replace(/^\uFEFF/, "")).name : undefined;
  asmdefNames.set(folder, name);
  return name;
};
const assemblyOf = (rel) => {
  for (let folder = posix.dirname(rel); folder !== "." && folder !== ""; folder = posix.dirname(folder)) {
    const name = asmdefIn(folder);
    if (name) return name;
  }
  return rel.split("/").includes("Editor") ? "Assembly-CSharp-Editor" : "Assembly-CSharp";
};

const sourcesOf = new Map();
const leftovers = [];
const listed = new Set();
for (const [name, { sources }] of assemblies) {
  const files = [];
  for (const rel of sources) {
    listed.add(rel);
    if (fromRepo.has(rel)) files.push(fromRepo.get(rel));
    else if (ownedFolders.has(posix.dirname(rel))) leftovers.push(rel);
    else files.push(rel); // the Unity repo's own file
  }
  sourcesOf.set(name, files);
}
const added = [];
const unbuilt = new Map(); // assembly Unity has not compiled yet -> its files
for (const [rel, src] of fromRepo) {
  if (!rel.endsWith(".cs") || listed.has(rel)) continue;
  const name = assemblyOf(rel);
  if (sourcesOf.has(name)) {
    sourcesOf.get(name).push(src);
    added.push(`${rel} -> ${name}`);
  } else {
    unbuilt.set(name, [...(unbuilt.get(name) ?? []), rel]);
  }
}

// --- compile in dependency order --------------------------------------------

const dependsOn = new Map([...assemblies].map(([name, { lines }]) => [name, new Set(lines.map(referenceTo).filter((r) => r && r !== name))]));
const order = [];
const placed = new Set();
while (order.length < assemblies.size) {
  const ready = [...assemblies.keys()].filter((n) => !placed.has(n) && [...dependsOn.get(n)].every((d) => placed.has(d)));
  const next = ready.length > 0 ? ready : [...assemblies.keys()].filter((n) => !placed.has(n)).slice(0, 1); // a cycle: break it
  for (const n of next.sort()) {
    order.push(n);
    placed.add(n);
  }
}

const out = toSlash(mkdtempSync(join(tmpdir(), "unity-check-")));
const built = new Set();
const quote = (p) => `"${p}"`;
const shown = (path) => {
  const p = toSlash(path);
  return p.toLowerCase().startsWith(`${repoRoot.toLowerCase()}/`) ? p.slice(repoRoot.length + 1) : `[Unity project] ${p}`;
};

let errorCount = 0;
console.log(`Unity ${version} · ${dagRel} · ${order.length} assemblies · files from ${repoRoot}\n`);
for (const name of order) {
  const fellBack = [];
  const lines = assemblies
    .get(name)
    .lines.filter((l) => !SOURCE.test(l))
    .map((l) => {
      if (l.startsWith("-out:")) return `-out:${quote(`${out}/${name}.dll`)}`;
      if (l.startsWith("-refout:")) return `-refout:${quote(`${out}/${name}.ref.dll`)}`;
      const ref = referenceTo(l);
      if (ref === undefined || ref === name) return l;
      if (built.has(ref)) return `-r:${quote(`${out}/${ref}.ref.dll`)}`;
      fellBack.push(ref);
      return l; // it failed here: compile against Unity's last build of it
    });
  const rsp = `${out}/${name}.rsp`;
  writeFileSync(rsp, [...lines, ...sourcesOf.get(name).map(quote)].join("\n") + "\n");
  const rsp2 = `${artifacts}/${dag}/${name}.rsp2`;
  const extra = existsSync(rsp2) && statSync(rsp2).size > 0 ? [`@${rsp2}`] : [];
  const run = spawnSync(dotnet, ["exec", csc, "-nostdlib", "-noconfig", `@${rsp}`, ...extra], {
    cwd: projectPath,
    encoding: "utf8",
    maxBuffer: 64 * 1024 * 1024,
  });
  const output = `${run.stdout ?? ""}${run.stderr ?? ""}`.split(/\r?\n/);
  const diagnostics = (kind) => [...new Set(output.filter((l) => l.includes(`: ${kind} CS`)))];
  const errors = diagnostics("error");
  const warnings = diagnostics("warning");
  const ok = run.status === 0 && errors.length === 0;
  if (ok) built.add(name);
  errorCount += errors.length;
  const note = fellBack.length > 0 ? ` (against Unity's last build of ${fellBack.join(", ")})` : "";
  console.log(`${ok ? "✓" : "✗"} ${name}: ${sourcesOf.get(name).length} files, ${errors.length} errors, ${warnings.length} warnings${note}`);
  for (const line of [...errors, ...(showWarnings ? warnings : [])]) {
    const at = /^(.+?\.cs)(\(\d+,\d+\): .*)$/.exec(line.trim());
    console.log(`    ${at ? shown(at[1]) + at[2] : line.trim()}`);
  }
  if (!ok && errors.length === 0) console.log(`    csc exited ${run.status}${run.error ? `: ${run.error.message}` : ""}`);
}

if (added.length > 0) console.log(`\nNew files, placed by their nearest .asmdef:\n${added.map((a) => `    ${a}`).join("\n")}`);
if (unbuilt.size > 0) {
  console.log("\nNot checked (Unity has not compiled these assemblies yet; open Unity once):");
  for (const [name, files] of unbuilt) console.log(`    ${name}: ${files.join(", ")}`);
}
if (leftovers.length > 0) {
  console.log(`\nLeft out (in the Unity project, not in this checkout; unity:sync never deletes):\n${leftovers.map((l) => `    ${l}`).join("\n")}`);
}

if (keep) console.log(`\nTemp folder kept: ${out}`);
else rmSync(out, { recursive: true, force: true });

const failed = order.filter((n) => !built.has(n));
console.log(failed.length === 0 ? "\n✓ Every Unity assembly compiles." : `\n✗ ${errorCount} errors in ${failed.join(", ")}.`);
process.exit(failed.length === 0 ? 0 : 1);
