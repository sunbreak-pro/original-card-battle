// What `unity:sync` copies into the Unity project, and where it lands. Shared by
// sync-unity-project.mjs (which copies) and check-unity-compile.mjs (which compiles this
// checkout's files in place of the copies, #344), so the two never disagree on the map.

import { existsSync, readdirSync } from "node:fs";
import { join, basename } from "node:path";

// The confirmed development machine's Unity project (Universal 2D, Unity 6000.5.5f1).
export const DEFAULT_PROJECT = "C:/Users/user/Unity/RPG-by-card";

const BS = String.fromCharCode(92); // Windows path separator, kept escape-free
export const toSlash = (path) => path.split(BS).join("/");

// Project path resolution: CLI argument > UNITY_PROJECT_PATH env var > DEFAULT_PROJECT.
export const resolveProjectPath = (positional) =>
  toSlash(positional ?? process.env.UNITY_PROJECT_PATH ?? DEFAULT_PROJECT);

const csFiles = (dir) =>
  readdirSync(dir)
    .filter((f) => f.endsWith(".cs"))
    .map((f) => join(dir, f));

// Recursive variant for folders with sub-assemblies; returns forward-slash paths.
const treeFiles = (dir, exts) =>
  existsSync(dir)
    ? readdirSync(dir, { withFileTypes: true, recursive: true })
        .filter((e) => e.isFile() && exts.some((x) => e.name.endsWith(x)))
        .map((e) => toSlash(join(e.parentPath, e.name)))
    : [];

/** Every [source in this checkout, destination in the Unity project] pair the sync writes. */
export function buildSyncPlan({ repoRoot, projectPath, noPolyfill = false, withParity = false }) {
  const kit = `${repoRoot}/unity-port/unity-project-kit`;
  return [
    // engine-free core -> Assets/Core (asmdef sets noEngineReferences)
    ...csFiles(`${repoRoot}/unity-port/BattleCore`)
      .filter((f) => !(noPolyfill && basename(f) === "IsExternalInit.cs"))
      .map((src) => [src, `${projectPath}/Assets/Core/${basename(src)}`]),
    [`${kit}/Assets/Core/BattleCore.asmdef`, `${projectPath}/Assets/Core/BattleCore.asmdef`],
    // The core and its tests are written with nullable annotations (`string?`), and both .csproj
    // compile with <Nullable>enable</Nullable>. A csc.rsp beside an asmdef applies to that assembly
    // only (and replaces Assets/csc.rsp for it), so each Unity assembly gets the same one; without
    // it every annotation is warning CS8632. Unity splits the file on whitespace: no comments in it.
    [`${kit}/Assets/Core/csc.rsp`, `${projectPath}/Assets/Core/csc.rsp`],

    // EditMode tests -> Assets/Tests
    ...csFiles(`${repoRoot}/unity-port/BattleCore.Tests`)
      // ParityTests needs System.Text.Json, which Unity does not ship. Parity is
      // already proven headlessly by `npm run parity:check`, so it stays out of
      // Unity unless asked for (then port its JSON reading to a TextAsset first).
      .filter((f) => withParity || basename(f) !== "ParityTests.cs")
      .filter((f) => !(noPolyfill && basename(f) === "IsExternalInit.cs"))
      .map((src) => [src, `${projectPath}/Assets/Tests/${basename(src)}`]),
    [`${kit}/Assets/Tests/BattleCore.Tests.asmdef`, `${projectPath}/Assets/Tests/BattleCore.Tests.asmdef`],
    [`${kit}/Assets/Core/csc.rsp`, `${projectPath}/Assets/Tests/csc.rsp`],
    ...(withParity
      ? [[
          `${repoRoot}/unity-port/BattleCore.Tests/Fixtures/parity-fixture.json`,
          `${projectPath}/Assets/Tests/Fixtures/parity-fixture.json`,
        ]]
      : []),

    // View MonoBehaviours (BattleScreenView + helpers) -> Assets/View
    ...csFiles(`${kit}/Assets/View`).map((src) => [src, `${projectPath}/Assets/View/${basename(src)}`]),
    // Battle depiction (live turn + the fixed script kept for filming): pure script assembly,
    // views, Editor builder, tests.
    // Prefabs / scenes / .meta stay in the Unity repo and are never copied from here.
    ...treeFiles(`${kit}/Assets/View/Depiction`, [".cs", ".asmdef"]).map((src) => [
      src,
      `${projectPath}/Assets/View/Depiction/${src.slice(`${kit}/Assets/View/Depiction/`.length)}`,
    ]),
    // The battle screen's typefaces and their licences (npm run fonts:fetch, #92) -> Resources so UiKit
    // can load them. Fetched, not committed: the folder is absent until fonts:fetch has run.
    ...treeFiles(`${kit}/Assets/View/Resources/Fonts`, [".ttf", ".txt"]).map((src) => [
      src,
      `${projectPath}/Assets/View/Resources/Fonts/${basename(src)}`,
    ]),
    // parity-fixture replay script (npm run unity:trace) -> Resources so the View can load it
    [`${kit}/Assets/View/Resources/trace-actions.txt`, `${projectPath}/Assets/View/Resources/trace-actions.txt`],

    // Exploration core + item catalogue (engine-free, like BattleCore) -> Assets/DungeonCore, Assets/DungeonContent
    ...csFiles(`${repoRoot}/unity-port/DungeonCore`)
      .filter((f) => !(noPolyfill && basename(f) === "IsExternalInit.cs"))
      .map((src) => [src, `${projectPath}/Assets/DungeonCore/${basename(src)}`]),
    [`${kit}/Assets/DungeonCore/DungeonCore.asmdef`, `${projectPath}/Assets/DungeonCore/DungeonCore.asmdef`],
    ...csFiles(`${repoRoot}/unity-port/DungeonContent`).map((src) => [src, `${projectPath}/Assets/DungeonContent/${basename(src)}`]),
    [`${kit}/Assets/DungeonContent/DungeonContent.asmdef`, `${projectPath}/Assets/DungeonContent/DungeonContent.asmdef`],

    // Battle backdrop (#89): the seven layers' values and motion (pure script assembly, tests) and the
    // view that draws them behind the battle. No prefab or scene: BackdropView attaches itself at run time.
    ...treeFiles(`${kit}/Assets/View/Backdrop`, [".cs", ".asmdef"]).map((src) => [
      src,
      `${projectPath}/Assets/View/Backdrop/${src.slice(`${kit}/Assets/View/Backdrop/`.length)}`,
    ]),

    // UI art (#92): which parts are drawn as art and where they load from (pure script assembly, tests),
    // and the loader that falls back to the code-drawn shape while a part has no art yet.
    ...treeFiles(`${kit}/Assets/View/UiArt`, [".cs", ".asmdef"]).map((src) => [
      src,
      `${projectPath}/Assets/View/UiArt/${src.slice(`${kit}/Assets/View/UiArt/`.length)}`,
    ]),

    // Exploration screen (#101) and the journal drawer it shares with the battle: script assemblies,
    // views, tests. No prefab or scene: ExplorationBootstrap builds the screen in code.
    ...["Exploration", "Journal"].flatMap((folder) =>
      treeFiles(`${kit}/Assets/View/${folder}`, [".cs", ".asmdef"]).map((src) => [
        src,
        `${projectPath}/Assets/View/${folder}/${src.slice(`${kit}/Assets/View/${folder}/`.length)}`,
      ]),
    ),

    // The browser trial's port (title → hall → departure → exploration ↔ battle → ending), built in code:
    // engine-free assemblies (Async, Geometry, Audio/Synth, Bridge, Script, Battle/Flow) and the Unity views.
    // Their dotnet tests live in unity-port/Trial.*.Tests and are not copied.
    ...treeFiles(`${kit}/Assets/View/Trial`, [".cs", ".asmdef"]).map((src) => [
      src,
      `${projectPath}/Assets/View/Trial/${src.slice(`${kit}/Assets/View/Trial/`.length)}`,
    ]),
    // The inheritance rules core (engine-free, like DungeonCore) -> Assets/InheritanceCore
    ...csFiles(`${repoRoot}/unity-port/InheritanceCore`).map((src) => [src, `${projectPath}/Assets/InheritanceCore/${basename(src)}`]),
    [`${kit}/Assets/InheritanceCore/InheritanceCore.asmdef`, `${projectPath}/Assets/InheritanceCore/InheritanceCore.asmdef`],
  ];
}

