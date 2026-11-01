# rules — JS の試遊版にあって現行 C# に無い規則・文言・データと、その移植計画

この文書は、ブラウザ試遊版（以下 JS）の規則まわりを現行 C#（`unity-port/`）と突き合わせた結果です。結論を先に書きます。

- JS の規則は C# の 1:1 移植に、設計書から起こした opt-in の拡張を足したものです。拡張は大きく 7 群あります（出立の枠、探索の持ち物とイベント、探索の結果と再開、手記の敵の頁と地図の敵情報、継承、複数の敵の描写、俊敏の空きマス）。
- C# は移植元 `6289a9bd` から `f120ab9d` までに、規則では `EncounterBattle.cs` だけが進んでいます（開示度の欠けを例外にする、`TryFinish` の追加）。他の規則ファイルは差分ゼロです。
- JS は C# の不具合を 2 つ直しています。2 体以上の敵で `CoreScriptWriter` が `KeyNotFoundException` を投げる不具合と、`CoreBattleSource` が狙う敵を渡さないために先頭の敵が倒れると戦闘に勝てない不具合です。
- 移植は 5 単位に分けると、互いに同じファイルを触らずに並行できます。U1〜U4 は互いに独立で、U5 だけが U1・U2・U4 の公開 API に依存します。契約（シグネチャ）は §6.5 に書きました。

パスの略記は次のとおりです。

