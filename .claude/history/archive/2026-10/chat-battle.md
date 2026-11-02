# HISTORY archive (chat-battle) 2026-10

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
