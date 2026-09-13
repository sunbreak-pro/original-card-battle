# Unity Project Kit — drop-in scaffolding for the real Unity project

This folder holds the **Unity-specific glue** for Phase 3 (the real Unity
project + UGUI battle screen). It does _not_ duplicate the battle core — that
lives once in `unity-port/BattleCore/*.cs` and is proven by `dotnet test`. Here
you get only the pieces a Unity project needs on top of that pure C#: assembly
definitions, a `.gitignore`, and a MonoBehaviour View skeleton already wired to
the store.

> Everything a human must do in the Unity Editor is called out with **[Editor]**.
> Everything else Claude can prepare as plain files (this kit).

## Layout

```
unity-project-kit/
├── unity.gitignore                    → rename to .gitignore at the Unity project root
└── Assets/
    ├── Core/BattleCore.asmdef          engine-free assembly for the ported core
    ├── Tests/BattleCore.Tests.asmdef   EditMode test assembly (NUnit)
    └── View/BattleScreenView.cs        MonoBehaviour skeleton (implements IBattleView)
```

## Steps

### 1. [Editor] Create the Unity project — ✅ done on this machine

- `C:\Users\user\Unity\RPG-by-card`, Unity **6000.5.5f1**, template
  **Universal 2D** (URP 2D Renderer; the earlier _2D (Built-In)_ recommendation
  was dropped — Built-In is legacy in Unity 6 and this project is moving to
  Unity for visual fidelity).
- **Separate git repo** as planned: `sunbreak-pro/RPG-by-card`. The Web repo
  stays the parity baseline.

### 2. Sync the core into `Assets/` — `npm run unity:sync`

Run from the Web repo root; add `-- --dry-run` to preview:

```powershell
npm run unity:sync
```

`unity-port/tools/sync-unity-project.mjs` copies `BattleCore/*.cs` →
`Assets/Core/`, `BattleCore.Tests/*.cs` → `Assets/Tests/`, and this kit's
asmdefs + `BattleScreenView.cs` into place. It is idempotent, so re-run it after
every change to the C# core. `ParityTests.cs` is excluded by default (see the
caveats below); `--with-parity` and `--no-polyfill` cover the exceptions.

**Never edit the copies under `Assets/Core/` — they are overwritten. Edit
`unity-port/BattleCore/`.**

### 3. Gitignore

The Hub template already ships a Unity `.gitignore`, so `unity.gitignore` here is
only a fallback — the sync script drops it in only when the project has none.

### 4. [Editor] Wire the scene (this is Phase 3)

- Create a scene, add an empty GameObject, attach `BattleScreenView`.
- Press Play: the stub `Render` logs each state change to the Console — proof the
  store → view loop runs. Then build the real UGUI (HP/stamina/guard panels, the
  hand as clickable cards calling `OnCardClicked`, the log, the result overlay).
- Distance is shown as **real on-screen spacing + posture sprites**, not a
  tab/track (2026-07-04 decision). `ViewModel.DistanceLabel` still gives the text.

## Caveats when the core lands in Unity

- **NUnit version.** The headless lib targets NUnit 4; Unity's Test Framework
  bundles NUnit 3.x. The tests only use the constraint model
  (`Assert.That(x, Is.EqualTo(y))`), which is stable across both, so they should
  compile unchanged. If a NUnit-4-only API sneaks in later, adjust it.
- **Implicit usings do not exist in Unity.** `BattleCore.Tests.csproj` injects
  `using NUnit.Framework;` into every file via `<Using Include="NUnit.Framework" />`.
  Unity's asmdef build has no such mechanism, so every test file must carry the
  `using` line itself — otherwise Unity fails with `CS0246: 'Test' could not be
  found` on every `[Test]` attribute (known-issue 002). Same rule for any other
  csproj-level implicit using: spell it out in the file.
- **`IsExternalInit.cs`.** It is a polyfill so `record`/`init` compile on
  netstandard2.1. Some Unity 6 versions now ship this type themselves — if you
  get a _duplicate definition_ error for `IsExternalInit`, just delete
  `Assets/Core/IsExternalInit.cs`.
- **`ParityTests.cs` is excluded from Unity.** It uses `System.Text.Json`, which
  Unity does not ship, so copying it in would fail to compile and take the whole
  test assembly down with it. It also reads the fixture via
  `AppContext.BaseDirectory`, which differs under Unity's test runner. Parity is
  already proven by the headless `npm run parity:check`, so Unity does not need
  it; to run it there anyway, pass `--with-parity` and first port the JSON
  reading to a `TextAsset` + `JsonUtility` (or add a JSON package).
  Expected EditMode test count in Unity: **57** (58 headless minus parity).
- **Don't hand-edit scene/prefab YAML.** GUID/fileID references break easily.
  Do placement and reference wiring in the Editor (or via Unity MCP with a
  read-only-first, confirm-before-write posture). See `../PHASE3-KICKOFF.md`.
