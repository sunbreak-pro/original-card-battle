# HISTORY (chat-battle)

### 2026-11-02 - battle 宛ての Issue 4 件（#580 / #724 / #725 / #755）を 4 本の PR へ

#### 概要

4 件を、Issue ごとに origin/main（4d28b7b8）から切ったブランチと PR にしました。実装は別の作業場所で Issue ごとに 1 体が受け持ち、別の 1 体が見直しました。4 本とも build / lint / test:run / unity:check / dotnet test が exit 0 です。4 本とも `Closes` で、merge は人の作業です。

#### 変更点

- **#580（PR #782）**: `CoreScriptWriter.cs`（着手時 1497 行）を partial の 7 ファイルに分けました。最大は `.Apply.cs` の 414 行です。振り分け `ApplyEvent` は 335 行から 98 行になり、デモの語彙の case は `ApplyDemoVocabulary` へ移しました。試験の名前の一覧は前後で同じ 512 行です
- **#724（PR #783）**: `BattleToolAmounts.StartStamina` を `StartStaminaBonus` に、`OpeningGuard` と `CarriedTools.ScaleGuard` を `FirstHitGuard` にしました。`Expedition` `DepartureBuilder` `BattleLaunch` `DepartureCarry` の呼び出しは名前つき引数にしました。探索側の 2 ファイルも、台帳に持ち主が無いので同じ PR に入れています
- **#725（PR #784）**: `CoreText` の `WhenWord` と `AttackBonus` は、文の無い `StanceWhen` で例外を投げます。今ある札と敵の文は変わりません。試験を 1 件足しました
- **#755（PR #785）**: 全敵の組み合わせの試験を、先頭 2 体ごとの 153 case に分けました。戦う並び 882 と戦闘 1764 回はそのままです。dotnet での最長は 54 秒から 1.79 秒になりました
- **申し送り 1 件**: 敵の構え「穂先を据える」の文に、間合い 1 以上の条件が入らない件（`2026-11-02-battle-stance-gap-at-least-words.md`）

#### 実測・知見

- **`npm run unity:check` は origin/main でも 4 本でも exit 0** でした。10-28 と 11-01 の exit 1 は解消しています
- Mono と dotnet の速さの比は、#755 の試験で約 3.7 倍でした（202 秒 / 54 秒）。dotnet で 48 秒を超える試験は、Unity の 180 秒の上限に当たる見込みです

#### 次

- merge の後、メインの Unity で #785 の EditMode を回します。#782 の新しい 6 ファイルの `.meta` は、Unity のリポで保存します

### 2026-11-02 - #585 DepictionPlayer を partial で分ける

#### 概要

3024 行の `DepictionPlayer.cs` を同じクラスのまま 7 ファイルに分け、204 行の `PlayCue` を 90 行にしました。PR #759 が open です。挙動、プレハブ、Inspector の欄の名前は変えていません。

#### 変更点

- **分割**: `DepictionPlayer.cs` と `.Layout.cs` `.Flow.cs` `.Beats.cs` `.Cards.cs` `.Hand.cs` `.Drag.cs` に分けました。最大は `.Beats.cs` の 569 行です。行はスクリプトで丸ごと動かし、元のファイルと行を突き合わせました
- **`PlayCue`**: 長い `case` を `.Beats.cs` の関数へ出しました。`yield` を持たなかった経路（`GuardReset`、スタミナの消費）は今も `yield` を通りません
- **Issue の案と違う点**: `Gate` `DebugStep` は `.Flow.cs`、敵ごとの部品は `.Layout.cs` に置きました
- **検証**: `build` `lint` `test:run` `unity:check` `dotnet test`（4038 件合格）は exit 0 です。別のコンテキストの 3 体のレビューでも、残った指摘はありませんでした

#### 次

- メインの Unity で PlayMode テスト 3 本と `Battle.unity` の 1 戦を通し、新しい 6 ファイルの `.meta` を Unity のリポで保存します
- `.claude/docs/art_document/briefs/` の `DepictionPlayer.cs:<行>` は分割の前からずれていて、持ち主が別の担当なので直していません

### 2026-11-01 - #589 の残り 2 本と #715 / #712 / #714 を 5 本の PR へ

#### 概要

battle 宛ての 4 件を、origin/main（9e2a44e2）を基準に 5 本の PR にした。#589 は 2 本目と 3 本目を分け、同じファイルを触る PR の上に積んだ。5 本とも open で、merge は人の作業。build / lint / test:run / dotnet test は 5 本とも exit 0。

#### 変更点

