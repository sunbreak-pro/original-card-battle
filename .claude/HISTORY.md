# HISTORY.md - 変更履歴

> セッション単位の変更履歴（降順）。各エントリは「概要」+「変更点」。要約は `README.md` の Development History、進行状況は `MEMORY.md`。古いエントリは肥大化したら `HISTORY-archive.md` へ退避。

### 2026-09-06 - Unity 移行 Phase 3 — UGUI 最小戦闘画面 + Web トレース一致

#### 概要

Unity 6000.5.5f1 の実プロジェクト（`C:\Users\user\Unity\RPG-by-card`）で `BattleScreenView.Render` を実装し、コード生成の UGUI だけで 1 戦（勝敗・リスタート）が回る最小戦闘画面を作った。冒頭で Unity 側の CS0246（`[Test]` 未解決 ×57）を NUnit 暗黙 using の明示化で解消（known-issue 002）。固定 RNG(0) でパリティ fixture の操作列を再生し、Unity Console のトレース 18 状態が Web 版と完全一致。SystemRng でも 3 戦 97 手を例外なく完走。EditMode 57/57・`parity:check` 58/58 緑。

#### 変更点

- **修正（known-issue 002）**: `unity-port/BattleCore.Tests/*.cs` 4 本に `using NUnit.Framework;` を明示。csproj の `<Using Include>` は Unity asmdef で効かない。`docs/known-issues/002-nunit-implicit-using-unity.md` + INDEX、kit README の Caveats に追記
- **View 実装**: `unity-port/unity-project-kit/Assets/View/BattleScreenView.cs`（`npm run unity:sync` で Unity へ）。Canvas 階層をコードで構築（YAML 手書きなし、`RuntimeInitializeOnLoadMethod` で自動配置）。両者パネル / 間合い = 2 体の実距離 + 体勢（近: 前傾・遠: 後傾）/ 手札ボタン → `OnCardClicked` / ターン終了・リスタート / ログ新着順 / 結果オーバーレイ / 乱数「固定 ⇄ 実戦」切替ボタン / トレース再生ボタン
- **検証基盤**: `npm run unity:trace`（`tools/gen-trace-actions.mjs`）が fixture から `Resources/trace-actions.txt`（再生用）と `expected-trace.txt`（期待値・連番付き）を生成。View は状態ごとに `[Trace] #n ...` を Console へ出すので diff で突合できる
- **Unity 操作**: 公式 Unity CLI（`unity test` / `unity open` / `unity command eval_file|editor_play|capture_game_view`）で検証。非フォーカス Editor は Play Mode でもフレームが進まないため、eval で同期 dispatch + `EditorApplication.Step()` で描画を進めた
- **検証結果**: EditMode 57/57、parity 58/58、トレース 18/18 一致、ランタイムエラー 0。スクリーンショット 4 枚は `docs/reports/2026-09-06-unity-phase3-ugui.html`

### 2026-07-06 - Unity 以降のための作業土台（環境地固め・Logic/View・パリティ同期・キット）

#### 概要

Unity 移行 First Step の Phase 3（実 Unity + UGUI、Editor 必須の人間作業）に先立ち、Unity Editor 抜きで用意できる作業土台を `unity-port/` に整備した。まず現状把握として、リモート/ローカル差異は実質ゼロ（`main`=`origin/main`・作業ツリークリーン、`origin/feat/unity-core-port` が stale 残存のみ）と確認。計画が「新規 Windows デスクトップ想定」としていた開発マシンに既に到達済み（本セッションが Windows 11・GPU 有）である一方、dotnet 未導入・node_modules 未導入でコアを本機で回す足場が無い、というギャップを特定。ユーザー選択（フル土台を段階実施・このマシンを本番に確定）に基づき 4 段階で土台を構築し、Workflow による敵対的マルチエージェント検証で固めた。branch `feat/unity-foundation`（commit `1769631` = 土台）。

#### 変更点

