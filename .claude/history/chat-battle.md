# HISTORY (chat-battle)

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

### 2026-10-21 - battle 宛ての Issue 4 件を 4 本の PR へ

#### 概要

battle 宛ての open Issue 4 件を、Issue ごとのブランチと PR にした。4 本とも build / lint / test:run / dotnet test が exit 0 で、merge は人の作業。#589 は 3 本に分ける Issue の 1 本目で、Issue は開いたまま。

#### 変更点

- **#646（PR #658）**: `MasteryProfilesTests` の `Has.Count` 1 行を `RankTraits!.Count` に。Unity の NUnit 3.5 で配列に `Count` が無く落ちていた
- **#615（PR #664）**: `unity:check` が古い写しで exit 3、未コンパイルのアセンブリで exit 4 を返す。`--allow-leftovers` / `--allow-unbuilt` で従来の 0。持ち場のフォルダを `unity-sync-plan.mjs` の `OWNED_ROOTS` に 1 か所で並べ、`unity:sync --prune` で古い写しと `.meta` を消す
- **#643（PR #665）**: 札のドラッグをフレームごとに JSON 1 行で記録する `DragTraceRecorder`（新規）と、行を作る純 C# の `DragTrace`。`DepictionPlayer.cs` と `CardView.cs` は無変更。掴んだ札は private の `_dragging` をリフレクションで読む
- **#589 の 1 本目（PR #667）**: `DepictionFrame.Enemies`（敵ごとの数値パネル・スタンス一覧・予兆・マス・斜線の向き・色見本の印）と `Cue.Unit`。`CoreScriptWriter` がコアのイベントを `BattleEvent.Unit` で振り分ける。数値パネル 3 枚以上を重ねない `StatusPanelLayout.PlaceRow`

#### 実測・知見

- **2 体の戦闘では、これまで 2 体目のイベントが 1 体目の数字と予兆に入っていた**。探索から 2 体の組に入ると、予兆の札が 2 体目のもので上書きされていた（PR #667 で台本側は直った。画面は 1 体ぶんのまま）
- **本物の Unity プロジェクトの古い写しは 0 件**、`unity:check` は 20 アセンブリで exit 0（2026-10-21）。`Exploration.Bridge` も Unity でコンパイル済みだった
- `unity:sync` の書き込みは、merge 前のコードをメインの Unity に置かないよう、4 本とも `--dry-run` だけにした

#### 次

- #589 の 2 本目（画面の部品）と 3 本目（斜線の向きと色見本）。`DepictionPlayer.cs` を触る #585 / #606 / #612 / #619 が落ち着いてから main で切る
- merge の後、メインのチャットが `unity:sync` と Unity の EditMode（#646）、ドラッグの記録（#643）を確かめる

### 2026-10-10 - battle 宛ての Issue 20 件を 20 本の PR へ

#### 概要

battle 宛ての open Issue 20 件を、Issue ごとのブランチと PR にした。実装は Issue ごとに別の作業場所で並列に進め、1 件ずつ独立したレビューと手直しを通した。同じ設計書（`battle_core_v4.md` / `battle_ui_ux_v2.md` / `SOURCES.md`）の版の行と変更履歴を触る 15 本は 1 本の鎖に積んだ。19 本は 2026-10-10 に merge 済みで、PR #435（#242）が open。

#### 変更点

- **main から切った 4 本**: #235（CS0618 を 0 件に、PR #416）/ #302（立ち絵の枠を絵の縦横比に合わせる、PR #417）/ #335（呪縛と鉤爪の文言、PR #418）/ #394（燐刃の十字受けを v4.13 に、PR #419）
- **積んだ 15 本**（merge 順）: #364 → #403 → #334 → #59 → #58 → #304 → #311 → #357 → #389 → #30 → #323 → #356 → #361 → #405 → #213（PR #420〜#434）
- **#242**: `battle-visual-v1.md` の形でカード・床・予兆の札・スタミナの札・状態チップを作り直し、ランプと床のマスは台本で渡す（PR #435）。§4〜§7 の残りは申し送りへ
- **申し送り**: 14 件を `.claude/comm/handoff/` に置いた（攻撃の目盛りの読み手 #364、習熟の札の割り当て #59、出立のツールの効果 #58、根の枯らしの膠着 #334、ターン開始の最悪の時間 #361、構えの見せ方 #323、予兆の語の注記 #357、#242 の残り 4 件など）