- **#714（PR #733）**: `battle_core_v4.md` §3.2 の表の見出しから「battle レーンの叩き台」を外し、決定の 1 文を足した
- **#712（PR #734）**: `battle_ui_ux_v2.md` §7 の手記の列を `dungeon_exploration_v4.md` §6.11 に合わせ、§6.4 の技の名前を「払い斬り」に直した
- **#715（PR #738）**: 被弾の数字は #688 で割合になっていたので、残りの `GainGuard` と `Reserve` を `FloatNumberSpec` の割合へ移した
- **#589 の 2 本目（PR #749、Refs）**: 敵ごとの人型・数値パネル・予兆の札・立ち位置、`Cue.Unit` の振り分け、札のずらし、敵ごとの対象の枠と落とす先、2 体目以降の撃破（§5.15 の順 5）、敵ごとの開示度。#738 と #734 の上に積んだ
- **#589 の 3 本目（PR #750、Closes）**: 敵ごとの斜線の向きと交差、色見本（予兆の札 22 px、HP の行とスタンス枠 16 px）。#749 の上に積んだ
- **申し送り 6 件**: 語が予兆の札に届く件、Tab と予兆の札のホバー、探索から敵ごとの開示度を渡す件、敵 1 体で開示度 0 の狙うマスが出る件、CLAUDE.md の段落、斜線を引いて消す演出

#### 実測・知見

- **`npm run unity:check` は exit 1**。162 件はすべて `View/Exploration`（160）と `View/Trial/Bridge`（2）で、`View/Depiction` と `BattleCore` は 0 件。手元の Library がリポジトリの新しいファイルを知らないために出る。#589 の完了条件のこの 1 行は満たせていない
- **#589 の撃破の拍は台本に足さずに済んだ**。書き手が敵ごとに `CueKind.Defeat` と `Cue.Unit` を書いていて、View がほかの敵の立ち具合をフレームから読む。300 + 300 ms は `EffectPlan` の外なので、実測のログは計画より 600 ms 長く出る
- **複製はシーンの部品の Awake より前に取る**。各 View が組み立てた子を複製に写らないフィールドで持つので、動いている部品を複製すると子が二重にできる

#### 次

- メインのチャットが PR の merge の後に `npm run unity:sync` と Unity Editor で 2 体の画面を確かめる。部品が二重にならないこと、灰のパネル、札の滑り、交差した斜線、16 px の色見本、スタンス枠のずれを見る

### 2026-10-28 - battle 宛ての Issue 12 件を 12 本の PR へ

#### 概要

メインから渡された 12 件を、Issue ごとに origin/main（ddf2f320）から切ったブランチと PR にした。大きい 5 件（#584 / #514 / #605 / #455 と #57 / #582）は別の作業場所のサブエージェントに任せ、小さい 6 件は battle の作業場所で進めた。12 本とも build / lint / test:run / dotnet test は exit 0。#455 と #57 は残りが Editor と決定待ちなので `Refs`、ほかの 10 本は `Closes`。merge は人の作業。

#### 変更点

- **#654（PR #682）**: `PileList.TabLabel` が知らない種類で例外を投げる。`PileListTests` を足した
- **#599（PR #684）**: `CardCatalog.Learned.cs` の見出しを、精鋭（不退の槍竜 / 100% 版で決める）と探索の一層〜七層に直した。コメントだけ
- **#576（PR #686）**: 既定の敵 id を `BattleLaunch.DefaultEnemyId` と `Enemies` から読む。`BattleBootstrap.ClearHandIn()` を足した。探索の `UniqueCards.cs` の直書きと `HandIn(null)` の置き換えは申し送り
- **#579（PR #689）**: `battle_core_v4.md` §11 の表を今のファイル名と型（`TurnLoop.cs`・`Enemies.cs` / `EnemyAi.cs`・`Traits.Evaluate`・`EnemyDef.Rank`）に直し、ランダムのデッキの種を `Debug.Log` に出す
- **#611（PR #692）**: `battle_ui_ux_v2.md` §6.4 の書体の段を 5 つのファイルと警告の扱いに合わせた
- **#606（PR #695）**: `DepictionPlayer.ApplyFrame` が `BackdropView.Show(frame.Corner.Floor)` を呼ぶ。1 層のグラデーションは下敷きとして残した
- **#605（PR #685）**: スタンスを状態チップから外し、`StanceRow` に効き方と減り方を持たせ、スタンス枠のホバーで説明パネルと「+n」の一覧を出す。`battle_ui_ux_v2.md` §2.1 / §3.7 も直した
- **#584（PR #691）**: `TurnLoop` を partial の 9 ファイルに分け、`Resolve` を段ごとの関数にした。公開の入口と試験台の出力は前後で同じ。最大 433 行
- **#582（PR #694）**: `Criteria.cs` の §13 の線を定数 19 個にし、`Compute` を 171 行から 33 行にした。試験台の出力はバイト単位で同じ
- **#514（PR #696）**: Web 版の使われていないファイル 16 本と宣言約 100 個を消した。分類 D（テストだけが使う）も消したので、テストは 204 件から 181 件（Issue の DoD どおり件数を本文に記載）
- **#455（PR #688、Refs）**: 被弾の浮かぶ数字（§5.5。Guard は盾と数、HP はハートと数を小札に載せる）
- **#57（PR #693、Refs）**: 二属性の上限 1545 ms を 3 つの速さで確かめる試験と、`battle_ui_ux_v2.md` §5.3 / §8 の追従
- **申し送り 5 件**: #576 の探索側の 2 行、#605 の試遊版 `Stances.cs`、#584 の `unity-port/README.md`、#514 の CLAUDE.md の Mastery の行、#514 で消したファイルを名指しする文書 9 本