- **① 環境地固め**: `npm install` で TS 依存復旧（test 204/204・build green を本機実走で確認）。`unity-port/README.md` を Mac パス（/Users/newlife・/opt/homebrew）除去し Windows 前提へ全面刷新、dotnet 導入手順（`winget install Microsoft.DotNet.SDK.10`）を明記。
- **② コード土台（純 C#・ヘッドレス検証可）**: `BattleCore/BattleStore.cs`（React useReducer 相当の Logic 層。購読型・no-op 抑制・`ToViewModel()`）、`BattleCore/IBattleView.cs`（View 契約 + `BattleViewModel` フラット射影）、`BattleCore.Tests/BattleStoreTests.cs`（パリティトレース準拠 8 件）。ストア方式は手書き reducer に確定（AppUI Redux 不採用）。
- **③ パリティ同期（TS↔C# ドリフト検出のワンコマンド化）**: `parityFixture.test.ts`（常時ドリフトガード + `PARITY_WRITE=1` で fixture 再生成）、`unity-port/tools/gen-parity.mjs`・`parity-check.mjs`（`npm run parity:gen`/`parity:check`、クロスプラットフォーム node 製）、`.gitattributes` で fixture を LF 固定（autocrlf 由来の無用差分を排除）。
- **④ 実行キット + 計画更新**: `unity-port/unity-project-kit/`（`BattleCore.asmdef`=engine-free / `BattleCore.Tests.asmdef` / Unity `.gitignore` / `BattleScreenView.cs` 雛形 / README）、`unity-port/PHASE3-KICKOFF.md`（Windows 手順 + Unity MCP 選定: IvanMurzak/Unity-MCP 第一候補・CoplayDev 代替、公式版はサブスク必須で除外。deep-web-research 調査・確度 medium）。計画書 `2026-06-28-unity-first-step-core-port.md` の決定記録更新（開発マシン=Windows 確定、ストア=手書き reducer 確定、MCP 暫定選定、作業土台節追加）。
- **検証（敵対的マルチエージェント）**: Workflow で C# コンパイル整合性・同期スクリプト・キット/ドキュメントを 3 次元並列レビュー→各指摘を敵対的検証。C# 整合性は CLEAN（指摘ゼロ。dotnet 未導入のため未コンパイル、導入後 `dotnet test` 58/58 想定＝49 unit + 8 BattleStore/View + 1 parity）。confirmed minor 2 件を修正: `parity-check.mjs` のドリフト基準を `git diff` → `git diff HEAD`（stage 時の偽陰性解消）、docs の `50/50` → 実数 `58` に統一。

### 2026-07-05 - Unity 移行 First Step — 戦闘コア C# 移植 + パリティ証明

#### 概要

Unity 移行計画書の Phase0（AI art 生成 + Live2D 仕上げ、Unity Editor スパイク）はユーザー指示で着手を試みたが、画像生成・Live2D・Unity Editor 操作の手段を持たないため自動実行不可と判断。ユーザー確認の上、子プラン（first-step）の戦闘コア C# 移植 + Web パリティ証明へスコープを絞って直行した。会話ではまず「間合いはタブ/トラックでなく実距離・体勢で表現する」方針を固め、両計画書（親戦略・子プラン）に反映（role-qa 監査で自己矛盾2件を修正済み）。その後 `unity-port/` に検証済み戦闘コア（`src/ui/battle-lab/core/`、TS）を netstandard2.1 の純 C# クラスライブラリへ移植し、実際の TS 実装を固定 RNG で走らせて生成したゴールドデータでクロス言語パリティを証明した。

#### 変更点

- **計画書更新**: 親プラン Phase0b のスパイク内容を「カード1枚めくり」から「間合い連動の位置移動+体勢差し替え」へ差し替え、Unity選定理由に⑤項追加、決定記録に傾き追記。子プラン Phase3 の間合いUIをトラック型→実距離・体勢表現に変更。role-qa 監査で子プラン決定記録の「決定」を「方針確定（実現性は0bで検証中）」へトーン修正、Non-goalsに体勢差分スプライトを仮アセット限定と明記
- **環境整備**: dotnet SDK 10.0.301 を Homebrew `dotnet`（非cask、sudo不要）で導入。旧 `dotnet-sdk` cask は sudo 必須のため断念
- **ブランチ整理**: このワークツリーが detached HEAD（旧 bake-off ブランチの残骸）だったため、origin/main（bake-off + Unity計画書2本が既に PR #16 でマージ済み）から `feat/unity-core-port` を新規作成し直し、計画書編集のみ stash 経由で引き継ぎ
- **`unity-port/` 新設**: `BattleCore/`（netstandard2.1, LangVersion 9.0, IsExternalInit ポリフィル）に Types/Constants/Combat/Cards/Enemy/BattleReducer/ViewModel/IRng を1:1移植。乱数は `IRng` 注入（`InitState(rng)`/`Reduce(state,action,rng)` の3引数、TS のグローバル `Math.random()` 依存を置換）。`Math.round` は `Math.Round(raw, MidpointRounding.AwayFromZero)` で JS 挙動と一致
- **パリティ証明**: TS実装を固定RNG（`Math.random`→0固定）で実走させ、21アクション+INITの状態遷移トレースをJSON化（手計算ではなく実行結果、`BattleCore.Tests/Fixtures/parity-fixture.json`）。`ParityTests.cs` が全ステップ・全フィールド（HP/スタミナ/間合い/ログ文言/カード順序含む）を突き合わせ
- **テスト**: TS 47テスト相当を NUnit へ移植（Combat13/Reducer21/ViewModel15）+ パリティ1件 = 50件、`dotnet test` 全 green（role-engineer実装後・Constants.cs の配列不変化修正後の両方で再確認済み）
- **検証**: role-qa 独立監査（別コンテキスト）PASS（Blocker0・Important0）。Nit2件のうち配列の `IReadOnlyList` 化は即修正、テスト件数の内訳説明は本エントリで補足

#### 次

残課題（Phase3: 実Unityプロジェクト作成 + UGUI最小戦闘画面）はUnity Editor操作が必須のため人間主体の作業。MEMORY.md 予定に記載。

### 2026-07-02 - 戦闘エンジン Bake-off 実装 + Unity 移行方針転換・計画策定

#### 概要

