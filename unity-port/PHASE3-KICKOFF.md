# Phase 3 Kickoff — Windows setup + UGUI battle screen

Everything Claude could prepare without the Unity Editor is done: the battle core
is ported (`BattleCore/`), proven against the TS core (`dotnet test`, 58/58), and
now has a Logic layer (`BattleStore`), a View contract (`IBattleView` /
`BattleViewModel`), a drop-in `unity-project-kit/`, a one-command parity check
(`npm run parity:check`), and a one-command Unity sync (`npm run unity:sync`).

**Setup status on this machine: sections 0–3 are done (verified 2026-09-05).**
What is left is section 4 — the UGUI battle screen itself. Role split:

- **[human]** — Editor GUI: scene/prefab placement, reference wiring, "does it
  feel right" checks.
- **[Claude]** — C# logic/tests/data, this repo's files, the Unity sync.
- **[MCP]** — bridge that lets Claude do some Editor actions; start read-only +
  confirm-before-write.

---

## 0. Prerequisites on this machine — ✅ done

Verified 2026-09-05: **node v24 / npm 11**, **NVIDIA GPU** (good for Unity +
later local AI art), **dotnet SDK 10.0.302**.

The cross-language loop runs end-to-end here:

```powershell
npm run parity:check   # regenerates the TS fixture, then runs the C# tests
```

Last result: fixture unchanged (TS matches golden) and `dotnet test` **58/58**
green (49 unit + 8 BattleStore/View + 1 parity). Re-run this whenever the TS core
changes — it is the proof the port still tracks the Web implementation.

---

## 1. Unity itself — ✅ done

- **Unity Hub** installed; **Unity 6000.5.5f1** with **Windows Build Support**
  and **WebGL Build Support**.
- Project created from the Hub: **`C:\Users\user\Unity\RPG-by-card`**, template
  **Universal 2D** (URP 17.6.0 with the 2D Renderer).
  - Template note: the earlier plan said _2D (Built-In)_. Built-In is flagged as
    legacy in Unity 6, and the whole reason for the Unity move is visual
    fidelity, so **Universal 2D is the deliberate choice** (2D lights and
    post-processing are available when the battle screen needs them).
  - Path is ASCII with no spaces — required by several Editor tools.
- **Separate git repo**, as planned: `https://github.com/sunbreak-pro/RPG-by-card`
  (this Web repo stays the parity baseline).
- "Unity CLI を使用" was ticked at creation, so the project carries
  `com.unity.pipeline` — that package is what makes `unity mcp` / `unity test` /
  `unity command` work against this project.

### 1a. Sync the ported core into the project — `npm run unity:sync`

```powershell
npm run unity:sync                # copy core + tests + View skeleton into Assets/
npm run unity:sync -- --dry-run   # show what would change, write nothing
```