#### 実測・知見

- `npm run unity:check` は origin/main でも exit 1（試遊版の 7 アセンブリで 86 件）。12 本とも、このリポの組み立て単位と `Assembly-CSharp` は 0 件
- 同じ文書の冒頭の Status に、#605 / #611 / #57 の 3 本が 1 項目ずつ足す。後から merge する側で両方の項目を残す
- `isolation: worktree` のサブエージェントは、同時に 5 体を起こすと 1 体が作業場所の作成で失敗することがある。1 体が終わってから起こし直せば通る

### 2026-10-28 - Unity 移植の戦闘まわりの残り 4 件を 4 本の積んだ PR へ

#### 概要

メインから渡された 5 件のうち 4 件を、Issue ごとのブランチと PR にした。4 本は同じファイルを触るので、#675 → #676 → #677 → #678 の順に積んだ。#575 は状態の説明の数字が BattleCore の定数待ちで `Refs`。#589 の 2 本目と 3 本目は台本の型が 2 通りあるため着手せず、申し送りにした。merge は人の作業。

#### 変更点

- **#588（PR #675）**: 特性のスタミナ +1 の合図を `CoreScriptWriter` が取り置き、全ての面の後（ドローの前、別のスタミナの拍の前、出来事が閉じるとき）に出す。小手打ちは「支払い、特性の合図、一撃、+1」。特性のスタミナを持つ 8 枚をデータから選んで並びを確かめる
- **#587（PR #676）**: 敵が撃ってから自分で動く 8 つの行動で、移動を撃った出来事に入れ、`EffectPlan.EnemyMoveFollows` で戻りを出さず結果の拍 + 80 ms から移動する。予兆の消え方は移動と並行。押す・引くは今までどおり戻る。最長は退き撃ちの Guard 一部吸収で 1640 ms
- **#577（PR #677）**: `RangeSide`、近 / 遠の欄、`CoreText.SideOf`、立ち位置 2 つと `SnapRange` / `SwitchRange` を消した。固定台本 `TurnSliceScript` は 6 マスの床に立ち、後ろ跳びで N 1 → N 2。全ての台本源が床を持つので、マスを持たない `RangeSwitch` は `CellsOf` が止める
- **#575（PR #678）**: 属性の語を `CardFaceText.AttributeWord`（攻撃・防御・スキル・スタンス）に寄せた。予兆の種別の語は `OmenLabel.ToText()` を間合いなしで呼んで BattleCore の 1 か所にした。特性の条件は `CardFaceText.ConditionWords` の表（短い形・枠・文・用語）から引く。枯らしの説明に回復の下限

#### 実測・知見

- **`npm run unity:check` は origin/main でも exit 1**。Unity のプロジェクトに trial の作業場所（`feat/main-trial-port-20261022`、未 push）の `Assets/View/Trial/` が同期されていて、7 アセンブリで 86 件。この 4 本が触った `Depiction.*` と `Assembly-CSharp` は 0 errors
- trial の作業場所は、#667 より前から分かれ、敵 2 体以上の台本の型を別の形（`Enemies` が `UnitFrame`、`Omens`、`FloorEnemy`、`EnemyHatch`）で持つ。#667 の `EnemyFrame` とコンパイラの上でぶつかっている
- Unity の NUnit 3.5 では `Does.Not.Contain(列挙値)` がコンパイルできない（文字列だけ）。`Has.No.Member` を使う

#### 次

- 申し送り 2 件: `2026-10-28-battle-589-two-multi-enemy-models.md`（どちらの型に寄せるか）、`2026-10-28-battle-575-status-numbers-core-constants.md`（BattleCore に状態の定数を足す）
- merge の後、メインのチャットが `unity:sync` と、#577 の Gate（固定台本が床のマスに立つ）、#587 と #588 の見た目を Unity で確かめる
