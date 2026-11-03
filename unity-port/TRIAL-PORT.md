# The browser trial, ported to Unity (View/Trial)

The browser game 「Original Card Battle 試遊版」 (title → 継承の間 → 出立 → seven layers of exploration and battle →
ending → the next heir) is the approved look and feel. This folder of work ports it into the Unity project so the
Unity build plays and looks the same, animations and sounds included. Koudai-san's request (2026-10-22): reproduce it
in the Unity Editor at the same quality; it becomes the base form of the game.

- **The JS look and feel wins for everything on screen** (layout px, colours, fonts, every animation's ms / ease /
  delay, sounds, keys). **The C# cores win for rules.** The JS core is a 1:1 port of the C#; where the JS added rules
  the C# lacks (inheritance, trial items and events, journal intel, deck limits, multi-enemy fixes, free step), they
  are ported to C# as **opt-in** extensions: with the option off, every existing C# behaviour and test stays as is.
- **One deliberate departure from the JS lengths (Koudai-san, 2026-11-02):** the battle's stance, guard and movement
  beats play 0.2–0.4 s slower in Unity. Every such length lives in `$KV/Battle/Flow/BattlePacing.cs`, which gives the
  Trial's own `EffectSwitches` their lengths (`EffectSwitches.SetLength`, opt-in). The catalog and `Battle.unity` keep
  the old lengths. Add any later departure there, not in the ported files.
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

## Where this port departs from the JS

Only Koudai-san's decisions go here; everything else follows the JS.

- **Character art (2026-11-01, #772).** A figure that has a standing picture (立ち絵, `Assets/Art/Characters/<id>/chr_<id>_idle_stand.png`) shows the picture; a figure without one keeps the JS vector silhouette (`FieldArt`). The picture wins over the JS for the figure only.
- **Layer backdrops (2026-11-03, #89).** Koudai-san asked for more information in the backdrops and a colour of its own for every layer (the deep ones were all near-black; layer 7 a starry sky; one more bright layer in the upper part, so layer 3 is bright now and 1-3 are bright, 4 the brightest; a pale-yellow layer). The values in `Backdrop.Script.BackdropSpec` / `ExplorationBackdropSpec` changed, so every screen that reads them departs from the JS: the battle, the exploration (including the interlude's sky strip), the title (layer 7) and the ending. Each layer also draws a far view in code (`BackdropScenery`: trunks, pillars, crystals, ridges, walls, ribs, coils, shards, between the sky and the haze) and may have further speck sets (`LayerBackdrop.MoreMotes`, stars with `Motes.Still`); `BattleBackdrop`, `ExploreBackdrop` and `MetaBackdrop` draw them and mark the additions "Unity addition". The exploration veil takes a dark shade of each layer's hue instead of the ground colour. The hall's strata skies (`HallBackdropSpec.StratumSky`) follow the new skies, so `HallBackdropSpecTests` pins the Unity values, not the JS ones, from here on. The old `View/Backdrop/BackdropLayers.cs` (`Battle.unity`) takes the new colours but draws neither the far view nor the extra specks.
- **Layer backdrop art (2026-11-03, #858).** When a layer's painted art is in `Resources/Backdrops/layer-<n>` (a Sprite; the path is `Backdrop.Script.BackdropArt`, which the old `BackdropLayers` reads too), `BattleBackdrop`, `ExploreBackdrop` (the slot and the interlude's sky strip) and `MetaBackdrop` (the title on layer 7, `TitleBackdropSpec`, and the ending; the hall's room is not a layer and takes none) lay it over the sky in place of the far view, cropped to fill without stretching (`BackdropArt.Cover`). The hazes, the specks, and each screen's earth, veil, shade or vignette stay on top. Without the art every screen draws as before. Under the gamma target an sRGB picture is shown through a linear copy of its bytes (`Trial.Kit.ArtImage`), so it is not darkened.
- **Miasma breath (2026-11-02, #773).** The breath phase is carried by `Backdrop.Script.MiasmaBreath` instead of being derived from the clock and the percent, because the JS formula jumps whenever the percent changes. The look at a steady percent is the same. The explore scene keeps one `MiasmaBreath` (`ExploreScene.State.cs`), advances it once a frame on the settled reading, and hands the same breath to the gauge (`MiasmaLook.FillAlphaOf`) and the shade (`MiasmaLook.ShadeBreathing`), so the two breathe in step; a pause such as a battle on top moves the breath at most `MiasmaBreath.MaxStepMs` (100 ms) when it resumes. The meta screens' `MetaTimelines.Breath` keeps the clock-based phase (its periods never change) but takes its cosine from `BackdropMotion.CosineDip`, so the formula lives in one place.

- **The next heir's name can be written over (2026-11-03, #105).** The JS only drew the name. The hall now puts the drawn name in a one-line field under the name row (`Meta/HeirNameField.cs`, the 装束 column's width, usable once the name is stamped); the row follows what is typed and shrinks a long name to fit beside the seal. 出立 passes the field to `Inheritance.StartLife(lineage, seed, name)`, and `Inheritance.ChosenHeirName` trims it, drops control characters, cuts it at `InheritanceRules.HeirNameMaxLength` (8) letters and falls back to the drawn name. The name then lives in the life record, so the past lives, the traces, the ending and the save keep it. The field's look is this port's own; Koudai-san checks it in Unity.

- **踏破 is no survivor (2026-11-03, #108).** A life that defeats 歪みの根 (`LifeEnding.Completed`) no longer becomes the survivor: the previous survivor stays as it was and the journal passes to nobody (`concept-v3.md` §8.1). Its card-only trace stays as the JS left it. `Inheritance.RecordSurvival` holds the rule, `InheritanceText.ClearedTraceNote` and the ending's lines say so. The JS parity tests compare the JS's cleared lineage less its survivor, and the random op sequences stop comparing a scenario at its first clearing (`FuzzParityTests`).

- **Save versions (2026-11-03, #109).** The JS checked only the lineage's version and set any other aside. The C# converts an older lineage through `InheritanceCore.LineageMigrations` (one step per version, none yet), refuses a newer one, and also refuses a newer save object (`Save.Version`, its `v`) and a life whose start has another version (`LifeStartData.Version`): a life in progress is never converted, it goes the way of a life that does not replay. The policy is `dungeon_exploration_v4.md` §7.9.

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
