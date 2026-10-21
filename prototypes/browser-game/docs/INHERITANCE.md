# 生・死・継承 — the inheritance rules of the browser game

Module: `src/core/inheritance/Inheritance.js` · tests: `test/inheritance/Inheritance.test.mjs` (one or more tests per rule, named `R-nn`); the game's side of R-11 / R-13 / R-14 / R-27 (what a trace and a survivor's journal give the heir, as the screens show it): `test/app/inherit-gains.test.mjs`, `test/app/survivor.test.mjs`.

> **Designed from the documents, not ported.** There is no C# for inheritance yet: in `unity-port/` the rules stop at `RunPhase.Survived` / `Fallen` / `MiasmaDeath` / `Completed` (`DungeonCore/ExplorationState.cs`), `NodeKind.Trace` (`DungeonCore/NodeKind.cs`, "whether a 死亡地点の痕跡 joins [the fights] is #106's"), `RunLoadout.SurvivorBonusToolSlots` (`DungeonCore/RunLoadout.cs`) and `MasteryBook` ("leaving a card behind at death and taking it up in the next life are the dungeon side's", `BattleCore/Mastery.cs`). The repo issues that will own the C# are #104 (出立), #106 (痕跡の中身) and #108 (生存者). Every rule below names its source; where the documents are silent or disagree, the choice is listed under **試作で決めたこと** with its reason. When the C# lands, it wins and this module is replaced by its port.

Sources are under `.claude/docs/` unless a path says otherwise. "concept" = `vision/concept-v3.md`, "tier1" = `requirements/tier1-core.md`, "explore" = `danjeon_document/dungeon_exploration_v4.md`, "layers" = `danjeon_document/seven_layers_v4.md`, "rewards" = `danjeon_document/encounters_and_rewards_v4.md`, "roster" = `enemy_document/enemy_roster_v4.md`, "camp" = `camp_document/CAMP_FACILITIES_DESIGN.md`.

---

## 1. The rules as implemented