The core lives **once**, in `unity-port/BattleCore/*.cs`. The script copies it
(plus the kit's asmdefs and `BattleScreenView.cs`) into the Unity project's
`Assets/`, so Unity is a consumer rather than a second, drifting copy. It is
idempotent — unchanged files are left alone, which keeps `.meta` files stable.

Layout it produces:

| Destination     | Source                                                                    |
| --------------- | ------------------------------------------------------------------------- |
| `Assets/Core/`  | `unity-port/BattleCore/*.cs` + `BattleCore.asmdef` (engine-free)          |
| `Assets/Tests/` | `unity-port/BattleCore.Tests/*.cs` + `BattleCore.Tests.asmdef` (EditMode) |
| `Assets/View/`  | `unity-project-kit/Assets/View/BattleScreenView.cs`                       |

Flags for the two known Unity-side gotchas:

- `--no-polyfill` — skips `IsExternalInit.cs`. Use it only if Unity reports a
  duplicate definition for that type.
- `--with-parity` — also copies `ParityTests.cs` and its fixture. **Off by
  default**: that test uses `System.Text.Json`, which Unity does not ship, so it
  would break the whole test assembly. Parity is already proven headlessly by
  `npm run parity:check`; to run it inside Unity, port its file reading to a
  `TextAsset` first.

Different machine or a renamed project? Pass the path
(`npm run unity:sync -- D:/path/to/Project`) or set `UNITY_PROJECT_PATH`.

Run `npm run unity:sync` after every change to the C# core, then let the Editor
reimport.

---

## 2. Claude Code on Windows — ✅ done

Native Windows Claude Code is in use. C# file editing and `dotnet test` work as
they do for the Web repo.

---

## 3. Unity MCP — ✅ done (official Unity CLI)

MCP lets Claude create GameObjects, add components, read Console errors, and run
EditMode tests from inside Unity. Keep Unity open while using it (the bridge
lives inside the Editor), and stay read-only-first with a confirm-before-write
posture for anything that edits a scene.

**This machine uses the official Unity CLI's own MCP server** — no third-party
package needed. Registered 2026-09-05 with:

```powershell
unity mcp configure claude-code --project-path C:/Users/user/Unity/RPG-by-card
```

which wrote the `unity-editor-mcp` stdio server into `~/.claude.json` at user
scope (`unity mcp --project-path …`). **It becomes visible only after Claude Code
is restarted.**

> Superseded (2026-07-05 research): IvanMurzak/Unity-MCP, CoplayDev/unity-mcp and
> CoderGamester/mcp-unity were the candidates before the official CLI shipped
> `unity mcp`. Keep them in mind only as a fallback if the official bridge turns
> out to be too thin — and note that Unity's paid `com.unity.ai.assistant` is a
> different thing, still preview and subscription-gated, deliberately not used.

### 3a. Smoke test

With Unity open, ask Claude to "list the GameObjects in the current scene" or
"run one EditMode test". A sensible reply = the bridge is connected.

---

## 4. [human + Claude] Build the battle screen — ✅ done (2026-09-06)

Done: `BattleScreenView.Render` is implemented (code-built UGUI, auto-bootstrapped
into any scene). Verified with the official Unity CLI: EditMode 57/57, and the
parity fixture replayed through the store (`npm run unity:trace` →
`Resources/trace-actions.txt`, expected lines in `unity-project-kit/expected-trace.txt`)
matched the Web trace 18/18 in the Console. `SystemRng` random play ran 3 battles
without errors. Remaining human step: play it by hand with the Editor focused
(the "乱数: 実戦" button unpins the RNG).

Gotchas found: (1) tests need an explicit `using NUnit.Framework;` — csproj implicit
usings do not exist in Unity (known-issue 002); (2) an unfocused Editor does not
advance frames in Play Mode, so coroutines stall — for scripted checks dispatch on
the store via `unity command eval_file` and step with `EditorApplication.Step()`;
(3) `capture_game_view --save_path` must stay under `Assets/`.

Original scope, kept for reference:

The wiring already compiles (`BattleScreenView` → `BattleStore` → `IBattleView`).
Phase 3 is filling in `Render`:

- both-side panels: HP / stamina / guard,
- distance as **real on-screen spacing + posture sprites** (2026-07-04 decision;
  `ViewModel.DistanceLabel` still supplies the text),
- hand from `vm.Hand` → clickable cards → `OnCardClicked(instanceId)`,
- End Turn / Restart buttons → `OnEndTurnClicked` / `OnRestartClicked`,
- log (newest first) and a result overlay when `vm.BattleOver`.

First checkpoint before any UI work: open the project once, let it import
`Assets/`, and confirm the Console is clean and the EditMode tests run
(58 minus the 1 parity test = **57** expected in Unity).

**Acceptance:** play one battle to a win/loss and restart, with a fixed seed
(`new FixedRng(0)`) matching the Web trace. Then unpin the RNG for real play.

---

## Guardrails (carry over from the plan)

- Keep MonoBehaviours thin; logic stays in the engine-free `BattleCore` assembly
  (`noEngineReferences: true` enforces it).
- Scope is **core + one battle**. No art/Live2D, no full card/enemy roster, no
  new combat systems (sword-energy / stagger), no 3D, no distribution yet.
- Don't hand-edit scene/prefab YAML — GUID/fileID references break.
- Never edit the C# core inside `Assets/Core/` — it is generated by
  `npm run unity:sync` and will be overwritten. Edit `unity-port/BattleCore/`.
- When the TS core changes, run `npm run parity:check` before trusting the port.
