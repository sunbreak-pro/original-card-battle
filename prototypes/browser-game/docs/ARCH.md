# The playable game — architecture

Goal: a browser Artifact that plays like the real game, as close to the Unity build and the design documents as possible. One life of a 襲名 heir: 継承の間 → 出立 → the seven layers (刻限 × 瘴気, branching maps) → battles (間合い × スタミナ) → rewards → interlude → survive or die → the next heir inherits. All rules come from the ported C# cores; all looks come from the design system "Original Card Battle 戦闘画面" (`build/src/` = its `components/bundle.js`, loaded as `window.OCB`).

```
┌ src/core/battle         port of unity-port/BattleCore          rules of a battle (TurnLoop, cards, enemies, AI)
├ src/core/dungeon        port of unity-port/DungeonCore         maps, 刻限, 瘴気, encounters, reducer
├ src/core/dungeonContent port of unity-port/DungeonContent      items, loadout
├ src/bridge/exploration  port of View/Exploration/Bridge        Expedition: exploration ↔ battle, rewards, unique cards
├ src/bridge/explorationScript port of View/Exploration/Script   ExplorationScreenBuilder: the map screen's view-model + texts
├ src/bridge/journal      port of View/Journal/Script            journal book, terms
├ src/bridge/depiction    port of View/Depiction/Bridge          CoreBattleSource + CoreScriptWriter (battle → script), CoreText, DeckBuilder, DepartureBuilder …
├ src/bridge/depictionScript port of View/Depiction/Script       the script's types, effect timings, hand fan, omen disclosure …
├ src/bridge/backdrop     port of View/Backdrop/Script            per-layer backdrop values, miasma look
├ src/sim                 port of BattleCore.Sim GreedyPlayer/Decks (for parity + auto-play tests)
├ src/shared/cs.js        C# semantics helpers
├ src/scenes/common       Stage (1920×1080 scaler), h(), tweens, keys, modal card — owned by the integrator
├ src/scenes/battle       the battle scene (DOM renderer of the depiction script)
├ src/scenes/explore      the exploration scene (map, top bar, details, inventory, journal, interlude, end cards)
├ src/scenes/meta         title, 継承の間, 出立, rewards, owned cards, settings/save, endings
├ src/scenes/audio        ambience + extra sound events (extends OCB.Sound)
└ src/app                 router, save/load, settings, boot — owned by the integrator
```

## Layers of responsibility (same rule as Unity)