| # | Rule | Source |
| --- | --- | --- |
| R-01 | A lineage is a sequence of lives with no lives counter and no full reset. It keeps the closed lives, the life in progress, the untaken traces, the one survivor, and the player's own knowledge (`Player.CarvingTaken`, `Player.RootDefeated`). | concept §8.1 「旧ライフ制は廃止…全リセットは存在しない」, §10; tier1 R1-16 「世代の記録（未回収の痕跡一覧・直近の生存者）」 |
| R-02 | Every heir is a different person with a personal name; the role (踏破者), the look and the class are the same for all. The hall can name the next heir before the life starts. | concept §1 主人公の扱い, §14 主人公 / 名前, §16.1 「名前は個人のもの、姿は役のもの」 |
| R-03 | A new life starts with 3 tool slots, 3 consumable slots and a deck of 20〜40 (raised by R-14). Only one life is in progress at a time. `startLife` returns the empty `RunLoadout` with the right slot count, the deck bounds, the journal the heir starts with and the layers that hold a trace. | concept §14 ツールの枠 / 資源・アイテム / デッキ枚数; `RunLoadout.ToolSlots`, `RunLoadout.ConsumableSlots`; `Constants.DeckMin` / `DeckMax` |
| R-04 | A death (HP 0 → `Fallen`, gauge 100% → `MiasmaDeath`) closes the life and leaves a trace at the place of death (layer + node), carrying the journal. Its candidates are **every card at 習熟 1 段 or more and every filled page**: enemy pages with 開示度 ≥ 1, the rare dungeon pages that have something written on them (none yet: 試作で決めたこと 9), and the memo as 「走り書き」. A card keeps its 段. The page of terms and statuses is never a candidate. | concept §5 「手記はそのキャラクターの死亡地点に痕跡として残る」, §8.1, §8.3, §14 遺産の候補 / 継ぐ札の段; tier1 R1-10 (走り書き, 用語と状態の頁), R1-11 「死亡地点（階層・ノード）が保存される」, R1-12; `requirements/tier2-support.md` R2-1 「遺産の痕跡が階層・ノードで一意に指せる」 |
| R-05 | Shallow deaths: a card at 2 段 or more stays as legacy only when the heir died on layer 3 or deeper. Kept-out cards are listed (`Withheld`) so the hall and the ending card can say why. Pages stay from any layer. | concept §8.4, §14 浅層死亡の閾値 「段 × 階層。2 段目以上のカードは階層 3 以上で死んだときだけ遺産になる」; tier1 R1-12 「浅層で死んだ高習熟カードが候補に出ない」 |
| R-06 | Traces stay until collected. A later death adds a trace and never overwrites an older one. The hall shows every untaken trace. | concept §8.3 (2026-09-12 「痕跡は回収されるまで残る」, 「継承の間では未回収の痕跡が全て見える」), §12-21; tier1 R1-12 「未回収の痕跡が次の死亡で消えない」 |
| R-07 | A layer that holds a trace shows one trace node, traded for one normal battle; a layer holds at most one. `mapSpecFor(lineage, profile)` returns `profile.MapSpec.With(NodeKind.Trace, 1)` for such a layer. | `DungeonCore/NodeKind.cs` (Trace: 「a layer holds at most one」); layers §4 「通常戦闘 1 つと入れ替えます」; rewards §2.1; explore §5-5 (#97: ノードとして置く); `LayerMapSpec.With` |
| R-08 | At the trace node the heir picks **up to 3** of its candidates, in any mix of cards and pages. | concept §8.3 「限られた枠（例: 3 つまで）から数個だけ選ぶ」; tier1 R1-12 「（仮 3）」「3 つまで選べる」 |
| R-09 | **One trace per life**: the first one visited. After it, no trace node appears for the rest of that life and a second take is refused; the other traces stay for later lives. | concept §8.3 「1 つの生で受け取れる遺産は 1 件（最初に訪れた痕跡）」; tier1 R1-12 「1 つの生で 2 件目の痕跡を訪れても受け取れない」; explore §6.3 痕跡 row; `NodeKind.cs` |
| R-10 | Visiting collects the trace: it leaves the list, the dead life records who took it, and what was not chosen is lost. | concept §5 「選ばなかった頁と資源は失われる。手記を丸ごと継ぐことはできない」, §9 table |
| R-11 | What was taken comes back as data for the life: cards with their 段 (to seed the mastery book and the cards owned) and the pages and 走り書き as journal additions. The choice is made at the place of death only (the take is refused when the heir is not on the trace's layer). The life's one-card limit on step 3 still holds (`mergeMastery`). The 痕跡 card and the 受け取った遺産 card print each row as **this heir** holds it, not with the dead heir's numbers (`src/bridge/exploration/TraceGain.js`, EXTENSIONS X-11): a card at the 段 and name it ends with after `mergeMastery` (a 3 段 card stopped at 2 says so), an enemy page at the 開示度 the journal keeps (only what goes above what it already knows: a page it knows as well or better adds nothing), a 走り書き the life already carries adds nothing; the receipt is written from what the take returned (`Expedition.prototype.Inherit`'s `{ Cards, Enemies, Pages }`). | concept §8.3 「選択は死亡地点で行う」, §14 継ぐ札の段 / 1 つの生の段数; `BattleCore/Mastery.cs` (`Mastery.TopRank`, `MasteryBook` Rerank) |
| R-12 | 継承の間 only shows: past lives, every untaken trace with its candidates and kept-out cards, the survivor, the next heir. Nothing in the hall takes a trace. | concept §8.3; camp §3.1 「候補の一覧を見るだけ」 |
| R-13 | 生存ルート (`Survived`, 「この生を終える」) passes **the whole journal** to the next life. After a death the next life's journal is empty apart from what it later takes at a trace. The survivor's memo becomes one 走り書き of the survivor's (`heirJournal`, 試作で決めたこと 16), so the heir starts on an empty sheet of their own. | concept §8.2 「生存ルートでは手記を丸ごと受け継ぐ」, §16.2; explore §6.6; tier1 R1-10 「死亡後に開始した次の生の手記は、遺産で選んだ頁以外が空である」, R1-13 「前の手記が全頁そのまま読める」 |
| R-14 | 生存者ボーナス: tool slots +1 (3 → 4, `RunLoadout.SurvivorBonusToolSlots`) and deck cap +5 (40 → 45), for **the next life only**. Once the life that used it has closed with no new survivor, the hall says so where the survivor stood (`hallView(...).SurvivorSpent`: 「第 N 代 〇〇の生でボーナスを使い終えた」「ツール枠 3 ／ デッキ上限 40 に戻った」), so the block does not vanish without a word. | concept §14 生存者ボーナス 「ツール枠 +1、デッキ上限 +5 を次の 1 生だけ」, §12-12; `RunLoadout.cs`, `DungeonContent/Loadout.cs` (`SurvivorToolSlots`) |
| R-15 | **One survivor at most**: a new survivor replaces the previous one, and whatever the previous one still had to give is dropped. | concept §8.2 「生存キャラクターは一人まで…前世代の生存者を蓄積できない」; tier1 R1-13 「生存者を 2 回続けて出しても次の生に効くのは直近 1 人だけ」 |
| R-16 | The dead heir's legacy and the survivor's bonus apply together. | concept §8.2 (1) と (2) の両方; tier1 R1-13 「遺産と生存者ボーナスが同時に適用される」 |
| R-17 | A won extra boss stretches the survivor bonus to two lives (`opts.ExtraBossWon`). | concept §15.2 報酬; roster §7.4 |
| R-18 | 踏破 (`Completed`: 歪みの根 defeated) closes the life alive as the survivor (R-13〜R-15) and records `Player.RootDefeated` (the out-of-game unlock of 根の裂け目). It also leaves a card trace on layer 7 — see 試作で決めたこと 10. | concept §12-15, §15.1, §15.2 「記録はプレイヤー（ゲーム外）に残す」; roster §6.6, §7.1, §7.3 |
| R-19 | The heir's 装束 (面の縁飾り・マントの裏地・留め具) is set by **the previous life's** reached layer; the outline never changes. | concept §14 世代差 「前の生の到達階層で決まる」, §16.1, §16.3; `art_document/briefs/player_swordsman.md` §3 |
| R-20 | Reading the layer-2 carving is the player's knowledge: it survives deaths, layer 2 shows 竜神の秘跡 from then on, and the carving node is no longer offered (`mapSpecFor` trades it back for a battle). | layers §5 「生をまたぐ。一度拾ったら次の生でも 竜神の秘跡 と表示し、ノードは出ない」; world-v1 §5.1 |
| R-21 | The lineage is plain JSON (`serialize` / `deserialize`, `toJSON` / `fromJSON`, checked on load). The game saves right after `recordDeath` / `recordSurvival` / `takeTrace`, so a reload neither revives the dead nor lets a trace be picked again. | tier1 R1-16 「死亡と生の終了は確定と同時に保存し、リロードで遺産を選び直せないようにする」, its criteria |
| R-22 | Pure: no DOM, clock or `Math.random`; every lineage is deep-frozen and every function returns a new one. | `docs/ARCH.md` (core computes, scenes render); `docs/CONVENTIONS.md` |
| R-23 | Deterministic: the same calls give the same lineage byte for byte (names and default life seeds come from the lineage seed through `SeededRng`). | `DungeonCore/IRng.cs` (SplitMix64, "a life replays from one number") |
| R-24 | Ending card: the end sentence (the same words as `ExplorationScreenBuilder`'s end cards), then what stays (trace, candidate counts, kept-out cards) or what passes (whole journal, bonus), then the next heir. | explore §6.9; `unity-project-kit/Assets/View/Exploration/Script/ExplorationScreenBuilder.cs` (EndCard lines) |
| R-25 | Hall and trace-node card texts; fixed labels within 10 characters. | camp §3.1; explore §6.3 (痕跡 「前の生の遺産を受け取れること（1 つの生で 1 件）」); `docs/ARCH.md` visual rules |
| R-26 | A finished `ExplorationState` closes the life directly: `placeFromState(state)` gives layer, node, node kind and `Phase`; `recordEnd` sends deaths to `recordDeath` and survivals to `recordSurvival`. | `DungeonCore/ExplorationReducer.cs` (`EndLife` → `RunPhase.Survived`, `ReportBattle` → `Fallen`, Resolve → `MiasmaDeath` / `Completed`) |
| R-27 | **Inherited pages are not found pages.** The rare pages a survivor's journal (R-13) or a trace (R-11) hands down are the life's — they count in `Expedition.Pages` and in its journal at the end (a trace leaves only filled pages, 試作で決めたこと 9) — but they were not picked up in this life: `Expedition.InheritedPages` counts them (`FoundPages` is the rest). The map's carried panel lists them under 「受け継いだ物」, apart from 「拾った物」, and the journal's ダンジョン tab has a 「希少な頁」 page (`rarePagesPage`) with 受け継いだ頁 and この生で拾った頁, which says that the pages have no content and no effect yet and that an empty page is no legacy pick. The reward card, the event's result and the map's page chips say the same in one line (`SceneText.PagesNote`). | concept §5 (the journal is the character's; what is inherited is chosen at the place of death), §8.2 「手記を丸ごと受け継ぐ」; tier1 R1-13 「前の手記が全頁そのまま読める」; explore §6.7 (the journal's tabs) |

Rule numbers (`InheritanceRules`): `LegacyPicks` 3, `CandidateMinRank` 1, `DeepRank` 2, `DeepLayer` 3, `SurvivorToolSlots` 4, `SurvivorDeckBonus` 5, `SurvivorBonusLives` 1, `ExtraBossBonusLives` 2, `MaxDisclosure` 2, `TierByLayer`, `ClearedTier`.

---

## 2. 試作で決めたこと

| # | Decision | Reason |
| --- | --- | --- |
| 1 | **The heirs' names**: a pool of 40 katakana personal names (`HeirNames`), shuffled per lineage from its seed; no repeat inside one pass over the pool, and a new pass never opens with the last name. 「第 N 代 踏破者 ＜名前＞」. | No document gives names. Katakana matches the lair's named bosses (world-v1 §7.1); ガルド and セルク are not reused. Seeded so the hall can name the next heir before the life starts. |
| 2 | **Five 装束 tiers** (`AttireTiers`): 縁なし / 鉄の縁 / 銀の縁 / 白銀の縁 / 灯芯の留め. Previous life reached layer 1 → 0, 2〜3 → 1, 4〜5 → 2, 6〜7 → 3, 歪みの根 defeated → 4; the first heir wears tier 0. Linings stay cool and achromatic, the warm colours (琥珀, 灯芯) sit on the clasps only. | concept §16.3 names the three parts that change but no steps; the brief defers the spec. The thresholds follow the real bosses (セルク on 3, ガルド on 6, the root on 7). Colours from world-v1 §6 and the style guide's rule that the player's 装束 carries no warm colour except light and clasps (§14.1). |
| 3 | The tier follows **the previous life only**, not the best life so far: a shallow death after a deep one sends the next heir back down. | concept §14 says 「前の生の到達階層で決まる」 literally. |
| 4 | Several untaken traces on one layer: **the oldest** shows; the others wait (the hall says 「同じ層の古い痕跡を回収すると現れる」). | `NodeKind.cs` allows one per layer; the documents do not say which. Oldest first lets nothing wait forever. |
| 5 | **Visiting collects the trace even with zero picks**, and uses the life's one trace (「何も受け取らない」 is allowed). | concept §8.3 「最初に訪れた痕跡」 + §5 「選ばなかった頁と資源は失われる」. |
| 6 | An inherited card keeps its 段; its ticks restart at **the threshold of that 段** (3 / 8 / 15, or the profile's, e.g. 型替え 2 / 6 / 12); progress toward the next 段 is not inherited. | concept §14 「段をそのまま継ぐ」 names the step only. |
| 7 | An inherited 3 段 card stops at 2 段 when the life already holds a 3 段 card. | Keeps `MasteryBook`'s one-card rule on step 3 (concept §14 1 つの生の段数). A single trace never holds two 3 段 cards. |
| 8 | The memo becomes one 「走り書き（第 N 代 名前）」 candidate; 走り書き inherited earlier stay separate candidates; an inherited 走り書き never overwrites the heir's own memo. | tier1 R1-10 calls the memo candidate 走り書き; §14 「埋まった頁を全部並べる」. |
| 9 | Rare dungeon pages are identified by id; a count (what `Expedition.Pages` gives today) becomes `page-1 … page-n` titled 「希少な頁 n」. **A page is a legacy candidate only when something is written on it** (`Text`, `isFilledPage`): today no page has content, so none takes one of the 3 picks, and the hall's and the ending card's 「頁 n」 count enemy pages only. A trace saved before this rule may still hold empty page candidates: the hall and the trace card leave them out, and a logged take of one still replays. Pages still pass whole with a survivor's journal (R-13). | Page contents are #107's. concept §14 / §12-10 list 「埋まった頁」 — an empty page is not filled, and a pick spent on one gave the heir nothing (review: seven identical 「希少な頁 n」 rows competing with cards for the 3 picks). |
| 10 | **踏破 also leaves a card trace on layer 7**: the life's cards at 1 段 or more plus 歪みの根's cards at any 段 (`opts.RootCards`), no pages (the journal already passed whole). | roster §6.6 and rewards §4.2 say 歪みの根's cards are used only 「遺産として継いだ次の生」, roster §7.4 lists the extra boss's cards as 遺産の候補, while §15.2 / roster §7.3 close that life alive — and a survivor passes no cards (concept §8.2). A trace at the root's place is the one path that fits all of them. |
| 11 | The survivor's journal goes to **the immediately next life only**; with the extra boss the bonus slots last two lives but the journal passes once. | §8.2 / §16.2 tie the journal to the role being handed back; §15.2 stretches only the bonus. |
| 12 | The trace node **holds no fight**. | `NodeKind.IsCombat(Trace)` is false in the C#; layers §2.4 lists field cells for a trace as if it held one. The C# wins until #106 decides. |
| 13 | The hall and the ending card show **the layer and the kind of node** of the death (e.g. 「第 3 層　大黒蛇の住処（階層ボス）」), not the node id. The id is stored. | Each life's map is drawn again from its seed, so a node id means nothing to the next heir; R2-1 needs it only as data. |
| 14 | A life started without a seed draws one from the lineage seed. | Replays from one number, like `ExplorationSession.LayerSeed`. |
| 15 | Ending-card and hall sentences beyond the four end sentences (「〜に痕跡と手記が残った」, 「遺産の候補：技術 n ／ 頁 n ／ 走り書き n」 …) are this module's wording. | No document fixes them. |
| 16 | **A survivor's memo becomes that survivor's 走り書き in the heir's journal** (`heirJournal(Journal, JournalFrom)`, saved in the life's start), and the heir's own memo starts empty. | 試作で決めたこと 8 says an inherited 走り書き never overwrites the heir's own memo; R-13 passes the whole journal. Moving the memo to a named 走り書き keeps both: the survivor's words stay readable (メモ tab, 受け継いだ走り書き) and become a candidate of their own if this heir dies. |

---

## 3. How the game calls it

```
hallView(lineage)                         → 継承の間 (look only)
startLife(lineage, seed?)                 → { Lineage, Life, Seed, ToolSlots, DeckMin, DeckMax, Loadout: RunLoadout, Journal, TraceLayers, CarvingKnown, Attire }
  出立: honour ToolSlots (4 with the bonus) and DeckMax (45 with the bonus); save heirJournal(Journal, JournalFrom) in the
    life's start: its 開示度 go to Expedition's journal, its pages to Expedition's page count as inherited pages
    (InheritedPages, R-27), its 走り書き (the survivor's memo among them) to the router's list.
each layer: MapGenerator.Generate(mapSpecFor(lineage, profile), ExplorationSession.LayerSeed(runSeed, layer))
on a Trace node: traceNodeView(lineage, layer) → player picks (pickState) → takeTrace(lineage, traceId, picks, { Layer })
  → save; Expedition.prototype.Inherit(Taken) (src/app/life.js): Taken.Cards → cards owned + mergeMastery(book.Records,
    cards, (id, t, r) => new CardMastery(id, t, r)); Taken.Journal → the journal keeps the higher 開示度, the pages count
    as inherited; it returns what it did ({ Cards: [{ CardId, Rank, From, To, Joins, … }], Enemies: [{ EnemyId, From, To }],
    Pages }), which the 受け取った遺産 card prints (TraceGain); the router adds Taken.Journal.Scribbles to its list, each once
on the carving: noteCarving(lineage) (or pass CarvingTaken: true in the journal at the end)
life over: recordEnd(lineage, life, placeFromState(state), masteryBook, journal, { cardName, enemyName, RootCards, ExtraBossWon })
  → save at once → endingView(lineage) → hallView
```

`deck` is a `MasteryBook` (its `Records`; `ProfileOf` gives thresholds and 段 names when present) or a list of `{ CardId, Rank, Name? }`. `journal` is `{ Disclosure: Map|object, Pages: count|ids|{Id,Title}[], Memo, Scribbles?, CarvingTaken? }`. `opts.cardName(cardId, rank)` and `opts.enemyName(enemyId)` store display names in the trace (the save then needs no catalogue to draw the hall).

---

## 4. Open points for the integrator

- `ExplorationSession` (port of the C#) draws each layer's map from `profile.Map(seed)` and its `NewLife` action restarts with `seed + 1`. The lineage flow needs the map spec from `mapSpecFor` and must start lives through `startLife`, not `NewLife`.
- `DeckRules` / `DepartureBuilder` use the fixed `ToolSlots` 3 and `DeckMax` 40; the survivor's 4 / 45 come only from `LifeStart`.
- `Expedition` keeps only 開示度 by enemy and a page count; the memo and 走り書き have no home in the ported C# yet (#107).
- `Mastery.js`, `CardCatalog.js` and `Enemies.js` were not in `src/` when this was written, so names come in through `opts.cardName` / `opts.enemyName` and `mergeMastery` takes a record factory instead of importing `CardMastery`.
- `ExplorationReducer` sets `Completed` when the last boss node is entered; record the end only after the battle returns (a lost root fight turns it into `Fallen`).
