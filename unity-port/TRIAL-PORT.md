# The browser trial, ported to Unity (View/Trial)

The browser game 「Original Card Battle 試遊版」 (title → 継承の間 → 出立 → seven layers of exploration and battle →
ending → the next heir) is the approved look and feel. This folder of work ports it into the Unity project so the
Unity build plays and looks the same, animations and sounds included. Koudai-san's request (2026-10-22): reproduce it
in the Unity Editor at the same quality; it becomes the base form of the game.

- **The JS look and feel wins for everything on screen** (layout px, colours, fonts, every animation's ms / ease /
  delay, sounds, keys). **The C# cores win for rules.** The JS core is a 1:1 port of the C#; where the JS added rules
  the C# lacks (inheritance, trial items and events, journal intel, deck limits, multi-enemy fixes, free step), they
  are ported to C# as **opt-in** extensions: with the option off, every existing C# behaviour and test stays as is.
- The existing Unity views (`View/Depiction`, `View/Exploration`, `View/Journal`) are not changed by this work; they
  keep serving `Battle.unity` and `BattleDepiction.unity`. Their pure C# (`Script/`, `Bridge/`) is reused.

## Where things are

| what | path |
| --- | --- |
| JS source of the trial (read only) | branch `origin/claude/nifty-dirac-a99nv7`, folder `prototypes/browser-game/` (session copy: `$P` below) |
| Built artifact (minified) | https://claude.ai/artifact/SMRswFnM5LEGuFjNmcEfo8 |
| Area analyses (porting reports) | `$PLAN/{kit,rules,app,audio,battle-flow,battle-views,explore,meta-a,meta-b}.md` |
| Reference screenshots (1920×1080) | `$REF/shots/` with `$REF/INDEX.md` |
| Unity kit (this repo) | `unity-port/unity-project-kit/Assets/View/Trial/` (`$KV`) |
| Unity project | `C:/Users/user/Unity/RPG-by-card` (Unity 6000.5.5f1, URP 2D, uGUI 2.5 + TMP, Input System only) |

## Assemblies

| assembly | folder | engine | holds | dotnet tests |
| --- | --- | --- | --- | --- |
| `Trial.Async` | `$KV/Async` | free | `Promise`, `Promise<T>`, `PromiseSource`, `Microtasks`, `Ease`, `TrialClock`, `ITweenable`, `TweenVars` | `Trial.Async.Tests` |
| `Trial.Geometry` | `$KV/Geometry` | free | vector kit: paths, meshes, colours, CSS boxes, gradients, hatching, text maths, flex/grid, CSS parsing, icon/figure data | `Trial.Geometry.Tests` |
| `Trial.Kit` | `$KV/Kit` | Unity | stage, vector graphics, TMP text, motion, input, masks, common stage.js ports, DS port `Ocb.*` | — |
| `Trial.Audio.Synth` | `$KV/Audio/Synth` | free | WebAudio subset, presets, ambience → PCM | `Trial.Audio.Synth.Tests` |
| `Trial.Audio` | `$KV/Audio` | Unity | clip cache, sfx player, ambience host, master bus | — |
| `InheritanceCore` | `unity-port/InheritanceCore` (→ `Assets/InheritanceCore`) | free | lineage rules (port of Inheritance.js) | `InheritanceCore.Tests` |
| `Trial.Bridge` | `$KV/Bridge` | free | app seam: life setup, names, journal intel, enemy pages, save data, JSON, replay recorder, departure carry | `Trial.Bridge.Tests` |
| `Trial.Script` | `$KV/Script` | free | screen-side pure helpers: scene texts, line builders, timelines | `Trial.Script.Tests` |
| `Trial.Battle.Flow` | `$KV/Battle/Flow` | free | port of player.js, battle clock, layout constants, env, module contracts | `Trial.Battle.Flow.Tests` |
| `Trial.Battle` | `$KV/Battle` | Unity | battle scene: mount, driver, input, hand / field / hud views | — |
| `Trial.Explore` | `$KV/Explore` | Unity | exploration scene | — |
| `Trial.Meta` | `$KV/Meta` | Unity | title, hall, departure, ending, settings, owned cards, journal drawer | — |
| `Trial.Boot` | `$KV/Boot` | Unity | `TrialBoot` (scene component) and the router (port of main.js) | — |

The dotnet vessel projects are `unity-port/<assembly>/<assembly>.csproj` (they only link the kit files), the tests are
`unity-port/<assembly>.Tests/`. Tests are not copied into Unity. Existing assemblies that the rule extensions touch:
`BattleCore`, `DungeonCore`, `Depiction.Script`, `Depiction.Bridge`, `Exploration.Script`, `Exploration.Bridge`
(which now references `InheritanceCore`).