#### 実測・知見

- **同じ設計書を触るブランチは、main から切ると冒頭の版の行で必ずぶつかる**。20 本の総当たりで 50 組が衝突し、そのほとんどが `battle_core_v4.md` と `battle_ui_ux_v2.md` の Status の行だった
- **20 本をまとめて merge した状態でも `dotnet test` と `unity:check` は exit 0**（BattleCore 657、Bridge 287、Script 185 ほか）
- **ワークフローの一時的な作業場所からは、レーンの `handoff/` へ書けない**。申し送りは各作業場所に置かれ、後から集める必要があった

#### 次

- PR #435 の merge の後、メインのチャットで `Battle.unity` の見た目を確かめる

### 2026-09-27 - Unity の BattleCore の警告 CS8632 と CS0618 を PR へ（#209）

#### 概要

Unity が BattleCore とそのテストを nullable の文脈なしでコンパイルしていたため、`string?` の注釈が 1 つずつ CS8632 になっていた。asmdef の隣に `csc.rsp` を置いて文脈を揃え、PlayMode テスト 1 本の廃止 API を外した。PR #236 は open で、merge は人の手番。

#### 変更点

- **`csc.rsp`**: `unity-port/unity-project-kit/Assets/Core/csc.rsp`（`-nullable:enable` の 1 行）を足した。`unity-port/.gitignore:90` の `*.rsp` に当たるので `git add -f` で入れた
- **同期**: `sync-unity-project.mjs` が同じファイルを `Assets/Core/csc.rsp` と `Assets/Tests/csc.rsp` へ写す。`BattleCore.Tests.csproj` も nullable 有効なので、テスト側にも同じ文脈を渡した
- **CS0618**: `BattlePlaybackTests.cs:94` を `FindObjectsByType<MonoBehaviour>()` へ。`DemoFlowPlaybackTests.cs` と同じ形
- **起票**: #235（`Assets/View/Depiction/` に残る CS0618 4 件。design レーンの作り直しの後）

#### 実測・知見

- **asmdef の隣の `csc.rsp` はそのアセンブリにだけ効き、`Assets/csc.rsp` を置き換える**。Unity 6000.5.5f1 の `ScriptCompilationBuildProgram.exe` と `UnityEditor.CoreModule.dll` を逆コンパイルして確かめた。rsp は空白と CR / LF で区切るので CRLF でも読めるが、`#` のコメントは扱わない
- **Unity の生成した rsp（`Library/Bee/artifacts/*/*.rsp`）を Unity 同梱の csc に渡すと、Editor を起動せずに Unity と同じ警告が出る**。CS8632 は エディタ用 57（BattleCore 43、BattleCore.Tests 14）→ 0、プレイヤー用 43 → 0。新しい警告は出なかった
- **Issue の 86 件はプレイヤー用の 43 件をログが 2 回ずつ書いたものと推定**。Unity のログは同じ警告を 2 回書く（`Logs/Editor-prev.log` 291〜314 行）
- **CS0618 は Issue の見立てより多い**。`FindFirstObjectByType` も廃止で、`BattleBootstrap.cs:49` はプレイヤー用のビルドにも入る
- `npm run build` / `lint` / `test:run` / `dotnet test` / `unity:sync -- --dry-run` は exit 0

#### 次

- メインのチャットが `npm run unity:sync` の後に Console で CS8632 が 0 件かを見て、Unity リポに `csc.rsp` 2 つと `.meta` をコミットする