- `JS/` = `C:\Users\user\AppData\Local\Temp\claude\C--Users-user-orca-original-card-battle\1ab86662-d6cc-4831-aa8c-76f9592e0ca6\scratchpad\proto\prototypes\browser-game\`
- `UP/` = `C:\Users\user\orca\workspaces\original-card-battle\trial\unity-port\`
- `V/` = `UP/unity-project-kit/Assets/View/`
- 設計書の出典は `.claude/docs/` 以下の相対パスです（`concept-v3` = `vision/concept-v3.md`、`tools` = `danjeon_document/tools_and_consumables_v4.md`、`rewards` = `danjeon_document/encounters_and_rewards_v4.md`、`explore` = `danjeon_document/dungeon_exploration_v4.md`、`layers` = `danjeon_document/seven_layers_v4.md`、`roster` = `enemy_document/enemy_roster_v4.md`、`ui_ux` = `battle_document/battle_ui_ux_v2.md`、`visual` = `art_document/battle-visual-v1.md`）。

どちらが勝つかの原則は次のとおりです。規則は C# が勝ちます。JS にしかない規則は、既定値で今の C# と同じ動きになる opt-in として C# に移し、試遊版だけが有効にします。文言と見た目は JS が勝ちます。

---

## 0. 調べ方

- JS 側の名前のうち、対応する C# ファイルに一度も出てこないものを機械的に抜きました（`scratchpad/rules-tools/jsonly.mjs`。型対応は `JS/docs/TYPE_MAP.md`）。逆向き（C# にあって JS に無い public メンバー）も抜きました（`scratchpad/rules-tools/csonly.mjs`）。逆向きで見つかった規則メンバーは `EncounterBattle.TryFinish` だけです。
- そのうえで、拡張を含む JS ファイルは全文を読み、C# の該当箇所と行単位で比べました。拡張の仕様書は `JS/docs/EXTENSIONS.md`（W / J / L / X 番号）、`JS/docs/INHERITANCE.md`（R 番号）、`JS/docs/port-notes/depiction-multi-enemy.md` です。
- C# の変化は `git -C trial diff 6289a9bd f120ab9d --stat -- unity-port` で確かめました。trial worktree の HEAD は `f120ab9d` で、未コミットの変更はありません。

---

## 1. C# に対応ファイルが無い JS モジュール

### 1.1 新規に移植が要るもの

| JS ファイル（行数） | 何をするか | 出典 | C# の置き場（§6） |
| --- | --- | --- | --- |
| `JS/src/core/inheritance/Inheritance.js`（1265） | 系譜（生・死・継承）の規則一式です。系譜の生成、次の継承者の名前と装束、`startLife`（生存者ボーナスで ツール 4 枠・デッキ上限 45）、`recordDeath` / `recordSurvival` / `recordEnd`（痕跡と候補、浅層死の除外）、`takeTrace`（3 つまで選ぶ、1 生 1 件）、`mapSpecFor`（痕跡ノードを 1 つ置き、秘跡の彫りを外す）、`mergeMastery` / `mergeJournal` / `heirJournal`、継承の間・痕跡カード・終わりのカードの表示データ、JSON の保存と検査です。 | concept-v3 §1 / §5 / §8 / §12 / §14 / §15 / §16、tier1 R1-10〜R1-16、layers §4 / §5、roster §6.6 / §7.4、`JS/docs/INHERITANCE.md` R-01〜R-27 と 試作で決めたこと 1〜16 | 新アセンブリ `InheritanceCore`（U4） |
| `JS/src/core/dungeon/LayerEvents.js`（126） | イベントノードの中身（泉 / 重い宝箱 / 特殊な食事）を、層ごとの重み 50/20/30・40/30/30・30/40/30・20/50/30・10/60/30・0/70/30・0/70/30 で引きます。同じ層では同じ種類を重ねません。ノードごとの種で引くので、再生で同じになります。泉の回復 20/25/30/30/30/30/30%、宝箱の中身（1 層 カード+消耗品、2 層 カード+頁、3 層以降 カード+頁+消耗品）、`ChestStaminaCost` 2、`MealStaminaGain` 1、`MealHpPercent` 10 を持ちます。 | rewards §5、explore §2.1、EXTENSIONS W-11〜W-15、試作で決めたこと 8 | `UP/DungeonCore/LayerEvents.cs`（U2） |
| `JS/src/bridge/explorationScript/TrialItems.js`（42） | カタログの道具を、探索コアが読む `ReliefItem` に変えます。`ItemEffect` から `ReliefKind` への対応表です（MiasmaDensity→Density、MiasmaGauge→Gauge、TimeLimit→Time、ChooseSurveyTarget→Survey、InterludeHeal→Interlude、HealHp→Heal、MaxStamina→MaxStamina、RefillStamina→Refill、Teleport→Teleport、それ以外→Battle）。 | tools §2 / §3、EXTENSIONS W-2 | `V/Exploration/Script/TrialItems.cs`（U2） |
| `JS/src/bridge/exploration/Loadouts.js`（45） | 出立の `Loadout`（カタログの `ItemDef`）を、試遊用の `RunLoadout`（trial=true、生のツール枠数つき）に変えます。保存用の行データとの相互変換（`runLoadoutToData` / `runLoadoutFromData`）も持ちます。 | `UP/DungeonContent/Loadout.cs` の「#104 is where a chosen Loadout becomes a RunLoadout」、W-2 | `V/Exploration/Bridge/Loadouts.cs`（U5） |
| `JS/src/bridge/exploration/MasteryView.js`（45） | 札の習熟を探索画面が読む形にします。`nameAtRank`（段ごとの改名）と `masteryProgress`（Rank / Ticks / Need / Grows / Top / Capped / Name / BaseName / NextName）です。 | explore §2.1 習熟訓練、battle_core_v4 習熟、EXTENSIONS X-15 | `V/Exploration/Bridge/MasteryView.cs`（U5） |
| `JS/src/bridge/exploration/TraceGain.js`（219） | 痕跡の候補が「この継承者に」何を与えるかを書きます。`inheritPreview`（実際の受け取りと同じ手順の試算）、`pagePreview`、`traceGains`（候補ごとの Name / Detail / Notes / Nothing）、`deckIsFull`、`cardNameAt`、`candidateCard` / `candidatePage`、文言 `TraceGainText`（「いまも 2 段目」「3 段目の札がこの生にあるので 2 段目止まり」「所持カードにだけ入る」など 20 件）です。 | INHERITANCE R-10 / R-11、試作 7、rewards §4.3、EXTENSIONS X-11 | `V/Exploration/Bridge/TraceGain.cs`（U5） |
| `JS/src/bridge/journal/EnemyPages.js`（332） | 手記の敵の頁です。13 体ぶんの文言（roster §1.1 の journal、適応条件の条件列、構え・崩しの行、ボス専用状態）をデータとして持ち、開示度ごとの行（0: 名前・HP 最大、1: 得意な間合い・間合い 0 / 1〜2 / 3 以上、2: 弱点・特殊行動・適応条件）を組みます。`MemoRow`（戦闘中は末尾に「戦闘中は書けない」）、地図の `EncounterName`（「 + 」と「 → 」）、`TendencyLine` も持ちます。 | roster §1.1 / §1.2 / §1.5 / §2〜§6 / §9、ui_ux §2.4 / §7 / §11.3、explore §6.3 / §6.7、EXTENSIONS J-1〜J-9 | `V/Trial/Bridge/EnemyPages.cs`（U5）。`BattleCore.Enemies` を読むので `Journal.Script` には置けません（§6.1） |
| `JS/src/app/intel.js`（195） | `JournalIntel`（`IEnemyIntel` の実装）です。ノードの敵を `LayerEncounters.Pick` と同じ種で引き、開示度 1 から名前を、2 から傾向行を出します。情報収集の対象を「ボス → 精鋭 → 通常、ノード番号順、開示度 2 未満の最初の敵」で決め（`SurveyTarget`）、七層に入ったら歪みの根を開示度 2 にします（`KNOWN_ON_LAST_LAYER = ['distortion_root']`）。上げる処理はセッションの `Arrived` の中で走ります。 | explore §2.1 / §5 の 4 / §6.3 / §6.7 / §6.8、layers §4 / §9 の 2、EXTENSIONS J-7〜J-15、試作 6 / 10 / 12 | `V/Trial/Bridge/JournalIntel.cs`（U5） |
| `JS/src/app/life.js`（215） | 1 つの生と系譜の継ぎ目です。出立の選択を保存データにし（`choiceToData`、`makeStart`）、保存から `Expedition` を組み直します（`lifeSetup`：`MapFor` で痕跡ノードと秘跡の彫りを差し替え、`JournalIntel` を結び、`{ trial: true, limits }` を渡し、受け継いだ頁を数えます）。`Expedition.prototype.Inherit`（痕跡から受け取った札・段・開示度・頁を生に入れ、結果の行を返す）、`addScribbles`、`lifeScribbles`、`startPages` も持ちます。 | INHERITANCE §3、R-09 / R-11 / R-13 / R-27、EXTENSIONS L-2 / W-2 / J-14 | `Expedition.Inherit` は `V/Exploration/Bridge/Expedition.Trial.cs`、残りは `V/Trial/Bridge/Life.cs`（U5） |
| `JS/src/app/names.js`（43） | 生の終わりに系譜へ渡す手記（`lifeJournal`：開示度・頁・メモ・走り書き・秘跡）と、表示名（`cardName`：段の名、`enemyName`）、踏破時の歪みの根の札（`endOptions` の `RootCards` = `UniqueCards.OfBoss('distortion_root')`）を作ります。 | INHERITANCE R-18 / 試作 10 | `V/Trial/Bridge/Names.cs`（U5） |
| `JS/src/app/recorder.js`（172） | 再生による保存です。`Expedition`・`ExplorationSession`・`EncounterBattle`・`CoreBattleSource` への状態を変える呼び出しを `[tag, method, args, n]` で記録し（tag は E / S / B / C、引数なしの連続呼び出しは n にまとめる、ソース引数は `'$source'`）、読み込みで同じ順に呼び直します。`settleWhenDecided`（コアが勝敗を決めた瞬間に `Finish` して保存）、`trimUnfinished`（再生できない戦闘だけ最初からやり直す）も持ちます。読むだけの呼び出し（`READ_ONLY`：Screen, CanOpen, DisclosureOf, HandIn, PreviewFor, Inspect, RefusalText, HasPlayableCard, NextLaunch, TryDescend, MapOf, SyncLife, CanUseSlot, EventAt, CanTeleport, TargetFor, CanTakeFreeStep, ShownGapGlyph ほか）は記録しません。 | tier1 R1-16、#462、INHERITANCE R-21、`JS/docs/ARCH.md`「Save by replay」 | `V/Trial/Bridge/Recorder.cs`（U5）。C# には Proxy が無いので、記録用の薄い窓口クラスと、メソッド名で振り分ける再生器に置き換えます |
| `JS/src/app/save.js`（149） | ブラウザ保存です。キー `ocb.save.v1` / `ocb.settings.v1` / `ocb.save.v1.broken`、書き込みごとの `{ rev, tab }` 印（別タブの上書き防止）、読めない保存を退避してから扱うこと、設定の既定値 `speed 1.25, shortFx false, master 0.8, effects 0.9, ambience 0.6, muted false` を持ちます。 | tier1 R1-16、CAMP §3.5 | 直列化と既定値は `V/Trial/Bridge/SaveData.cs`（U5）。ファイルの読み書きは View 側（Trial の app 担当） |
| `JS/src/app/main.js`（524） | ルーターです。規則に当たる部分は、出立の所持カード = 初期 40 種 ∪ 試作デッキの種類（L-8）、痕跡の受け取りを生が終わった後は拒む、生の終わりは `IsOver` かつ探索中でないときだけ記録する、`?autobattle` は `AutoCardId` で戦う、の 4 点です。 | EXTENSIONS L-8 / X-3 | 規則の部分だけ `Life.cs` に移し、画面遷移は View 担当 |

### 1.2 JS 専用の補助（移植しない）

- `JS/src/shared/cs.js`（180）、`JS/src/core/dungeon/_errors.js`（58）：C# の例外文言や record 等値を JS で真似る補助です。
- `JS/src/bridge/backdrop/CrtPow.js`（382）：オラクル（Linux の glibc）の `Math.Pow` を最後の 1 ビットまで真似る補助です。C# では `Math.Pow` をそのまま使います。

### 1.3 C# に対応ファイルがあると確かめたもの

依頼文で候補に挙がっていた次のファイルは C# にあります。規則の差分はありません。

| JS | C# | 差分 |
| --- | --- | --- |
| `bridge/explorationScript/EnemyIntel.js` | `V/Exploration/Script/EnemyIntel.cs`（51） | ありません。試遊用に `ISurveyIntel` と `IArrivalIntel` を足します（§6） |
| `bridge/explorationScript/ExplorationText.js` | `V/Exploration/Script/ExplorationText.cs`（82） | JS が 230 行ほど文言を足しています（§2.3） |
| `bridge/journal/JournalTerms.js` | `V/Journal/Script/JournalTerms.cs`（73） | ありません。用語の頁に足す「スタンス」の行は画面側（`JS/src/scenes/meta/journal.js:101` `withStanceTerm`、文言は `scenes/meta/text.js:116`）です |
| `bridge/depiction/CardSystems.js` | `V/Depiction/Bridge/CardSystems.cs`（61） | ありません |
| `bridge/depiction/PileCards.js` | `V/Depiction/Bridge/PileCards.cs`（89） | ありません |
| `bridge/depiction/DemoEnding.js` | `V/Depiction/Bridge/DemoEnding.cs`（84） | ありません |
| `bridge/depiction/FloorCells.js` | `V/Depiction/Bridge/FloorCells.cs`（95） | ありません |
| `scenes/meta/DepartureSupplies.js`（67） | `V/Depiction/DepartureSupplies.cs`（50） | JS が `NewBuilder(limits)`（:35）と `Carry(departure, limits)`（:46）を足しています |

---

## 2. 移植済みモジュールの中に JS が足したもの

どれも「引数を省けば C# と同じ」の opt-in です。JS の行番号を添えます。

### 2.1 BattleCore：デッキの枠（DeckLimits、EXTENSIONS L-1〜L-9）

| JS の場所 | 追加内容 | C# の対応箇所 |
| --- | --- | --- |
| `JS/src/core/battle/DeckRules.js:240-325` | `DeckLimits` クラス（DeckMin / DeckMax / ToolSlots / ConsumableSlots、既定 20 / 40 / 3 / 3、`Default`、`From`、`IsDefault`、`With`、`Equals`）。整数でない値、DeckMin < 1、DeckMax < DeckMin、枠 < 0 は `ArgumentOutOfRangeException` です（L-9）。 | 新規 `UP/BattleCore/DeckLimits.cs` |
| `DeckRules.js:178` / `:196` / `:209` | `ValidateDeck` / `CanAdd` / `Validate` に末尾の `limits`。ツールの枠超えの文言は `"{n} tools do not fit {L.ToolSlots} slots."` のまま数字だけ変わります。 | `UP/BattleCore/DeckRules.cs:92` / `:113-118` / `:125-135` |
| `JS/src/core/battle/Cards.js:263-268` | `Cards.Validate(deck, limits = null)`。上限と下限を limits から読みます（`"Deck has {n} cards; the maximum is {max}."`）。 | `UP/BattleCore/Cards.cs:213-225` |

生存者ボーナスで上がるのはツール枠 +1 とデッキ上限 +5 だけで、消耗品 3 枠・下限 20・同名 3 枚は動きません（L-2、試作 1、出典 concept-v3 §14「ツール枠 +1、デッキ上限 +5 を次の 1 生だけ」）。

### 2.2 DungeonCore：試遊の探索規則（EXTENSIONS W-1〜W-19、X-7 / X-13）

| JS の場所 | 追加内容 | C# の対応箇所 |
| --- | --- | --- |
| `JS/src/core/dungeon/RunLoadout.js:12-26` | `ReliefKind` に `Time, Survey, Interlude, Battle, Heal, MaxStamina, Refill, Teleport` を既存 2 つの後ろに足します。 | `UP/DungeonCore/RunLoadout.cs:11-18` |
| `RunLoadout.js:63-84` | コンストラクタ末尾の `trial`。trial でなければ「ツールは Density だけ」の C# の検査、trial なら「ツールは Density / Time / Survey / Interlude / Battle のどれか」の検査（文言 `'a trial tool is one of the eight tools of tools_and_consumables_v4.md §2'`）です。 | `RunLoadout.cs:43-63` |
| `RunLoadout.js:92-126` | `DensityRelief`（trial なら Density の合計）、`TimeBonus`、`InterludeBonus`、`ChoosesSurvey`、`SumOf(kind)`、`HasFreeSlot`、`Take(item, replaceSlot = -1)`（空きがあれば後ろへ、満杯なら指定の枠と入れ替え）。複製は足し算です（試作 2）。 | `RunLoadout.cs:72` ほか新規メンバー |
| `RunLoadout.js:133-143` | `Spend` が使った `item` も返します。 | `RunLoadout.cs:75-86` に `out ReliefItem item` の多重定義を足す |
| `JS/src/core/dungeon/ExplorationState.js:59-92` / `:137` | `Trial` フラグ（コンストラクタ末尾と `With`）、`TimeLimit` = `Profile.TimeLimit + TimeBonus`、`TimeBonus`（trial のときだけ刻限の砂の合計）。 | `UP/DungeonCore/ExplorationState.cs` のコンストラクタ（13 引数）と `With` |
| `JS/src/core/dungeon/ExplorationReducer.js:45-69` | `Enter(..., trial = false)`。trial なら刻限に `TimeBonus` を足します。 | `UP/DungeonCore/ExplorationReducer.cs:39-71` |
| `ExplorationReducer.js:100-125` | `UseConsumable` の trial 分岐。薬草は最大 HP の Amount% 回復、濃い干し肉は一時的な最大スタミナ +Amount（±4 で切る）、忘れ水は現スタミナ満タン、浄化の香は C# と同じ、標の石は `Teleport` を使えと例外です。 | `ExplorationReducer.cs:105-115` |
| `ExplorationReducer.js:132-156` | `Teleport(state, index, nodeId, choice)` と `CanTeleport`。同じ層の未踏ノード（ボスも可、試作 3）へ跳び、刻限 1 と瘴気を払って普通に解決します。 | 新規（partial ファイル） |
| `ExplorationReducer.js:169-190` | `ResolveEvent(state, kind, accept)`。泉は回復、重い宝箱は一時的な最大スタミナ −2（現スタミナも下がる）、特殊な食事は一時的な最大スタミナ +1 と HP −10%（1 で止まる、試作 5）。受けないなら何も変わりません。 | 新規 |
| `ExplorationReducer.js:196-224` | `InterludeHealOf`、`SpringHeal`、`MealCost`、`TakeConsumable`、`InterludePercent`（30% + 灯芯の欠片）。 | 新規 |
| `ExplorationReducer.js:234-236` / `:270` | `Interlude` の回復量（trial なら 30% + 灯芯の欠片）、`Descend` が `state.Trial` を次の層へ渡すこと。 | `ExplorationReducer.cs:121-135` / `:144-160` |

### 2.3 Exploration.Script：試遊の探索画面（W / X / J の画面側）

**`JS/src/bridge/explorationScript/ExplorationSession.js`（767 行。C# は 370 行）**

| JS の場所 | 追加内容 |
| --- | --- |
| `:55` `RunStart.Trial`、`:60` `RunStart.CarvingKnown` | 試遊の切り替えと、秘跡の彫りを系譜が知っているか（null なら地図から推定） |
| `:503` `MapOf(profile, seed)` | `RunStart.MapFor` があればそれで地図を作る差し込み口（無ければ `profile.Map(seed)` で C# と同じ） |
| `:111-152` | 試遊の状態：`Keeper`（Expedition）、`PendingEvent`、`Events`（`EventOutcome[]`）、`PendingFound`、`SurveyChoices`、`SurveyTarget`、`SurveyPicked`、`AimingSlot`、`PendingJumpSlot`、`EventState`、`InterludeState`、`TrainingWaiting`、`_foundFrom` |
| `:160-179` | `HasPendingEvent` / `HasPendingFound` / `HasPendingSurvey` / `IsAiming`、`IsIdle` がこれらも見る |
| `:203` / `:224-242` / `:251-261` / `:268-281` | `Step` は照準中なら断る、`UseConsumable` は使っても何も変わらないなら断り、標の石なら照準を始める（もう一度で止める）、`CanUseSlot`（「変わる」の定義は `ChangeLines` が空でないこと、試作 1）、`Jump` |
| `:290` / `:298-312` | 訓練の刻みを待つ札：`SpendTrainingMark` で刻みが尽きたら解除、`OpenTraining`、`PutOffTraining`（X-10） |
| `:320-436` | `Act(action, arg)`：`TrialAction.AcceptEvent / DeclineEvent / TakeFound(slot) / LeaveFound / ChooseSurvey(enemyId)`、`AskEndLife` と `Cancel` は照準も止める、`ConfirmStep` は跳躍の確認も受ける、`GoToInterlude` は `InterludeState` を残す |
| `:480` | `Escape` は照準中も「やめる」として扱う |
| `:524` `EventSeed` | `EncounterSeed(...) ^ 0x9E6C63D0676A9A99`（W-18） |
| `:532` `LayerEnemies(layer)` | 写しの帳で選べる敵（その層の通常 → 精鋭 → ボスの表から重複なし） |
| `:545` `EventAt`、`:561` `Receive`、`:580` `TurnDownFind`、`:592` `ApplyJump`、`:602-648` `ResolveEvent` | イベントの種類を引く、拾った消耗品を枠へ入れる（満杯なら入れ替え待ち）、宝箱の拾い物を断ったら記録から消す（X-9）、跳躍の実行、イベント結果の行（結果カード用 `Lines` と手記用 `Record`） |
| `:658-668` `CarvingKnownAt` | 系譜が彫りを読んだかを、`RunStart.CarvingKnown` か地図（彫りの枠があるのに地図に無い層）から決める（X-5） |
| `:670-730` | `Begin` / `Descended` / `Apply` の試遊分（状態の初期化、層ごとの情報収集の解除、刻みの待ちの開始） |
| `:743-765` `Arrived` | イベントノードで泉は即解決、宝箱と食事は選択待ち。情報収集で写しの帳があれば候補を出し、無ければ `Intel.SurveyTarget` を聞いて Keeper に開示度 2 を書く（W-4、J-10） |

**`JS/src/bridge/explorationScript/ExplorationScreen.js`**：`TrialAction`（:52、5 値）、`TrialCardKind`（:75、`EventChoice / Swap / SurveyChoice`）、`StepOption.Jump`（:140）、`SlotCard.Forecast / Reason / Aiming / Id`（:256-263）、`CardButton.Arg / Note`（:275-278）、`ScreenCard.IsChoice`（:305）、`ToolCard`（:326）、`EventOutcome`（:342、`Summary` は `Record` を「・」でつなぐ）、`ExplorationScreen.ToolCards / TimeBonusLabel / ReliefLabel / Aiming / Events`（:455-464）。

**`JS/src/bridge/explorationScript/ExplorationScreenBuilder.js`（801 行。C# は 522 行）**

| JS の場所 | 追加内容 |
| --- | --- |
| `:141-149` | 休息の行を実際の回復量にし、上限で止まるなら「・上限まで」（X-13） |
| `:154-158`、`:604-612` `SurveyLine` | 情報収集済みの行を「済ませた。〇〇が開示度 2 になった」にする（X-16）、写しの帳の行 |
| `:161-164`、`:187-210` `TrialEventLines` | イベントノードの中身（隣に来るまで隠す、W-12） |
| `:166-171` | 痕跡を回収済みなら「この痕跡は回収した」 |
| `:216` `ArrivalEvent`、`:228` `JumpOptions`、`:242` `Options(..., event)` | 泉を予測に入れる、標の石の着地先と予測（遠いノードの泉は数えない） |
| `:270-273` | 階層間の休憩中の瘴気の予告を「降りると +（次の層の濃さ）%」にする（X-6） |
| `:287` `ReliefLabel`、`:383` `ToolCards`、`:396` `ToolNames`、`:411` `InterludeShare` | 防瘴の面の注記、ツールの一覧、灯芯の欠片の注記 |
| `:338` `TrialSlots`、`:373` `ChangeLines`（公開） | 消耗品の枠ごとの予測・使えない理由 |
| `:420-455` `TrialCard`、`:509-513` | 選択待ちの札（イベント、写しの帳、入れ替え）を確認札の後・層の終わりの前に出す |
| `:558-590` `InterludeCard`、`:760` 公開版 | 休憩の行は `InterludeState` を読む、次の層の刻限に刻限の砂を足す、`DisplayName` で層名 |
| `:631-664` `Book` | 濃さの行が下限で止まる注記（X-7）、「出来事」の行（W-17）、秘跡の彫りの頁（X-12）、刻限の行は `state.TimeLimit` |
| `:690-752` `Build` | 照準中のノードの選択肢、ヒント文、新しい欄 5 つ |
| `:769-774` `Forecast(..., jumpSlot, event)` | 跳躍の予測と泉の加算 |

**`JS/src/bridge/explorationScript/ExplorationText.js`（313 行。C# は 82 行）**：`DesignStatus` 正規表現 `。?[^。（）]*（[^（）]*凍結[^（）]*）`（:16）と `PlayerNote`（:97、X-14）、そしてイベント・枠・跳躍・入れ替え・ツール・写しの帳・秘跡の頁の文言 60 件ほど（:106-312）です。全文は JS ファイルをそのまま写してください。

### 2.4 Exploration.Bridge：Expedition と戦闘の受け渡し

**`JS/src/bridge/exploration/Expedition.js`（505 行。C# は 328 行）**

| JS の場所 | 追加内容 |
| --- | --- |
| `:55-66` | コンストラクタ末尾の `options = { limits, trial, carvingKnown }`。trial なら `RunStart` を Trial 付きで作り直す、デッキは `limits` で検査 |
| `:57`、`:181`、`:324`、`:412`、`:416` | `_limits` を `HandIn`（`BattleHandIn.Limits`）、`Take`（`DeckRules.CanAdd`）、`OfferFits`、`DeckMax` で使う |
| `:88-95`、`:147` | `InheritedTraceId` / `InheritedLayer` / `InheritedPages` / `FoundPages`（R-27） |
| `:105` | trial なら `Session.Keeper = this` |
| `:206-269` `Return` | 開示度が上がった敵（`Raised`）、札の山が尽きたか（`PileSpent`：持ち札なしで同じ種で振り直して判定）、デッキが満杯で入らなかった札（`Benched`）、拾った消耗品を `Session.Receive` へ、結果札を `PendingResult` に保持（X-1 / X-4） |
| `:300` `Learn` | trial なら結果札を閉じる |
| `:322-328` `Take` | デッキに入ったかを bool で返す（C# は void） |
| `:335-458` | `Events`、`TakeChest`（宝箱：札の山から 3 枚、尽きていれば消耗品、消耗品は層の重みで、頁を数える。種は `RewardSeed`、試作 6 / 7）、`PendingResult` / `CloseResult`（閉じたら宝箱の「カード 1 枚」を記録から消す）、`OfferFits`、`DeckMax`、`ShowTaken` / `PendingTaken` / `CloseTaken`（受け取った遺産の札、X-3）、`TakeSurvey`、`EnemyName` |
| `:478-480` | `BeginLife` で試遊の状態を消す |
| `JS/src/app/life.js:119-152` | `Expedition.prototype.Inherit(taken)`（R-11）。札は所持に入れ、デッキに空きがあれば 1 枚入れ、`mergeMastery` で段を合わせ、開示度は高いほうを残し、頁は受け継いだ頁として数え、`{ Cards, Enemies, Pages }` を返す |
| `JS/src/app/life.js:205-213` | 受け継いだ頁を生の最初から数えるために `BeginLife` を包む |

**`JS/src/bridge/exploration/EncounterBattle.js`**：`BattleHandIn` の末尾引数 `limits`（:24、:58）と、`NextLaunch` で `launch.Limits` に渡すこと（:211）。

**`JS/src/bridge/depiction/BattleLaunch.js`**：`Limits` 欄（:87）と `BuildDeck` での `Cards.Validate(Deck, Limits)`（:134）。`EXTENSIONS.md:72` が「未対応」と書いている件は、この行で既に直っています。

### 2.5 Depiction.Bridge：デッキと出立の画面（L-6 / L-7 / L-8 / X-17）

**`JS/src/bridge/depiction/DeckBuilder.js`（581 行。C# は 469 行）**：コンストラクタの `limits` と `Limits`（:82-85）、`CanAdd` / `Validate` / `StatusText`（上限超えなら「45 枚です。あと 5 枚外すと戦えます」、:168-178）、`RaisedLine`（「デッキ上限 +5（40 → 45）」、:185）、`TitleFor(limits)` とインスタンスの `Title`（:214-228）、`ShownAttributeWords` / `ShownAttributeOptions` / `ShownDetailOf`（攻撃・防御・スキル・スタンスの語、:293-313、X-17）、`WithPrototypeKinds`（:334、L-8）、`Prototype` / `Random` / `Load` / `Read` / `LoadOrPrototype` の `limits`（:346-466）、`ReadOverCap`（:502、試作 7）、`OverCapNotice`（:518、「生存者ボーナスが終わり、デッキ上限が 40 枚に戻りました。保存したデッキ 45 枚から、外す 5 枚を選んでください」）、`WithOwned`（:532）。

**`JS/src/bridge/depiction/DepartureBuilder.js`（334 行。C# は 286 行）**：コンストラクタの `limits` と `Limits`（:64-72）、枠数を `Limits` から読む `CanAddTool` / `CanAddConsumable` / `ToolsLine` / `ConsumablesLine`（:89-91、:276-278）、`Validate` はデッキの上下限をデッキ側の `Limits` から、枠を自分の `Limits` から読む（`LimitsWith`、:200-210、試作 2）、`TitleFor` / `ToolHeadingFor` / `ConsumableHeadingFor` とインスタンス版（:216-245）、`BonusLabel`「生存者ボーナス」（:252）、`RaisedLine`「ツール枠 +1（3 → 4）」（:258）、`IsBonusToolSlot`（:265）。

### 2.6 Depiction：複数の敵と俊敏の空きマス

**`JS/src/bridge/depiction/CoreScriptWriter.js`（1286 行。C# は 1032 行）**：`EnemyTrack`（:130、C# の `_enemy` / `_omen` / `_omenVisible` / `_omenPreview` / `_omenPreviewFor` / `_plan` / `_planVisible` / `_planChanged` / `_enemyReserveHeld` / `_enemyFalls` を敵ごとに持つ）、`EnsureTracks`（:223）、`FocusUnit`（:235、立っている敵のうち一番手前）、`Fallen`（:251）、`GapTo`（:262）、`Opening` の全敵（:270-298）、`Write` で倒れを拍ごとに挟み番号を振り直す（:316-357）、`WriteFalls` / `QueueFall`（:360-387）、`About`（:485）、`FlushHeldReserves`（:807）、狙いの既定 `TargetOf` / `Cheapest` / `CostOn` / `AimsAtOne` / `LineOf` / `ReachOn`（:927-1006）、`DefOf`（:1283）。

**`JS/src/bridge/depiction/CoreBattleSource.js`（362 行。C# は 221 行）**：`TargetFor`（:165）、`Inspect` / `TryPlay` / `PreviewFor` / `RefusalText` の末尾 `target`（:248-329）、`ShownGapGlyph`（:335）、`HasPlayableCard` / `AutoCardId` / `SuggestedCardId` が `TargetFor` と最も近い敵の間合いを読む（:131-155、:356）。俊敏の空きマスは `FreeStepKind = 'FreeStep'`（:58）、`FreeStepTitle = '俊敏'`（:61）、`CanTakeFreeStep(dir)`（:204）、`TakeFreeStep(dir)`（:219）、`WriteFreeStep`（:234）です。コアは `TurnLoop.CanTakeFreeStep` / `TakeFreeStep`（`UP/BattleCore/TurnLoop.cs:751-784`）を既に持ち、C# のソースだけが画面に出していません（出典 battle_core_v4 §5 #48、ui_ux §3.8 / §5.1 順 7 / §5.7 順 10）。

**`JS/src/bridge/depictionScript/DepictionTypes.js`（1205 行。C# は 1017 行）**：`CardFace.ReachOn / TargetUnit(-1) / CostOn / StanceHintAfterPayOn` と `ReachFor / CostFor / CostDropFor / StanceHintAfterPayFor`（:195-250）、`UnitFrame.Unit(-1) / Cell / Size / Gap(-1) / GapGlyph("") / Hatch(-1)`（:408-435）、`EnemyHatch`（:443、Count 3、角度 45 / −45 / 90）、`OmenFrame.Unit / Hatch`（:528）、`DepictionFrame.Enemies / Omens / SwatchesShown`（:594-615）、`FloorFrame.Enemies`（:751）、`FloorEnemy`（:769）、`Cue.Unit(-1) / UnitCellAfter / UnitGapAfter`（:929-945）、`DepictionEvent.Unit(-1)`（:976）。C# では `DepictionEventKind` の末尾に `FreeStep` を足します（`V/Depiction/Script/DepictionTypes.cs:656-665`）。`Effects.cs:563-569` は種類を `==` で見るだけなので、足しても壊れません。

### 2.7 文言とデータで、移植先が画面側になるもの

次は規則ではなく画面の文言なので、各画面の担当に回します。`JS/src/scenes/meta/text.js`（130）、`JS/src/scenes/explore/text.js`（104）、`JS/src/scenes/meta/journal.js` の `withStanceTerm` / `bookOf` / `tabsOf`（J-16 の戦闘中の敵ごとのタブ）、`SceneText.PagesNote`（R-27）。

---

## 3. 移植元より C# が進んだところ

`6289a9bd`（2026-10-19 22:21）から `f120ab9d` までの差分は 13 ファイルです。規則に関わるのは 1 ファイルだけです。

| C# の場所 | 変化 | JS との関係と移植での扱い |
| --- | --- | --- |
| `V/Exploration/Bridge/EncounterBattle.cs:43-45`（BattleHandIn のコンストラクタ） | 遭遇の敵ごとに開示度が無いと `ArgumentException("no 開示度 for {id}: every enemy of the encounter needs one")` | JS は欠けを 0 として読みます（`JS/src/bridge/exploration/EncounterBattle.js:198`）。C# が勝ちます。`Expedition.HandIn` は全員分を渡すので試遊版の流れは影響を受けません。試遊版で `BattleHandIn` を自前で組むときは全員分の開示度を入れてください |
| `EncounterBattle.cs:233` | `NextLaunch` の開示度を `HandIn.Disclosure[id]` で直接読む | 上と同じです |
| `EncounterBattle.cs:266-272` | `TryFinish(source)`：決着していれば記録して true、していなければ何もせず false | JS の `settleWhenDecided` と同じ目的です。C# 版の記録係（U5）はこれを使います |
| `V/Exploration/ExplorationBootstrap.cs` | 戦闘を見せられなかった後は代わりの札だけにする `_standInOnly` | View です。試遊版には関係しません |
| `V/Depiction/Script/DragTrace.cs`（新規 161）と `V/Depiction/DragTraceRecorder.cs`（新規 260） | カードのドラッグを 1 フレーム 1 行の JSON で記録する道具（#643） | JS にはありません。規則ではないので移植対象外です |
| テスト | `UniqueCardsTests.cs` +132、`CoreAgreementTests.cs` +48、`LayerBenchTests.cs` +11、`MasteryProfilesTests.cs` の書き換え、`ViewRulesBoundaryTests.cs` の許可リストから `Exploration/ExplorationBootstrap.cs` を外した | 規則は変わっていません。`ViewRulesBoundaryTests` の制約は §6.1 で扱います |

`TurnLoop`、`CoreScriptWriter`、`CoreBattleSource`、`ExplorationSession`、`Expedition`、`BattleRewards`、`DeckBuilder`、`DepartureBuilder`、`CoreText`、`CardFaceText` は差分ゼロです。JS の移植済み部分はこれらと今も一致しています（JS 側の parity は 2026-10-20 に TurnLoop をバイト一致で確認済み、`JS/docs/port-notes/battle-turnloop.md`）。

---

## 4. JS が直した C# の不具合

### 4.1 2 体以上の敵で描写の書き手が例外を投げる（実害あり）

- **症状**：探索の 2〜3 層の精鋭ノードは `polearm_warped` + `crossbow_hunter` の 2 体戦です（`LayerEncounters`、roster §9）。`BattleLaunch` はコアに 2 体を渡し（`V/Depiction/Bridge/BattleLaunch.cs:114-117`）、コアは戦います。ところが `CoreScriptWriter` が `KeyNotFoundException: The given key 'bolt' was not present in the dictionary.` を投げ、`CoreBattleSource.AdvanceAuto / EndTurn / TryPlay` から外へ出ます。Battle.unity でも同じです。
- **原因**：`CoreScriptWriter.cs:55` の `_omen` が 1 つしかなく、`:372-383` の `case OmenSet` が `omen.Unit` を見ずに上書きします。2 体目の予兆（`bolt`）が `_omen` に入ると、`Snapshot`（:817）→ `FloorOf`（:906、:919 `FloorCells.AimOf(_omen, after.Enemies[0].Body, ...)`）→ `AsShown`（:934-939、`shown.WithUnit(0, ... Omen = _omen ...)`）で 1 体目に 2 体目の行動 id を載せ、`TurnLoop.OmenBoundaryCells(shown, 0)` が 1 体目の行動表を引いて失敗します。
- **直し方（JS の実装）**：書き手が持つ敵の情報を `EnemyTrack` として敵ごとに持ちます（Def、UnitModel、Omen、OmenVisible、OmenPreview、OmenPreviewFor、Plan、PlanVisible、PlanChanged、ReserveHeld、FallPending）。トラック 0 が C# の 1 体ぶんそのものです。
  1. どの `BattleEvent` も `Unit` を持っているので、その敵のトラックに書きます。`OmenSet` は `omen.Unit` のトラックの `Omen` を変えます。
  2. フレームには全員の `UnitFrame` と `OmenFrame` を、それぞれ自分の敵の Def で読んで書きます（`DepictionFrame.Enemies / Omens`、`FloorFrame.Enemies`）。`AsShown` は `WithUnit(k, ...)` を敵ごとにします。
  3. C# の単数のメンバー（`Frame.Enemy`、`Frame.Omen`、`Floor.EnemyCell / Gap / AimCells`、`Player.RangeGlyph`）は `FocusUnit` の敵を表します。`FocusUnit` は「倒れていない敵のうちセルが一番小さい敵。この手で倒れた敵は飛ばす。最後の 1 体ならその敵。全員の倒れを書いた後は最後に倒れた敵」です。1 体戦では常に 0 です。
  4. `EnemyDefeated`（他が立っている）と `BattleEnded` 勝ち（最後の 1 体）は敵ごとに倒れを予約し、閉じた拍の直後に、その敵の `Defeat` 拍を 1 つずつ書きます。次の拍の番号を後ろへずらし、`Order` は 1, 2, 3 … のまま保ちます。負けた戦闘では、プレイヤーと同時に倒れた敵の倒れは書きません。
  5. 敵の構えの Guard（予約）は、その敵自身の次の予兆の頭で流します。1 体戦では C# と同じ場所です。
  6. `EndTurn` は「ターン終了(−1) → 敵の番(0) → 0 の行動の拍 → 次の予兆(0) → 敵の番(1) → … → 次の予兆(1)」の順になります。
- **1 体戦が変わらないことの担保**：JS は変更前のコードで 127 通りの 1 体戦（全敵 × 種、開示度 0 / 1、狭い列と広い列、持ち越し HP とスタミナ、道具、ターン上限、GreedyPlayer 戦）の全出力をハッシュで固定し（`JS/test/depiction/MultiEnemy.single.golden.json`）、変更後に C# の既存メンバーだけを比べています。C# でも同じ手順を踏みます（§6.4）。

### 4.2 狙う敵を渡さないので、先頭の敵が倒れると勝てない（実害あり）

- **原因**：`TurnLoop.PlayCard / CanPlay / Preview` は `target = 0` が既定です。`CoreBattleSource.cs` は `:92`、`:137`、`:159`、`:173`、`:216` で target を渡しません。先頭の敵が倒れると、単体を狙う札はすべて拒まれ（コアの `NoSuchTarget`。`Inspect` では `NotWaiting` になる）、2 体戦に勝てません。`:91` の逃げの規則（#192）も倒れた敵の間合い（`_state.Gap`）を読みます。
- **直し方**：`TargetFor(cardId)` = `CoreScriptWriter.TargetOf(state, id)` を既定の狙いにします。「今その札を出せる立っている敵のうち、一番安く出せる敵（同じなら一番手前）。無ければ届く敵のうち同じ選び方。無ければ一番手前の立っている敵。誰も立っていなければ 0」です。`Inspect / TryPlay / PreviewFor / RefusalText` に狙いの引数を足し、省略時は `TargetFor` を使います。札の表のコスト・▼・構えの予告・予測値も同じ敵で読みます（visual §6.3 決定 6）。立っていない敵を名指ししたら `NotWaiting` を返します（`PlayVerdict` の値は増やしません）。`OutOfRange` の文言は、名指しした敵の `GapGlyph` を引きます。逃げの規則は最も近い立っている敵の間合いを読みます。1 体戦ではすべて 0 で、C# と同じです。

### 4.3 C# が画面に出していない規則（不具合ではなく抜け）

- 俊敏の空きマス（§2.6）：コアにはあり、C# のソースは呼びません。Unity の戦闘ではこの一歩が無駄になります。
- 生のデッキ上限が戦闘に届かない（§2.4）：`BattleLaunch.BuildDeck` は C# の 20〜40 で検査するので、生存者ボーナスの 45 枚デッキは最初の戦闘で例外になります。

### 4.4 C# の癖で、JS もそのまま残しているもの

`CoreScriptWriter.Opening` が番号などを初期化しない点と、`Snapshot` の `Corner.Floor = 1` / `MiasmaPercent = 0`（#99 で未接続）は JS も同じです。試遊版の戦闘画面は層と瘴気を探索側の値（`BattleHandIn.Layer` / `MiasmaPercent`）から取ります（`JS/src/app/main.js:447-449`）。

---

## 5. 規則の数値と固定の文字列（移植で取り違えやすいもの）

| 項目 | 値 | JS の場所 |
| --- | --- | --- |
| 生存者ボーナス | ツール枠 3 → 4、デッキ上限 40 → 45、次の 1 生（追加ボスに勝てば 2 生） | `Inheritance.js:43-66` |
| 遺産 | 3 つまで、候補は 1 段以上、2 段以上は 3 層以上で死んだときだけ、開示度の上限 2 | 同上 |
| 装束の段 | 前の生の到達層 0〜7 → `[0,0,1,1,2,2,3,3]`、踏破 → 4。名前と色は `AttireTiers`（:90-96） | 同上 |
| 継承者の名前 | 40 個のカタカナ（:77-82）。系譜の種から周回ごとにシャッフルし、周回の境で同じ名前が続かない | `heirName` :258 |
| 種の混ぜ方 | `mix(seed, salt) = seed * 0x9E3779B97F4A7C15 + salt`（64 ビット）。名前は salt `0x5A17 + cycle`、生の既定種は `SeededRng(mix(seed, 0xC0FFEE + generation)).NextUInt64()` | :229、:249、:587 |
| 痕跡と候補の id | 痕跡 `trace-{generation}`、候補 `card:{id}` / `enemy:{id}` / `page:{id}` / `scribble:{i}` / `memo` | :740、:486-501 |
| 系譜の保存形式 | `Version 1`、`Seed` は 10 進の文字列、`Lives` / `Current` / `Traces` / `Survivor` / `Player{CarvingTaken, RootDefeated}` | :550-560、:1171-1221 |
| イベントの種 | `EncounterSeed ^ 0x9E6C63D0676A9A99` | `ExplorationSession.js:524` |
| 宝箱の種 | `Expedition.RewardSeed`（`EncounterSeed ^ 0xD1B54A32D192ED03`、C# に既存） | `Expedition.js:277` |
| 情報収集の対象順 | Boss → Elite → Battle、ノード番号順、開示度 2 未満の最初の敵 | `intel.js:32`、:94-107 |
| 七層で開示度 2 にする敵 | `distortion_root` | `intel.js:40` |
| 敵の頁の行と必要な開示度 | 名前 0 / HP 最大 0 / 得意な間合い 1 / 間合い 0・1〜2・3 以上 1 / 弱点 2 / 特殊行動 2 / 適応条件 2（ボスだけ）/ 自分のメモ 0 | `EnemyPages.js:279-303` |
| 設定の既定値 | 速さ ×1.25、演出短縮 切、主音量 0.8、効果音 0.9、環境音 0.6、消音 切 | `save.js:10-17` |

---

## 6. 移植計画

### 6.1 アセンブリの置き場と参照

置き場を決める制約が 2 つあります。

- **View と規則の境界テスト**：`UP/Depiction.Bridge.Tests/ViewRulesBoundaryTests.cs` は `V/` 以下で、フォルダ名が `Bridge` か `Tests` でないすべての `.cs` を読みます。そこでは `using BattleCore;` が許可リストのファイル以外で禁止され、`TurnLoop. EnemyAi. Combat. Field. Traits. Statuses. Columns. Mastery. Cards. Chain. DeckRules. Constants.` の呼び出しも禁止です。`Constants.DeckMax` を読むだけでも引っかかります。試遊版の規則コードは、`V/Trial/Bridge/` のように `Bridge` という名前のフォルダか、`V/` の外のコアプロジェクトに置きます。
- **unity:sync**：`UP/tools/unity-sync-plan.mjs` は `BattleCore` / `DungeonCore` / `DungeonContent` を平らに写し、`V/` 以下は `Depiction` / `Backdrop` / `UiArt` / `Exploration` / `Journal` だけを再帰で写します。新しいコアの置き場と `V/Trial` を足し、`OWNED_ROOTS` にも足す必要があります。

| アセンブリ | 置き場 | 参照 | 中身 |
| --- | --- | --- | --- |
| BattleCore（既存） | `UP/BattleCore/` | なし | 新規 `DeckLimits.cs`。`Cards.cs` / `DeckRules.cs` に省略可能な引数 |
| DungeonCore（既存） | `UP/DungeonCore/` | なし | 新規 `LayerEvents.cs`、新規 `ExplorationReducer.Trial.cs`（`partial`）。`RunLoadout.cs` / `ExplorationState.cs` / `ExplorationReducer.cs` の小さな変更 |
| Exploration.Script（既存） | `V/Exploration/Script/` | DungeonCore, DungeonContent, Journal.Script | 新規 `TrialItems.cs`、`ExplorationKeeper.cs`、`ExplorationSession.Trial.cs`、`ExplorationScreenBuilder.Trial.cs`、`ExplorationText.Trial.cs`。既存 5 ファイルの変更 |
| Exploration.Bridge（既存） | `V/Exploration/Bridge/` | 既存の参照 + **InheritanceCore（追加）** | 新規 `Expedition.Trial.cs`（`partial`）、`ExpeditionOptions.cs`、`Loadouts.cs`、`MasteryView.cs`、`TraceGain.cs`。`Expedition.cs` / `EncounterBattle.cs` の変更 |
| Depiction.Script / Depiction.Bridge（既存） | `V/Depiction/Script/`、`V/Depiction/Bridge/` | 既存のまま | 複数の敵、俊敏、デッキの枠 |
| **InheritanceCore（新規、エンジン非依存）** | `UP/InheritanceCore/` → Unity では `Assets/InheritanceCore/` | BattleCore, DungeonCore | `Inheritance.cs`（関数群）、`InheritanceText.cs`、`Lineage.cs`（record 群）、`LineageJson.cs`、`MiniJson.cs`。名前空間は `InheritanceCore`、静的クラス `Inheritance` |
| **Trial.Bridge（新規、エンジン非依存）** | `V/Trial/Bridge/` | BattleCore, DungeonCore, DungeonContent, Journal.Script, Exploration.Script, Exploration.Bridge, Depiction.Script, Depiction.Bridge, InheritanceCore | `Life.cs`、`Names.cs`、`JournalIntel.cs`、`EnemyPages.cs`、`Recorder.cs`、`SaveData.cs`、`DepartureCarry.cs` |

どちらの新規アセンブリも `noEngineReferences: true` にし、`dotnet test` 用の csproj（`UP/InheritanceCore.Tests/`、`UP/Trial.Bridge.Tests/`）を作って `UP/UnityCorePort.slnx` に足します。ファイルの読み書き（`Application.persistentDataPath`）だけは Trial の View 側に置きます。

`V/Depiction/DepartureSupplies.cs` は View の名前空間 `Depiction.View` にあり、`using BattleCore;` を足すと境界テストに落ちます。`Carry` と `NewBuilder(limits)` は `Trial.Bridge/DepartureCarry.cs` に置き、既存の `DepartureSupplies.cs` は変えません。

### 6.2 opt-in の切り替え（C# の形）

| 拡張 | 切り替え | 省略時 |
| --- | --- | --- |
| デッキの枠 | `DeckLimits limits = null` を `Cards.Validate`、`DeckRules.ValidateDeck / CanAdd / Validate`、`DeckBuilder` と `DepartureBuilder` のコンストラクタと静的生成メソッド、`BattleLaunch.Limits`、`BattleHandIn` 末尾、`ExpeditionOptions.Limits` に | `DeckLimits.Default`（今の C# の定数） |
| 探索の試遊規則 | `RunStart.Trial`（bool）、`ExplorationReducer.Enter(..., bool trial = false)`、`RunLoadout(..., bool trial = false)`、`ExplorationState(..., bool trial = false)`、`ExpeditionOptions.Trial` | false。`ReliefKind` / `ScreenAction` / `CardKind` に足した値は使われない |
| 地図の差し替え | `RunStart.MapFor`（`Func<LayerProfile, ulong, LayerMap>`） | null なら `profile.Map(seed)` |
| 彫りの既知 | `RunStart.CarvingKnown`（`bool?`）、`ExpeditionOptions.CarvingKnown` | null なら地図から推定。trial でなければ常に false |
| 受け継いだ頁 | `ExpeditionOptions.StartPages`（int） | 0 |
| 敵の情報と手記の上げ | `Expedition` の `intel` に `JournalIntel` を渡し、`Bind(expedition)` する | `NoEnemyIntel`（「敵: ？」） |
| 情報収集の対象 | `IEnemyIntel` を実装したうえで `ISurveyIntel` も実装する | 実装していなければ何も聞かない |
| 解決時の追加処理 | `IArrivalIntel.Arrived(nodeId)` を `ExplorationSession.Arrived` の最後で呼ぶ | 実装していなければ呼ばない |
| 複数の敵 | 切り替えなし。1 体戦は全出力が同じであることを固定テストで担保 | 1 体戦の出力は今と同じ |
| 狙いの指定 | `CoreBattleSource` に target 付きの多重定義を足し、target なしは `TargetFor` に委ねる。`IDepictionSource` は変えない | 1 体戦は常に 0 |
| 俊敏の空きマス | `CanTakeFreeStep(int? dir = null)` / `TakeFreeStep(int dir)` を足す | 呼ばなければ C# と同じ |

JS が `TrialAction` と `TrialCardKind` を別の列挙にしたのは、JS 側の列挙順テストを守るためでした。C# では `ScreenAction` の末尾に `AcceptEvent, DeclineEvent, TakeFound, LeaveFound, ChooseSurvey`、Exploration の `CardKind` の末尾に `EventChoice, Swap, SurveyChoice` を足すほうが素直です。これらの列挙を全値で回す C# テストは無いと確かめました（`Enum.GetValues` の使用箇所を全部見ました）。`Act` は `Act(ScreenAction action, string arg = null)` にし、`TakeFound` の枠番号は不変カルチャの整数文字列で渡します。

C# は同名の静的プロパティとインスタンスプロパティを持てません。JS の静的 `Title` とインスタンス `Title`（DeckBuilder、DepartureBuilder の `Title` / `ToolHeading` / `ConsumableHeading`）は、静的側を C# のまま残し、インスタンス側を `LifeTitle` / `LifeToolHeading` / `LifeConsumableHeading` と呼びます。JS との対応表に 1 行書いておいてください。

### 6.3 拡張ごとの置き場

| JS | C# の置き場 | 単位 |
| --- | --- | --- |
| `DeckRules.js` の `DeckLimits` | 新規 `UP/BattleCore/DeckLimits.cs` | U1 |
| `Cards.js:263`、`DeckRules.js:178-237` | `UP/BattleCore/Cards.cs`、`UP/BattleCore/DeckRules.cs` | U1 |
| `DeckBuilder.js`、`DepartureBuilder.js`、`BattleLaunch.js` の拡張 | `V/Depiction/Bridge/DeckBuilder.cs`、`DepartureBuilder.cs`、`BattleLaunch.cs` | U1 |
| `LayerEvents.js` | 新規 `UP/DungeonCore/LayerEvents.cs`（`EventKind`、`ChestContents` record も） | U2 |
| `RunLoadout.js`、`ExplorationState.js`、`ExplorationReducer.js` の拡張 | 既存ファイルの最小変更 + 新規 `UP/DungeonCore/ExplorationReducer.Trial.cs` | U2 |
| `TrialItems.js` | 新規 `V/Exploration/Script/TrialItems.cs` | U2 |
| `ExplorationSession.js` / `ExplorationScreen.js` / `ExplorationScreenBuilder.js` / `ExplorationText.js` の拡張 | 既存ファイルのフック + 新規 `*.Trial.cs`（`partial` に変える） | U2 |
| Keeper の型 | 新規 `V/Exploration/Script/ExplorationKeeper.cs`（`IExplorationKeeper`、`ChestFind`、`PendingEventInfo`） | U2 |
| `EnemyIntel.js` に対する差し込み口 | `V/Exploration/Script/EnemyIntel.cs` に `ISurveyIntel`、`IArrivalIntel` | U2 |
| `CoreScriptWriter.js` / `CoreBattleSource.js` / `DepictionTypes.js` の拡張 | `V/Depiction/Bridge/CoreScriptWriter.cs`、`CoreBattleSource.cs`、`V/Depiction/Script/DepictionTypes.cs` | U3 |
| `Inheritance.js` | 新規 `UP/InheritanceCore/*.cs` | U4 |
| `Expedition.js` の拡張と `life.js` の `Inherit` | `V/Exploration/Bridge/Expedition.cs`（`partial` とフック）+ 新規 `Expedition.Trial.cs`、新規 `ExpeditionOptions.cs` | U5 |
| `EncounterBattle.js` の `Limits` | `V/Exploration/Bridge/EncounterBattle.cs` | U5 |
| `Loadouts.js`、`MasteryView.js`、`TraceGain.js` | 新規 `V/Exploration/Bridge/` の同名ファイル | U5 |
| `life.js`、`names.js`、`intel.js`、`EnemyPages.js`、`recorder.js`、`save.js`（直列化）、`DepartureSupplies.js` の拡張 | 新規 `V/Trial/Bridge/` の `Life.cs`、`Names.cs`、`JournalIntel.cs`、`EnemyPages.cs`、`Recorder.cs`、`SaveData.cs`、`DepartureCarry.cs` | U5 |

### 6.4 移植するテスト

| JS のテスト | 件数 / 行数 | C# の置き場 | 単位 |
| --- | --- | --- | --- |
| `JS/test/ext/limits.test.mjs`（L-1〜L-9 と受け渡し） | 22 / 507 | `UP/BattleCore.Tests/DeckLimitsTests.cs`、`V/Depiction/Bridge/Tests/DeckLimitsBuilderTests.cs`。受け渡しの 3 件は U5 の `V/Exploration/Bridge/Tests/` へ | U1（受け渡しは U5） |
| `JS/test/ext/last-round.test.mjs` の X-17 デッキの語 | 1 | `V/Depiction/Bridge/Tests/` | U1 |
| `JS/test/ext/world.test.mjs`（W-1〜W-19） | 44 / 961 | 規則は `UP/DungeonCore.Tests/TrialRulesTests.cs`、画面は `V/Exploration/Tests/TrialSessionTests.cs`、Expedition と再生は U5 の `V/Exploration/Bridge/Tests/` | U2（一部 U5） |
| `JS/test/ext/explore-results.test.mjs`（X-1〜X-17） | 28 / 630 | セッションと画面の分は `V/Exploration/Tests/`、Expedition と痕跡の分は `V/Exploration/Bridge/Tests/` | U2 / U5 |
| `JS/test/depiction/MultiEnemy.test.mjs` と `_singleEnemyRuns.mjs` | 14 / 778 | `V/Depiction/Bridge/Tests/MultiEnemyTests.cs`。**最初に**今の C# で 1 体戦の固定データを書き出すテスト（生成モード付き）を作り、その後に書き手を変える。JS のハッシュは C# の直列化と一致しないので流用できません | U3 |
| `JS/test/depiction/FreeStep.test.mjs` | 8 / 217 | `V/Depiction/Bridge/Tests/FreeStepTests.cs` | U3 |
| `JS/test/inheritance/Inheritance.test.mjs`（R-01〜R-27） | 47 / 680 | `UP/InheritanceCore.Tests/InheritanceTests.cs` | U4 |
| `JS/test/app/stamp.test.mjs` の系譜 JSON 部分 | 3 / 81 | `UP/InheritanceCore.Tests/LineageJsonTests.cs`（印とタブの部分はブラウザ固有なので落とす） | U4 |
| `JS/test/ext/journal.test.mjs`（J-1〜J-16） | 16 / 761 | `V/Trial/Bridge/Tests/JournalIntelTests.cs`。J-2 は `.claude/docs/enemy_document/enemy_roster_v4.md` を読むので、ファイルが無ければ `Assert.Ignore` | U5 |
| `JS/test/ext/last-round.test.mjs` の X-15 | 4 | `V/Exploration/Bridge/Tests/MasteryViewTests.cs` | U5 |
| `JS/test/app/inherit-gains.test.mjs`、`life.test.mjs`、`settle.test.mjs`、`survivor.test.mjs` | 21 / 777 | `V/Trial/Bridge/Tests/` | U5 |
| `JS/test/exploration/EnemyIntel.test.mjs`、`JournalTerms.test.mjs` | 既存の移植 | C# に既にあるので不要 | — |
| `JS/test/meta/traceLines.test.mjs` | 6 / 75 | 画面の文言のテストなので、継承の間・終わりのカードの画面担当へ | — |

### 6.5 並行できる 5 単位

互いに同じファイルを触りません。共通の足場（新規 csproj と asmdef、`UnityCorePort.slnx`、`tools/unity-sync-plan.mjs` の `OWNED_ROOTS` と写し先、`Exploration.Bridge` の asmdef と csproj への InheritanceCore 参照の追加）は、5 単位が始まる前にアーキテクトか統合役が 1 回で入れます。

| 単位 | 触るファイル | 依存 | C# の行数の見込み |
| --- | --- | --- | --- |
| **U1 デッキの枠** | `UP/BattleCore/DeckLimits.cs`（新）、`Cards.cs`、`DeckRules.cs`、`V/Depiction/Bridge/DeckBuilder.cs`、`DepartureBuilder.cs`、`BattleLaunch.cs`、テスト新規 2 ファイル | なし | 本体 380、テスト 450 |
| **U2 探索の試遊規則** | `UP/DungeonCore/LayerEvents.cs`（新）、`ExplorationReducer.Trial.cs`（新）、`RunLoadout.cs`、`ExplorationState.cs`、`ExplorationReducer.cs`、`V/Exploration/Script/TrialItems.cs`（新）、`ExplorationKeeper.cs`（新）、`ExplorationSession.cs` と `.Trial.cs`（新）、`ExplorationScreen.cs`、`ExplorationScreenBuilder.cs` と `.Trial.cs`（新）、`ExplorationText.cs` と `.Trial.cs`（新）、`EnemyIntel.cs`、テスト新規 | なし | 本体 1500、テスト 900 |
| **U3 複数の敵と俊敏** | `V/Depiction/Bridge/CoreScriptWriter.cs`、`CoreBattleSource.cs`、`V/Depiction/Script/DepictionTypes.cs`、テスト新規（固定データ生成を含む） | なし | 本体 650、テスト 900 |
| **U4 継承のコア** | `UP/InheritanceCore/*.cs`（新）、`UP/InheritanceCore.Tests/*.cs`（新） | なし（既存の BattleCore / DungeonCore の API だけを使う） | 本体 1300（JSON 250 を含む）、テスト 700 |
| **U5 遠征と試遊版の継ぎ目** | `V/Exploration/Bridge/Expedition.cs` と `Expedition.Trial.cs`（新）、`ExpeditionOptions.cs`（新）、`EncounterBattle.cs`、`Loadouts.cs`（新）、`MasteryView.cs`（新）、`TraceGain.cs`（新）、`V/Trial/Bridge/*.cs`（新 7 本）、テスト新規 | U1・U2・U4 の公開 API。記録係は U3 の `TryPlay(target)` と `TakeFreeStep` も記録する | 本体 1900、テスト 1200 |

U5 は下の契約に合わせて書けば U1〜U4 と同時に始められます。結合は最後に U5 が行います。

**U1 が出す API**

```csharp
namespace BattleCore {
  public sealed class DeckLimits : IEquatable<DeckLimits> {
    public DeckLimits(int? deckMin = null, int? deckMax = null, int? toolSlots = null, int? consumableSlots = null); // 不正値は ArgumentOutOfRangeException
    public int DeckMin { get; } public int DeckMax { get; } public int ToolSlots { get; } public int ConsumableSlots { get; }
    public static DeckLimits Default { get; }            // 20, 40, DeckRules.ToolSlots, DeckRules.ConsumableSlots
    public static DeckLimits From(DeckLimits? limits);  // null → Default
    public bool IsDefault { get; }
    public DeckLimits With(int? deckMin = null, int? deckMax = null, int? toolSlots = null, int? consumableSlots = null);
  }
  // Cards.Validate(IReadOnlyList<CardInstance> deck, DeckLimits? limits = null)
  // DeckRules.ValidateDeck(deck, ownedKinds, DeckLimits? limits = null) / CanAdd(deck, id, ownedKinds, DeckLimits? limits = null) / Validate(departure, ownedKinds, DeckLimits? limits = null)
}
// Depiction.Bridge: new DeckBuilder(IReadOnlyCollection<string>? owned = null, DeckLimits? limits = null); DepartureBuilder(tools, consumables, DeckLimits? limits = null); BattleLaunch.Limits (DeckLimits?)
```

**U2 が出す API**

```csharp
namespace DungeonCore {
  public enum ReliefKind { Density, Gauge, Time, Survey, Interlude, Battle, Heal, MaxStamina, Refill, Teleport }
  // RunLoadout(tools = null, consumables = null, int toolSlots = ToolSlots, bool trial = false)
  //   bool Trial; int TimeBonus; int InterludeBonus; bool ChoosesSurvey; int SumOf(ReliefKind); bool HasFreeSlot;
  //   RunLoadout Take(ReliefItem item, int replaceSlot = -1); RunLoadout Spend(int index, out int gaugeRelief, out ReliefItem item)
  // ExplorationState(..., RunPhase phase, bool trial = false); bool Trial; int TimeLimit; int TimeBonus; With(..., bool? trial = null)
  // ExplorationReducer.Enter(..., int tempMaxStaminaMod = 0, bool trial = false); Teleport; CanTeleport; ResolveEvent(state, EventKind, bool accept);
  //   InterludeHealOf; SpringHeal; MealCost; TakeConsumable(state, ReliefItem, int replaceSlot = -1); InterludePercent
  public enum EventKind { Spring, HeavyChest, StrangeMeal }
  public sealed record ChestContents(int Cards, int Consumables, int Pages);
  public static class LayerEvents { /* ChestStaminaCost=2, MealStaminaGain=1, MealHpPercent=10, Weights, WeightOf, SpringPercent, ChestContents, HasChoice,
                                       IReadOnlyDictionary<int, EventKind> Draw(LayerMap map, int layer, Func<int, ulong> seedOf) */ }
}
namespace Exploration {
  // RunStart: bool Trial; bool? CarvingKnown; Func<LayerProfile, ulong, LayerMap> MapFor
  public interface IExplorationKeeper {
    ChestFind TakeChest(int layer, int nodeId, ChestContents contents); void TakeSurvey(string enemyId);
    int DisclosureOf(string enemyId); string EnemyName(string enemyId); string InheritedTraceId { get; } int? InheritedLayer { get; }
  }
  public sealed record ChestFind(IReadOnlyList<string> Cards, IReadOnlyList<string> Consumables, int Pages, bool PileSpent);
  public interface ISurveyIntel { string SurveyTarget(int layer, LayerMap map); }
  public interface IArrivalIntel { void Arrived(int nodeId); }
  // ExplorationSession: Trial, Keeper{get;set;}, PendingEvent, Events, PendingFound, SurveyChoices, SurveyTarget, SurveyPicked, AimingSlot,
  //   EventState, InterludeState, TrainingWaiting, Has*, IsAiming, CanUseSlot, Jump, OpenTraining, PutOffTraining, Act(ScreenAction, string arg = null),
  //   static EventSeed, static LayerEnemies, EventAt, Receive(IEnumerable<string> ids, int fromEvent = -1), TurnDownFind(int eventIndex, string line), CarvingKnownAt
  // ExplorationScreenBuilder: static SurveyLine(session), static InterludeCard(session), static ChangeLines(before, after),
  //   Forecast(before, nodeId, choice, int jumpSlot = -1, EventKind? arrivalEvent = null)
}
```

**U3 が出す API**：`CoreBattleSource.TargetFor(string)`、`Inspect(string, int target)`、`TryPlay(string, DropZone, int target, out DepictionEvent)`、`PreviewFor(string, int)`、`RefusalText(string, PlayVerdict, int)`、`AutoCardId`、`CanTakeFreeStep(int? dir = null)`、`TakeFreeStep(int dir)`、`static CoreScriptWriter.TargetOf(BattleState, string)`、§2.6 の DepictionTypes のメンバー、`DepictionEventKind.FreeStep`。

**U4 が出す API**：`InheritanceCore.Inheritance` の静的メソッド（`NewLineage`、`NextHeir`、`HeirName`、`TierAfter`、`LifeSeed`、`StartLife`、`TraceForLayer`、`TraceLayers`、`MapSpecFor`、`PlaceFromState`、`RecordDeath`、`RecordSurvival`、`RecordEnd`、`NoteCarving`、`TakeTrace`、`MergeJournal`、`HeirJournal`、`MergeMastery`、`NormalizeJournal`、`EmptyJournal`、`IsFilledPage`、`HallView`、`TraceNodeView`、`PickState`、`EndingView`、`ToJson`、`FromJson`）、`InheritanceRules`、`InheritanceText`、`HeirNames`、`AttireTiers`、`LifeEnding`、`CandidateKind`、`InheritanceRuleException`。系譜は不変の record で表し、JS の「凍結した素のオブジェクト」と同じ意味にします。`MergeMastery` は `IReadOnlyDictionary<string, CardMastery>` を受けて返します（JS の `make` 引数は要りません）。

**U5 が出す API（試遊版の View が使う）**

```csharp
namespace Exploration.Bridge {
  public sealed class ExpeditionOptions { public DeckLimits Limits; public bool Trial; public bool? CarvingKnown; public int StartPages; }
  // Expedition(start, deck, battleToolIds, talent, intel, journal, ExpeditionOptions options = null) : IExplorationKeeper
  // + PendingResult, CloseResult, OfferFits, DeckMax, ShowTaken(TakenCard), PendingTaken, CloseTaken, TakeChest, TakeSurvey, EnemyName,
  //   Events, InheritedTraceId, InheritedLayer, InheritedPages, FoundPages, InheritResult Inherit(TakenLegacy taken)
}
namespace Trial.Bridge {
  // Life.ChoiceToData / MakeStart / LifeSetup(start) → (Expedition, TrialLife) / LifeScribbles / AddScribbles / StartPages
  // Names.LifeJournal / CardName / EnemyName / EndOptions
  // JournalIntel : IEnemyIntel, ISurveyIntel, IArrivalIntel { Bind(Expedition); SurveyTarget; Arrived; SurveyedEnemy; LevelOf; static EncounterAt; KnownOnLastLayer }
  // EnemyPages（§1.1）
  // Recorder: LifeLog, LogEntry(Tag, Method, Args, Count), RecordedExpedition / RecordedSession / RecordedBattle / RecordedSource, Replay(expedition, log) → (kept, open), SettleWhenDecided, TrimUnfinished
  // SaveData: SaveFile(lineage, life{start, log, memo, scribbles}, deckSave)、SettingsData の既定値、JSON 化
  // DepartureCarry.NewBuilder(DeckLimits) / Carry(Departure, DeckLimits) → (battleToolIds, consumableIds, loadout)
}
```

`Expedition` の `partial` 化について補足します。JS の `life.js` は `Expedition.prototype.Inherit` を足し、`BeginLife` と `Inherit` を包んでいます。C# では差し替えができないので、`Inherit` を `Expedition` の正式なメソッドにし、受け継いだ頁は `ExpeditionOptions.StartPages` で `BeginLife` が数えます。地図の差し替えが読む「この生で痕跡を受け取った」は、JS の `life.traceTaken` の代わりに `expedition.InheritedTraceId != null` で判定します（JS でも `NewLife` で戻らない点まで同じです）。

### 6.6 確かめ方

- 各単位の前後で `dotnet test UP/UnityCorePort.slnx` を回し、既存テストが全部通ることを確かめます。opt-in を省いた既定の動きは今の C# と同じなので、既存テストは 1 件も変えずに通るはずです。
- U3 は、固定データの生成を今の C# で先に実行し、そのデータを入れたコミットを作ってから書き手を変えます。
- U5 の結合テストは、JS の `settle.test.mjs` と `survivor.test.mjs` と同じ筋書き（何代かの生、死、痕跡の受け取り、生存者、保存の途中からの再生）で、全保存点から再生して同じ状態になることを見ます。
- `ViewRulesBoundaryTests` が通ることも毎回確かめます（Trial の View を足したときに落ちやすいテストです）。

---

## 7. 判断が要る点と危険

### 7.1 設計書に無く、JS の試作で決めた規則（移植すると規則として固まるもの）

- イベント：4〜6 層の重み（泉が 1 層ごとに 10 減る延長）、食事で死なない（HP は 1 で止まる）、宝箱の札は山から 3 枚出して 1 枚選ぶ、泉は選択なしで即解決、受けないイベントは結果札を出さない、刻限を使い切ったノードでもイベントは起きる（EXTENSIONS 世界の試作 1〜15）。
- 消耗品：何も変わらないなら使わせない、標の石はボスにも跳べる、照準してから跳ぶ。
- 継承：名前の候補 40、装束 5 段とその閾値、前の生だけで段を決める、同じ層に古い痕跡があれば古い順、何も選ばなくても痕跡は回収済み、段のティックはその段の閾値から、3 段目は 1 枚まで、踏破でも 7 層に札だけの痕跡を残す、痕跡ノードは戦闘を持たない（INHERITANCE 試作 1〜16）。
- 手記：情報収集の対象順、メモは 1 生に 1 つ、歪みの根は七層に入った時点で開示度 2、竜神（`dragon_god`）はコアに無いので頁も無い（J の試作 1〜12）。
- 出立：ボーナスは出立画面にも出す、上限を超えた保存デッキは全部開いて外す枚数を言う（L の試作 6 / 7）。

### 7.2 危険

- `ViewRulesBoundaryTests` は、`V/` 以下で `Bridge` / `Tests` 以外のフォルダにある `.cs` の `using BattleCore;` と規則クラスの呼び出し（`Constants.` を含む）を落とします。Trial の View を書く人にも共有が要ります。
- U3 は Battle.unity が使う共有の書き手（`CoreScriptWriter.cs`）を変えます。1 体戦の固定データで守りますが、戦闘担当の作業場所と同時に触ると衝突します。今回は main が持つと決めてから始めてください。
- 系譜と生の保存には JSON が要ります。Unity の `JsonUtility` は辞書と null を扱えず、`System.Text.Json` は Unity に入っていません。Newtonsoft は Unity プロジェクトに間接依存（`com.unity.nuget.newtonsoft-json` 3.2.1、packages-lock.json）でしか入っていないので頼れません。エンジン非依存の小さな JSON（`MiniJson.cs`）を InheritanceCore に置く案を勧めます。
- 再生による保存は、C# では窓口クラスとメソッド名の振り分けで作り直します。JS の保存データを Unity で読む必要はありませんが、記録するメソッドの一覧を JS と揃えないと、再生で状態がずれます。JS の画面が呼ぶ状態変更は、E: StartEncounter / Return / Abandon / Learn / Train / CloseResult / ShowTaken / CloseTaken / Inherit、S: Step / UseConsumable / Jump / Act / Escape / OpenTraining / PutOffTraining、B: StartNext / Finish、C: AdvanceAuto / EndTurn / TryPlay / TakeFreeStep です（`Next` は `?autobattle` だけが呼びます）。重ね画面の開閉（`Toggle`）は JS では app 側が持ち、記録していません。
- JS は 1 つの戦闘の開示度を、遭遇した敵の最小値 1 つにしています（C# と同じ）。敵ごとの開示度は未対応です（`depiction-multi-enemy.md` の Open points）。
- `JS/docs/EXTENSIONS.md:70-72` の「未対応」は古い記述です。`life.js` と `BattleLaunch.js` は既に直っています。移植する人が JS の文書だけを読んで二重に直さないよう注意が要ります。
