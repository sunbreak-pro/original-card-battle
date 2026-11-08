# HISTORY (chat-dungeon)

### 2026-11-04 - 探索の設計書: 暗き死海、札 64 種、崩壊の暦と 3 つの領域（#920 / #900 / #887）

#### 概要

dungeon 宛ての open な Issue 3 件を PR にした。PR #921（#920）と #922（#900）は origin/main から切り、PR #924（#887）は `seven_layers_v4.md` を一緒に書くので #921 の上に積んだ。3 本とも open で、merge は人の作業。`dotnet test` は、origin/main から続く `MasteryProfilesTests` の 1 件だけが赤い（札の表の C# の追従は #898）。

#### 変更点

- **#920（PR #921）**: 探索コアの層の表とテスト、継承の照合用の記録 2 つ、`TraceLinesTests` の五層の名前を暗き死海にした。`js-fuzz.json` は試遊版の JS の層の名前だけを替えて作り直した。画面側の名前は design の持ち物なので申し送りにした
- **#900（PR #922）**: `encounters_and_rewards_v4.md` を v4.11 の札に合わせた。門竜と長竜は札 1 枚で、選ばずに習得する。四層の代役の門竜には札の山の 1 枚を足して期待値 7 を保った。札の山を 19 枚の表にし、山が尽きる見込みを約 20 回に数え直した
- **#887（PR #924）**: `seven_layers_v4.md` を v5.0 に書き直した。暦 360 日、王は 60 日で戻り倒れている日は暦が減らない、クリアは三体が同時に倒れている状態、八層と 3 つの領域、降り口、1 年で 9〜16 代の見積もり。`dungeon_exploration_v4.md`・`miasma_and_battle_v4.md`・`tools_and_consumables_v4.md` も合わせ、瘴気の品 3 つを日数に効く品に替えた。台帳の「未反映」を外した
- **申し送り**: 画面側の層の名前（design）、クリアの条件の読み方（main）、出会いと報酬の文書の暦への追従（dungeon）の 3 件。#888 には C# に写す中身をコメントした

### 2026-11-03 - 継承の 3 件: 名前の書き換え、踏破は生存者にしない、セーブの版の移行（#105 / #108 / #109）

#### 概要

dungeon 宛ての open な Issue 3 件を PR にした。PR #864（#108）と #874（#105）は origin/main から切り、PR #866（#109）は同じ設計書の版の行を書くので #864 の上に積んだ。3 本とも open で、merge は人の作業。

#### 変更点

- **#108（PR #864）**: `Inheritance.RecordSurvival` で、踏破した生（`LifeEnding.Completed`）を生存者にしないようにした。前の生存者はそのまま残し、札だけの痕跡は残す。終わりの画面の行と `ClearedTraceNote` を直した。`dungeon_exploration_v4.md` §7.8 に、生存ルートと死亡を 3 つの型の生で比べた結果を書き、同じ 3 つの生をテストで通した。浅い生では生存ルートが勝ち、第 3 層より深い生には死亡でしか渡せない 3 段目のカードがある
- **#109（PR #866）**: `InheritanceCore/LineageMigrations.cs` を足し、古い系譜を版ごとの手順で順に変換する。新しいビルドの版と手順の無い版は読まずに退避する。セーブの入れ物の `v` も確かめるようにした。生の途中は変換せず、版が違えば「再開できない生」の流れに乗せる。`TraceLedger` に次の世代番号を持たせる依頼は取り下げ、説明と §7・§7.9 に書いた
- **#105（PR #874）**: 継承の間の名前の下に入力欄（`Meta/HeirNameField.cs`）を足した。判が押された後に触れるようにし、IME は手記のメモ欄と同じ仕組みを使う。`Inheritance.StartLife` に名前を渡し、`ChosenHeirName` が空白と制御文字を除いて 8 文字で切る。長い名前は名前の行で文字を小さくする
- **`TRIAL-PORT.md`**: 3 件とも試遊版の JS と違うので、違いの節に 1 行ずつ足した

#### 実測・知見