// The Unity project folders whose .cs and .asmdef files come only from this checkout. A flat root owns
// its own files; a recursive root owns everything below it as well. Anything of those kinds found there
// that the plan does not write is a leftover: a copy of a file this checkout has since deleted or moved
// (#615). The rest of Assets/ (Scenes, Prefabs, Editor tools the Unity repo keeps) is never looked at.
export const OWNED_ROOTS = [
  { folder: "Assets/Core", recursive: false },
  { folder: "Assets/Tests", recursive: false },
  { folder: "Assets/View", recursive: false },
  { folder: "Assets/DungeonCore", recursive: false },
  { folder: "Assets/DungeonContent", recursive: false },
  { folder: "Assets/View/Depiction", recursive: true },
  { folder: "Assets/View/Backdrop", recursive: true },
  { folder: "Assets/View/UiArt", recursive: true },
  { folder: "Assets/View/Exploration", recursive: true },
  { folder: "Assets/View/Journal", recursive: true },
  { folder: "Assets/View/Trial", recursive: true },
  { folder: "Assets/InheritanceCore", recursive: false },
];

const OWNED_KINDS = [".cs", ".asmdef"];

/**
 * The .cs and .asmdef files under OWNED_ROOTS that the plan does not write, as project-relative paths
 * ("Assets/..."), sorted. These are what `unity:sync --prune` removes and what `unity:check` refuses.
 */
export function findLeftovers({ projectPath, plan }) {
  const written = new Set(plan.map(([, dest]) => toSlash(dest).slice(projectPath.length + 1)));
  const found = new Set();
  for (const { folder, recursive } of OWNED_ROOTS) {
    const dir = `${projectPath}/${folder}`;
    if (!existsSync(dir)) continue;
    for (const entry of readdirSync(dir, { withFileTypes: true, recursive })) {
      if (!entry.isFile() || !OWNED_KINDS.some((x) => entry.name.endsWith(x))) continue;
      const rel = toSlash(join(entry.parentPath, entry.name)).slice(projectPath.length + 1);
      if (!written.has(rel)) found.add(rel);
    }
  }
  return [...found].sort();
}