検証済み「間合い×スタミナ」戦闘コアを共有 `core/` として本番品質へ昇格し、@pixi/react 版と Phaser 4 版の2アダプタに同一コアを載せて肌感比較する bake-off を実装。実機プレイの結果、Phaser は好印象だが低解像度・ボタン重なり・全体的なリアル感不足が判明し、ユーザー方針として「ゲーム本体ごと Unity へ移行（アニメ・2.5D 絵柄、個人開発・低コスト先行）」へ転換。エンジン選定は Unity 移行で moot 化。Unity 移行の全体戦略と first step 実装計画（環境セットアップ含む）を策定し、bake-off 実装 + 計画書を PR #16 で main マージ。feat ブランチはローカル・リモート削除。

#### 変更点

- **共有コア昇格**: `src/ui/prototype/engine/` を `src/ui/battle-lab/core/`（types/constants/combat/cards/enemy/battleReducer）へ非コメント差分0で昇格（公平性担保）+ 表示導出を `viewModel.ts` に抽出。core 単体テスト 47件（combat/battleReducer/viewModel）
- **2アダプタ**: `adapters/pixi/`（@pixi/react、既存 `@/ui/pixi` の PixiStage 再利用、StrictMode #602 ガード）+ `adapters/phaser/`（Phaser 4 Scene、薄いストア→reducer→再描画）。vite `rollupOptions.input` に pixi/phaser 2エントリ + ルート HTML 追加
- **検証**: tsc / test203件 / build 4エントリ green、session-verifier PASS、独立 role-qa PASS-with-fixes（Blocker0・公平性 core 同一 Yes・viewModel 検証台一致 Yes）
- **方針転換（Unity 移行）**: 実機評価で Phaser 低解像度（Scale.FIT 引き伸ばし + hi-DPI 無）・ボタン/カード重なり・リアル感不足 → キャラ絵本格化のため Unity フル移行を決定。Pixi/Phaser アダプタは使い捨て、`battle-lab/core/` は C# 移植元・パリティ基準として保全
- **Unity 計画策定**: `2026-06-28-unity-migration-character-art.md`（全体戦略・費用/Live2D 等 2.5D/アニメ AI art + 商用注意/Web→C# 移植、web-researcher 4体で裏取り・出典付き）+ `2026-06-28-unity-first-step-core-port.md`（View/Logic/Data 3層・MonoBehaviour 薄く・IRng 注入で言語間決定的パリティ・Unity→Claude Code→MCP 環境セットアップ4段階・Windows 11 デスクトップ想定）
- **Git**: PR #16 を origin/main へマージ（merge `853226a`）。feat `feat/battle-engine-bakeoff` をローカル（`git branch -d`）・リモート（`git push origin --delete`）削除。worktree `../battle-bakeoff` は detached HEAD で保持（不要時に `git worktree remove`）

### 2026-06-28 - 要件正本の一本化確定 + 戦闘エンジン Bake-off 計画策定

#### 概要

並行2セッションで分岐していた v2 要件ドキュメントを照合し、`docs/realism-concept-v2`（Tier1/2/3）を正本として確定。リアルタイムタイマー/speed-chess 方向（包括版 `combat-core-redesign.md` / `realtime-turn-timer.md`）は矛盾設計のため supersede→削除に決定。次ステップとして、検証済みの「間合い×スタミナ」コアを PixiJS版と Phaser 3版の両方に載せ肌感比較する「ゲームエンジン Bake-off」計画書を策定（別セッションで実装）。本セッションは Phase 0 prep（docs→main マージ + umbrella削除 + rollup除去 + phaser導入の前提整備）を担当。

#### 変更点

- **要件正本化**: docs/realism-concept-v2 を正本確定（Tier1/2/3 = R1-0〜R1-20 の Phase 順包括要件）。包括版 `combat-core-redesign.md` / `realtime-turn-timer.md` は設計矛盾（tier%/ドロー曲線/タイマー vs 間合い連動/疲労確定減衰・スタミナ+剣気2軸）のため supersede→削除対象。RTS/speed-chess 方向は concept-v2 で廃棄済みを再確認
- **並行セッション調整**: 同一作業ツリーを共有する2チャットのブランチ取り合い + 同一「doc照合」タスクの二重化を検出。git 実行を単一セッションに一本化。docs 確定分は別チャットが bd325cf でコミット済
- **Bake-off 計画策定**: `.claude/docs/vision/plans/2026-06-28-battle-engine-bakeoff.md`（Status PLANNED）。共有コア（検証台 engine/ を本番品質で `core/` へ昇格 + `viewModel.ts` 抽出）+ Pixi/Phaser 2アダプタ。公平性ルール（数値・reducer 同一、描画だけ2通り）。勝者を Tier 1 本実装の描画基盤に昇格、検証台 DOM版は対照群として保全
- **Phase 0 prep**: ①docs→main マージ（衝突なし検証済）+ umbrella削除 ②rollup 時限爆弾除去（tech-debt #5・独立コミット）③次セッションは専用 worktree `feat/battle-engine-bakeoff`（main 分岐）で phaser 導入から実装