- 3 本とも `npm run build` / `npm run lint` / `npm run test:run` / `dotnet test UnityCorePort.slnx` / `npm run unity:check` が exit 0
- JS と照らす乱数の操作列は、最初の踏破の後を比べるのをやめた。421 手のうち 139 手が比べられなくなる。踏破を JS の規則に戻すオプションを本番のコードに残すより、比べる範囲を絞るほうを選んだ
- Write と Edit の道具が、C# の文字列にある ` ` を生の文字に変えてしまう。生の U+2028 は C# では改行なのでコンパイルが落ちる。Python のスクリプトで書き直した
- 空きメモリが 6GB を切っていたので、レビュー役のエージェントは起動せず、差分は自分で読み直した

#### 次

- 3 本の merge と、入力欄の見た目と手触り、保存した生からの再開の確認は、メインの Unity での作業
- 踏破の文と世代の数は #111 が入れる

---

### 2026-11-01 - 探索の 5 件: 遺産を選ぶ画面、消耗品の入れ替え、イベントの解決、報酬の分岐、敵に結んだメモ（#717 / #719 / #718 / #723 / #713）

#### 概要

dungeon 宛ての open な Issue 5 件を PR にした。PR #735（#717）→ #739（#719）→ #745（#718）→ #747（#713）は同じファイルを触るので積み、PR #746（#723）だけ origin/main から切った。5 本とも open で、merge は人の作業。

#### 変更点

- **#717（PR #735）**: `ExplorationScreenView.ShowHeritage` と `ExplorationBootstrap` の選択の持ち方を足した。痕跡のノードの「まだ出ない」の行と設計書の同じ記述を消した。札の選択と同時に待つときは遺産が先
- **#719（PR #739）**: 純 C# の `Bridge/ConsumableSwaps.cs` と `ShowSwap` を足した。札を選ぶ画面が先で、品はその後に 1 つずつ出す
- **#718（PR #745）**: `ExplorationReducer.ApplyEvent` を切り出し、`RunStart.ResolvesEvents` を足した。探索の画面だけが入れる。解決すると `LifeJournal` がその層の出来事を開示度 1 にする。受けなかった出来事も 1 にする。重い宝箱の中身は戦闘の報酬と同じ行き先へ入れる
- **#723（PR #746）**: `BattleRewards.Roll` の先頭で `IsCombat` を見て、表に行の無い種類は `RollCombat` が例外で止める。`ExplorationText.IsCombat` を消した
- **#713（PR #747）**: `MemoNote.EnemyId` と `JournalMemo.About` を足し、`EnemyJournal.Page` の末尾に「自分のメモ」の行を出す。敵を持つメモもメモの頁に残す
- **申し送り**: 旧い戦闘シーンの手記に頁を渡す経路が無いので、`handoff/2026-11-01-dungeon-battle-journal-memo-path.md` を main へ回した

#### 実測・知見

- 5 本とも `dotnet test UnityCorePort.slnx`、`npm run build`、`npm run lint`、`npm run test:run`、`npm run sources -- --check` が exit 0
- `npm run unity:check` は exit 1。Unity の前回コンパイルの引数が #613 の asmdef の参照より古いため。参照を足した検査は全アセンブリが通った。メインの Unity を 1 度開くと揃う
- 上の検査が `Exploration/Tests` の `Assert.Multiple` を見つけた。Unity の NUnit に無く、`dotnet test` では通ってしまう。`Bridge/Tests` には代わりの部品がある
- `Trial.Bridge.Tests` の `RecorderTests` は `ExplorationSession` の公開メンバーを全部検査する。試遊版に関係ない公開プロパティを足すと落ちるので、`ResolvesEvents` は `internal` にした

#### 次

- 5 本の merge と、Unity Editor での見た目と手触りの確認はメインの作業

---

### 2026-10-21 - 探索の 3 件: 身代わりへの切り替えのログ、拾った消耗品、札を選ぶ画面（#649 / #651 / #626）

#### 概要