## Porting conventions (JS → C#)

1. **One C# file per JS module**, same function names in PascalCase (`runAutomaticEvents` → `RunAutomaticEvents`,
   `FEET_Y` → `FeetY`). Keep the JS order inside the file. Put the JS location on each ported function:
   `// js: scenes/battle/player.js:645`. A reviewer must be able to read both side by side.
2. **Numbers and strings are byte-identical** (Japanese punctuation, full-width spaces, `〜`, `−`). Integer division,
   rounding and sort stability follow `docs/CONVENTIONS.md` of the JS (banker's rounding is the C# default; the JS
   port used `roundEven` to match it).
3. **Async**: JS promises become `Trial.Promise` (`async Promise Foo()`, `await`, `Promise.All`, `Promise.Race`,
   `.Then`, `Promise.Safe`, `PromiseSource`, `Promise<T>`). Never use `Task`, Unity `Awaitable` or coroutines for
   scene flow. `await p` always yields to `Microtasks` (as in JS); `TrialClock.Step` drains the queue at the end of a
   frame; input handlers call `Microtasks.Drain()` after they run. A fire-and-forget call is written `Foo().Forget();`.
   A scene that is destroyed stops its clock with `Stop(resolvePending: false)` (the battle) or `true` (stage.js
   clocks), exactly as the JS does.
4. **Time**: every animation runs on a `TrialClock` with the JS's ms and ease (`Ease.Out/In/InOut/Linear/Smooth`,
   `Ease.Of("cubic-bezier(...)")`, CSS keywords). CSS transitions and `@keyframes` become clock tweens/runs with the
   CSS duration, delay, iteration count and timing function. Real-time timings (input holds, the refusal line's
   3.5 s) use `WaitReal`.
5. **Coordinates**: 1920×1080 stage px, origin top-left, y down, exactly the JS numbers. Kit helpers place elements
   (`anchor = pivot = (0,1)`, `anchoredPosition = (x, −y)`). CSS `rotate(θ)` is `localEulerAngles.z = −θ`.
6. **Looks**: colours from the DS tokens (`DsTokens`), CSS declarations copied verbatim through the kit's CSS
   parser where possible (`Css.Box("background: …; border: 1px solid …; border-radius: 6px; box-shadow: …")`), so a
   reviewer can diff the strings against the JS CSS. Fonts: Display = Shippori Mincho (400/700/800), Body = Zen Kaku
   Gothic New (400/700), Mono = IBM Plex Mono (400/600/700), via `TrialFonts`.
7. **The View rules boundary** (`unity-port/Depiction.Bridge.Tests/ViewRulesBoundaryTests.cs`): under
   `Assets/View/`, only folders named `Bridge` or `Tests` may say `using BattleCore;` or call a rule class. Outside
   them, a fully qualified definition (`BattleCore.CardDef`) is fine, but never write a bare or `BattleCore.`-qualified
   member access on `TurnLoop`, `EnemyAi`, `Combat`, `Field`, `Traits`, `Statuses`, `Columns`, `Mastery`, `Cards`,
   `Chain`, `DeckRules`, `Constants`. That regex also catches **your own** identifiers with those names: never name a
   class, property, field or local `Field`, `Cards`, `Chain`, `Constants`, … and access a member on it bare
   (`Cards.Count` fails; use `FieldView`, `CardList`, `this.Cards.Count`). Rule calls the screens need go through a
   `Bridge` folder (`Trial.Bridge`, `Depiction.Bridge`, `Exploration.Bridge`).
8. **Unity specifics**: domain reload is off in this project: every static cache must use Unity's `!obj` null check
   and be reset in `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
   `raycastTarget` is off by default on every graphic and label; turn it on only where the JS element takes pointer
   events. Input is Input System only (`Keyboard.current`, `Pointer.current`, EventSystem with
   `InputSystemUIInputModule`). No `UnityEngine.Input`. No prefabs, no assets: everything is built in code.
9. **No new rules in views.** A number, verdict or line of text a screen shows comes from a core, a bridge or a scene
   text table ported from the JS (`// scene text` where the JS hard-codes it).

## Verifying

- Engine-free assemblies: `dotnet test unity-port/<Assembly>.Tests` (and `dotnet test unity-port/UnityCorePort.slnx`
  before a hand-over: every existing test must still pass).
- Unity assemblies: `node unity-port/tools/check-unity-compile.mjs` (= `npm run unity:check`) compiles every Unity
  assembly from this checkout without opening the Editor. While several people write at once, errors in files you do
  not own may appear: read only your own files' errors, and retry later if a file you depend on is half written.
- Only the main chat syncs to the Unity project (`unity:sync`) and drives the Editor (`uloop`), unless a task says so.