1. **Core** computes everything. Ported 1:1 from C#.
2. **Bridge** turns core state into view-models and texts (the Unity `Bridge/` and `Script/` folders are pure C#; we port them 1:1 too). Every number and string a screen shows comes from here.
3. **Scenes** only render view-models and send player input back (play card X on target Y, step to node N, end turn, pick reward...). A scene never computes a rule or a number. Where Unity's MonoBehaviour view (e.g. `DepictionPlayer.cs`, `ExplorationScreenView.cs`) has presentation logic (timings, layout, which panel shows when), the scene ports that presentation logic; where it has rules, they already live in the bridge.

## Screens and flow

```
Title ─▶ 継承の間 (hall of heirs: generations, untaken traces, survivor bonus)
          ─▶ 出立 (DepartureBuilder: tools / consumables / battle deck from owned cards)
              ─▶ Explore (layer N map) ─┬─ battle node ─▶ Battle ─▶ Reward ─▶ Explore
                                        ├─ rest / info / event / trace / carving ─▶ Explore
                                        ├─ layer cleared / pushed out ─▶ Interlude ─▶ next layer
                                        └─ life ends (dead / miasma / survived / cleared) ─▶ Ending card ─▶ 継承の間
```

- The integrator owns the router; scenes are mounted into the shared Stage and call back (`onDone(result)`), never navigate themselves.
- Save: one JSON blob in `localStorage` (guarded by try/catch); a reload resumes where the game was closed, mid-battle included (tier1 R1-16, 2026-10-12 #462 — newer than the Unity build, which restarts the fight). A death, an end of life and a taken trace are saved the moment they are decided.
- Test switches (never in the shipped URL): `?debug` exposes `window.__ocb`; `?autobattle` fights every battle unattended with `CoreBattleSource.AutoCardId` (a JS extension: the C#'s `SuggestedCardId` choice whether or not the source was built to suggest, so a fight that a reload brings back, recorded on a page without the switch, is fought too and not only ended turn by turn).
- Build: `node build.mjs [--dev]` writes `dist/` — `index.html` opens with a doctype, `<meta charset="utf-8">` and the viewport (the Artifact's publish wraps the page in its own skeleton, where they are ignored; served as plain files they keep the page in standards mode and UTF-8); `--dev` adds `dev.html`, a full document for Playwright runs.
- Settings (Esc, and the gear — in a fight too): battle speed ×1.0/×1.25/×1.5, 演出短縮, volume, mute, and 「保存して閉じる」 wherever there is something to save: on the map and in a fight (saved move by move, so つづきから comes back to the same moment), and as 「保存してタイトルへ」 in 継承の間 / 出立 (the lineage and 出立's draft are already kept). A held key opens or closes an overlay once (repeats are ignored by the overlays and by the keys that open them).

## Scene contracts

All scenes: `export function mountX(host, ctx) → { destroy(), refresh?() }`. `host` is a 1920×1080 element inside the Stage. `ctx` always has `{ stage, audio, settings }` plus the scene's own inputs and callbacks. Coordinates are stage px (use `ctx.stage.toStage(e)` for pointer events). The router is `src/app/main.js`.

- `mountBattleScene(host, ctx)` — ctx `{ source, layer, nodeKind, chain: { index, total }, disclosureOf(enemyId), openJournal(host, opts) → { destroy }, onSettings(), onFinished() }`. `source` is the ported `CoreBattleSource` of one fight (from `EncounterBattle.StartNext(false)`), wrapped by the save recorder: every state-changing call (`TryPlay`, `EndTurn`, `Take`, `AdvanceAuto`, `Next`) must go through `ctx.source` (never keep a raw reference), read-only calls are free. The scene plays the source's depiction script like Unity's `DepictionPlayer`, and calls `onFinished()` once `source.Finished` and its result card has been seen. The app then calls `EncounterBattle.Finish(source)`; `Expedition.Return()` is the explore scene's.
- `mountExploreScene(host, ctx)` — ctx `{ expedition, inheritance: { traceView(layer), take(picks) → Taken & { Result } | null, scribbles() }, onBattle() → Promise, onLifeEnd(ending), onOverlay(kind), onSaveQuit(), onPersist(), onCarving() }`. Renders `ExplorationScreenBuilder` output, sends steps to `expedition.Session` (recorded), calls `expedition.Return()` / `Learn(id)` after a battle.
- Meta scenes (`src/scenes/meta/index.js`): `mountTitle`, `mountHall`, `mountDeparture`, `mountEnding`, `mountSettings` (overlay), `mountOwnedCards` (overlay), `mountJournal` (drawer, also used in battle).
- Save by replay (`src/app/recorder.js`): a life is its start (`src/app/life.js` `makeStart`) plus the ordered calls on Expedition / Session / EncounterBattle / CoreBattleSource; loading replays them, an unfinished fight included (it resumes from where it stood; `recorder.settleWhenDecided` saves a fight's outcome the moment the core decides it). Only a fight whose source threw is dropped (`trimUnfinished`), and the node starts it over.

## Visual rules (from the design system README)

- Tokens only (`var(--…)` from tokens.css); per-layer backdrop values come from the ported `BackdropSpec` (battle-visual §2.4/§2.5).
- Colour by role; text under 24 px is `--ink`; red `--reach-red` only for floor hatching and swatches; warm colours only on the player's side.
- Fonts: Shippori Mincho (display), Zen Kaku Gothic New (body), IBM Plex Mono (numbers).
- 1920×1080 coordinate system, scaled to the window, letterboxed with `--ground`.
- Japanese UI text, no emoji, fixed labels ≤ 10 characters.
- CSS class prefix per scene: `ocbg-battle-`, `ocbg-explore-`, `ocbg-meta-`; scope every selector under the scene root.