dungeon 宛ての open な Issue 3 件を、それぞれ origin/main から切ったブランチで実装して PR にした。PR #663（#649）、#666（#651）、#668（#626）はどれも open。

#### 変更点

- **#649（PR #663）**: `ExplorationBootstrap` が身代わりのカードへ切り替える 2 か所で `Debug.LogWarning` を出し、切り替えを private の欄に持つ。`EncounterBattle.TryFinish` を足して View が `GameResult` を読まないようにし、`ViewRulesBoundaryTests` の許可の一覧から 1 行外した。`BattleHandIn` は開示度が足りないと例外にする。`UniqueCardsTests` が代役でないボスと精鋭の全てに表の行があることを確かめ、行を消すと落ちることも実測した
- **#651（PR #666）**: `ReliefKind` に薬草・濃い干し肉・忘れ水・標の石を足し、`ExplorationReducer.UseConsumable` が効きを持つ。拾う操作は `RunLoadout.Take` / `ExplorationReducer.TakeConsumable`。`Expedition.Return` が拾った品を空いた枠へ入れ、入らない品は `WaitingConsumables` に残す。入れ替えは `SwapConsumable`、諦めは `DiscardConsumable`。標の石は枠に入るが使えない
- **#626（PR #668）**: 純 C# の `Bridge/CardPicks.cs` が選ぶ画面の中身を作り、`ExplorationScreenView.ShowPick` が戦闘の `CardView` をコードで作って並べる。訓練の候補はデッキの札の種類にした
- **申し送り**: 入れ替えを選ぶ画面を別の Issue にするため、`handoff/2026-10-21-dungeon-consumable-swap-screen-651.md` を main に回した

#### 実測・知見

- 3 本とも `dotnet test UnityCorePort.slnx`、`npm run unity:check`、`npm run build`、`npm run lint`、`npm run test:run` が exit 0
- `ExplorationBootstrap.cs` の `Render()` は #663 と #668 が隣の行を書き換えるので、あとから merge する側で取り込みが要る
- 札のプレハブが無いシーンでも、`CardView` は地の画像だけ渡せば残りの部品を自分で作る

#### 次

- 3 本の merge と、Unity Editor での見た目の確認はメインの作業

---

### 2026-09-27 - 探索の画面を Unity に作る（#101）

#### 概要

探索の画面を仮の見た目で Unity に作った。画面は値を計算せず、純 C# の組み立て役が探索のコアから作った値を置くだけにした。PR #238（open）。

#### 変更点

- **設計書**: `dungeon_exploration_v4.md` に §6「探索の画面」（出す値、ノードの段階開示、1 歩の予告、階層の切れ目、「この生を終える」、手記のドロワー、仮の戦闘、終わりの画面、Unity への写し方）と、§5 の判断 6・7（最後の層で刻限が尽きたとき / 生を終えられる時機）を足した
- **コア**: `RunPhase.Survived` と `ExplorationReducer.EndLife` を足した。刻限も瘴気も動かさない。画面が回復量を読めるよう `RestHeal` / `InterludeHeal` を公開した
- **組み立て役**（`Assets/View/Exploration/Script/`）: `ExplorationSession` が操作をコアの遷移へ渡し、`ExplorationScreenBuilder` が 1 フレームを作る。1 歩と降下の予告は、不変の状態にコアの遷移を試した結果なので実際の結果と一致する。敵の情報は差し込み口 `IEnemyIntel` から受け取り、#102 / #107 まで「？」
- **手記のドロワー**（`Assets/View/Journal/`）: 頁の型 `JournalBook` / `JournalPage` と画面。探索の型に依存しないので戦闘の画面からも使える
- **画面**: `ExplorationBootstrap` を空の GameObject に付けるとキャンバスまで組む。仮の色は `ExplorationPalette` だけ。`BattleTheme` と `Depiction/` には触っていない
- **写しと器**: `sync-unity-project.mjs` が `DungeonCore` / `DungeonContent` / 探索の画面 / 手記を写す。`Exploration.Script(.Tests)` と `Journal.Script` を `UnityCorePort.slnx` に載せた
- **食い違い**: 手記のタブ（`battle_ui_ux_v2.md` は 3 つ、`CAMP_FACILITIES_DESIGN.md` は 5 つ）を #234 に起票し、3 つに従った

#### 実測・知見

- レーンでは Unity を動かせないので、Unity 6000.5.5f1 の DLL と Unity プロジェクトの `Library/ScriptAssemblies/`（UnityEngine.UI / InputSystem）を読み取り専用で参照する使い捨ての csproj を作業用フォルダに置き、View のコンパイルを旧入力 / 新入力の両方で確かめた。テストは `-p:LangVersion=9.0` で Unity と同じ C# 9 でも通した
- 第三者レビュー（PASS）で 3 点を直した: 休息で線を越えても最大スタミナが上がる場合の誤った警告、降りた先の入口での瘴気死に確認が無かったこと、画面側が文言を持っていたこと。札の暗幕の上に持ち物と「この生を終える」を置き、休憩中に浄化の香で降下を救えるようにした
- 次の層の入口は着いた時点で踏んだことになるので、降下は 1 歩ぶんの瘴気をすぐ払う。予告もコアの `Descend` を試して出す

#### 次

- PR #238 は open。merge と Unity Editor での手触りの確認はメイン（`npm run unity:sync` → 空のシーンに `ExplorationBootstrap`）
- `SOURCES.md` に探索の画面と共有の手記のドロワーの行を足すか、`CLAUDE.md` に探索の画面の 1 行を足すかはメインの判断
- 戦闘をつなぐのは #99、敵の情報は #102 / #107、生存ルートの受け継ぎは #108

---

### 2026-09-23 - 層ごとの戦闘の場のマス数と開始の間合い 3（#168）

#### 概要

戦闘の場のマス数を層ごとに決め、`seven_layers_v4.md` §2.4 に置いた。開始の間合いは 3 が既定。開始の位置を変えるツールを、v4.3 の間合い N に合わせて「間合いの履」に読み替えた。PR #173（open）。

#### 変更点

- **層ごとのマス数**: 通常戦闘は 一〜三層 6 / 四〜六層 7 / 七層 8。精鋭は層の値 + 1（上限 8）、階層ボスは 8、死亡地点の痕跡は層の値。場は「5 + 出る敵の大きさの合計」より狭くしない
- **こうだいさんの決定（2026-09-23）**: 深い層ほど広くする向きで確定。5 マスはマス数の最小値として残す。ただし層の表は 6 から始める
- **間合いの履**: `toma_no_kutsu`（遠間の履）→ `maai_no_kutsu`。開始のマスが 2 → 1 になり、開始の間合いが 3 → 4。C# は `ItemEffect.StartFar` → `WiderStart`（トークン `wider_start`）、テストで量 1 を固定
- **`concept_v3_open_items.md`**: ボスの適応案の観測を「間合い 0 での攻撃」に読み替えた
- **`SOURCES.md`**: 登録漏れだった `tools_and_consumables_v4.md` の行を dungeon で 1 行足した。`npm run sources -- --check` が通るようになった

#### 実測・知見

- 開始の間合い 3 とプレイヤーの開始のマス 2 のままだと、5 マスの場では敵の立つマスが残らない。5 マスで開始の位置をどう置くかは battle レーンの #169 に残した
- マス数を広げても、プレイヤーの後ろは常に 1 マス。広さで変わるのは敵の後ろの余白だけで、6 マスなら敵は端を背にして始まる
- 整形のフックが Edit のたびに表全体を並べ直す。他レーンの持ち物（`SOURCES.md`）は Bash で 1 行だけ差し込み、差分を 1 行に抑えた
- `battle_core_v4.md` はまだ開始の間合い 2・1 マスに 2 体のまま。#169 が追っているので「正本の食い違い」の Issue は立てず、PR 本文にどちらに従ったかを書いた

#### 次

- PR #173 は open。merge は人の手番
- マス数を戦闘へ渡すのは #99。`SevenLayers.cs` にマス数を足すのもそこで行う
