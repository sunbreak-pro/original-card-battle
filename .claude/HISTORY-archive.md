# HISTORY-archive.md - 変更履歴アーカイブ

> `HISTORY.md` が 5 件を超えた際に退避した古いエントリ（降順）。最新は `HISTORY.md`。

### 2026-09-12 - 企画書 v3「生と継承」で設計ルールを上書き + アーマー凍結

#### 概要

life-editor Note「ゲーム設計(chat GPT)」（27 節の企画書）を読み取り、既存の設計ルールをその内容で上書きした。正本は新設の `docs/vision/concept-v3.md`。ライフ制・エクストラクション・手記の死越えを廃止し、刻限・衰弱・生存ルート・遺産・図鑑の消失を核にした「生と継承」ループへ。アーマー（AP / 装備耐久）は「戦闘が複雑化するので、まずアーマー無しで難易度と調整を測る」ため凍結。コードは触っていない（`unity-port/` と `battle-lab/core/` にアーマーは元々無く Guard のみ）。

#### 変更点

- **正本新設**: `docs/vision/concept-v3.md`。企画書を 12 節に再編。§10 に既存ルールとの対応表（置換 / 凍結 / 再解釈 / 廃止 / 継続）、§12 に未確定 19 項目（企画書 §27 の 15 + 上書きで生じた 4）
- **構想**: `vision/core.md` を全面改稿（Core Value を 5 要素 + 生と継承の 6 本、凍結欄）。`concept-v2.md` / `2026-06-11-gap-analysis.md` に SUPERSEDED バナー
- **要件**: `requirements/tier1-core.md` v6（R1-1〜R1-17、旧 v5 との ID 対応表つき）/ `tier2-support.md` v3 / `tier3-experimental.md` v2（凍結表に解凍条件、廃棄一覧）
- **総合・拠点設計**: `game_design_master.md` V4.0（英語 801 行 → 日本語で全体像だけ）/ `CAMP_FACILITIES_DESIGN.md` V5.0（5 施設 + Journal → 継承の間 / 出立 / 図鑑）
- **旧ルールのバナー（本文は残す）**: FROZEN = `ap-equipment-system.md` / shop / blacksmith / sanctuary / `EQUIPMENT_AND_ITEMS_DESIGN.md`。SUPERSEDED = `return_system_design.md` / `DESIGN_CHANGE_PLAN_lives_system.md` / guild。PARTIALLY SUPERSEDED = `battle_logic.md`（エネルギー・AP 節）/ `dungeon_exploration_ui_design_v3.0.md` / journal plan。STALE SNAPSHOT = `PROJECT_OVERVIEW.md`
- **索引・規約**: `.claude/CLAUDE.md` Game Loop Flow を新ループへ。`docs/INDEX.md` の壊れたリンク 2 件（`combat-core-redesign.md` / `realtime-turn-timer.md`）を修正
- **レポート**: `docs/reports/2026-09-12-concept-v3-overwrite.html`（Artifact 発行、life-editor Note `note-d144ed88` に控え）
- **未対応（判断待ち）**: §12 の未確定項目、ステージ構造、凍結した個別施設設計書 4 本（約 4,800 行）の archive 移動可否。README に Development History 節が無いため README は未更新

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

### 2026-06-27 - 戦闘プロトタイプ（間合い×スタミナ最小検証台）実装・マージ

#### 概要

「リアル性コンセプト v2」戦闘主柱（間合いの読み合い × スタミナの消耗）が遊びとして成立するかを本実装前に検証する throwaway プロトタイプを `src/ui/prototype/` に隔離実装し、PR #14 を origin/main へマージ。当初タスクが参照した計画書が実在しなかったため concept §1 / tier1 R1-5・R1-6 / タスク本文から計画書を再構成して実装。役割分担（role-engineer 実装 → session-verifier → role-qa 独立監査）で進め、role-qa PASS-with-fixes。ユーザー実機プレイで「ゲーム性はかなり面白い」と評価。

#### 変更点

- **隔離実装（既存無改変）**: `src/ui/prototype/` に engine（types/constants/combat/cards/enemy/battleReducer）+ UI（PrototypeBattle/DistanceTrack/CombatantPanel/HandView/BattleLog/ResultOverlay）+ `prototype-battle.css`（`.prototype-battle` スコープ）+ ルート `prototype.html` 起動口。既存トラッキングファイルの差分ゼロ、`useBattleOrchestrator`/`BattleScreen`/不可侵 deck は無改変・非 import。デッキ操作は自前 Fisher-Yates
- **メカニクス**: 間合い 近/中/遠（相性ベース・固定強弱なし、diff 0/1/2→×1.0/0.5/0.15）、スタミナ MAX20・回復 近+1/中+2/遠+3・閾値8未満で確定威力減衰（floor 0.4、確率ミスではない）、剣士6カード、リーチ型の敵1体（中=キルゾーン、矯正技 shove）。敵フェーズは純 reducer の END_TURN 内で同期解決（StrictMode 安全）
- **計画書再構成**: `.claude/docs/vision/plans/2026-06-27-battle-prototype-range-stamina.md` を新規作成（Steps/Files/Verification + チューニング所見）。当初参照のファイルが不在だったためユーザー承認のうえ復元
- **検証ゲート**: `npm run build` 緑 / `test:run` 156件緑（プロトタイプ純関数 33件）/ `npx eslint src/ui/prototype` 0 エラー（全体 lint の16エラーは origin/main 既存・本変更外）
- **balance 調整（sim 由来）**: ヘッドレスで4戦略を検証し初期値（敵HP42・穂先6）は勝ち筋ほぼ無しと判明 → 設計意図を保ち `reach_thrust` 6→3（遠を回復の逃げ場に）・`ENEMY_MAX_HP` 42→38。ゴリ押し/カイトは負け・賢い立ち回りで勝てる帯を維持。最終バランスは実機プレイで詰める前提
- **Git**: PR #14 を origin/main へマージ（merge `9b88536` / feat `c77907c`）、マージ済 feat ブランチをローカル・リモート削除。プロトタイプは throwaway（本番非流用）

### 2026-05-23 - PixiJS Phase 1 基盤実装（ハイブリッド描画レイヤー導入）

#### 概要

既存 DOM/CSS バトル演出を温存したまま、`.battle-field` 上に透過 PixiJS キャンバスを重ねる「ハイブリッド描画」基盤を導入。lead-pipeline 重ティアのフルチェーン（session-manager START → role-pm → role-engineer → session-verifier → role-qa + security-reviewer 並列）で実施。Step 0 では @pixi/react v8 の未解決 issue #602（StrictMode 二重マウント時の WebGL context stale）を A 案（React.lazy + mountedRef post-commit ゲート）で構造的回避。QA 判定 PASS-with-fixes（Blocker0 / 実害ある Major0）。実機検証で起動・オーバーレイ表示・クリック貫通・StrictMode 往復・リサイズ追従を全て OK 確認。

#### 変更点

- **計画書（コードレベル版）作成**: `docs/vision/plans/2026-05-17-pixijs-phase1-code-level.md` を新規作成し旧抽象版 `pixijs_phase1_foundation.md` を超越。§0B で旧計画を 6 観点監査し致命的誤り 3（particle-emitter 死亡・z-index:5 誤り・StrictMode #602 未考慮）+ 要修正 4 を是正。実装完了でファイルを `.claude/archive/` へ移動・Status=COMPLETED
- **依存追加**: `pixi.js@^8.18.1` + `@pixi/react@^8.0.5` の 2 本のみ。`@pixi/particle-emitter` は v8 メンテ停止のため**意図的に未導入**。`vite.config.ts` も無変更（pixi v8 は native ESM で optimizeDeps 不要）
- **新規 `src/ui/pixi/` ツリー**: `core/PixiStage.tsx`（`extend({Container,Graphics,Sprite,Text})` + `<Application>` 透過設定 + `preference:'webgl'` 固定）、`core/usePixiApp.ts`（`useApplication` 再エクスポート + `usePixiEventGuard` で `renderer.events.features.move=false` を 1 箇所に確定）、`battle/BattleCanvas.tsx`（Step 0-A の React.lazy + mountedRef マウントガード）、`battle/layers/`（Background/Character は空コンテナ、Effect はテスト粒子）、`battle/PixiEffectBridge.ts`（命令型 API シグネチャ骨格）、`types/pixiTypes.ts`（`BattlePixiProps`）
- **画面統合**: `BattleScreen.tsx` と `GuildBattleScreen.tsx` の両 `.battle-field` 直後に `<BattleCanvas>` を挿入（後者は前者を再利用せず構造複製のため両ファイル個別修正）
- **CSS レイヤリング**: `battle-layout.css` に `.battle-screen .battle-pixi-host { z-index: 15; pointer-events: none; ... }` 追加（field=10 と hand=100 の間）。CSS 共通のため両画面でスコープ機能
- **Step 0 採用**: A 案（React.lazy + useRef マウントガード）。`main.tsx` の `<StrictMode>` は無変更で維持。B 案（dev のみ StrictMode 外し）は不要だったため不採用
- **テスト**: 新規 4 件（`pixiFoundation.test.ts`）+ 既存全 pass（123/123、回帰ゼロ）。WebGL/reconciler 依存テストは jsdom 制約により意図的に契約・layer に限定（コメントで理由明示）
- **意図的 lint disable 3 箇所**: `BattleCanvas.tsx`(set-state-in-effect=Step 0-A post-commit ゲート)、`usePixiApp.ts`(immutability=PixiJS 命令型 API 設定)、`EffectLayer.tsx`(exhaustive-deps=rAF ループ再起動防止)。いずれも該当行限定 + 理由コメント付き
- **独立監査**: role-qa（PASS-with-fixes）+ security-reviewer 並列。security は 9 脆弱性が**全て dev/build 専用ツールチェーン由来・非 PixiJS 由来**と確定、リリースブロックなし。ただし既存の `package.json` の rollup ネイティブバイナリ・ハードコード（time bomb）を発見 → MEMORY 予定 #5 として独立タスク起票（Phase 1 コミットには混ぜない）
- **方針合意**: rollup ハードコード除去は別タスク・別コミット。Phase 1 は実機検証後に commit/PR（ユーザー方針）

### 2026-05-17 - バトルロジック脆弱性修正（V-CHAIN-01 / V-ENM-02 + 回帰テスト + 脆弱性ガイド正本化）

#### 概要

README と脆弱性ガイドの不整合（V-EXEC/V-PHASE 系の完了表記）を実コードで決着。lead-pipeline 重ティアのフルチェーン（session-manager START → general-purpose 事実調査 → role-engineer 実装 → role-qa 別コンテキスト独立監査）で実施。**ground truth: README が正・ガイドが陳腐化**。当初13件と見えた修正は実体2件（V-CHAIN-01 / V-ENM-02）に縮小。QA 判定 PASS-with-fixes（コードブロッカー0、フォローアップ1件起票）。

#### 変更点

- **事実確定（調査）**: V-EXEC-01/02/03/04・V-PHASE-01/02・V-DMG-MANAGE-01 は全て修正済み（README 通り、ガイドが古い）。Phase 4 もガイドの「1/7」は陳腐化で実態 6/7。真に未修正は V-CHAIN-01・V-ENM-02 の2件のみと file:line 証拠付きで確定
- **V-CHAIN-01 修正（魔術師共鳴1枚遅れ）**: `elementalSystem.ts` に純粋関数 `getDamageModifierIncludingCard`（`onCardPlay` 後の仮想 state で modifier 算出）追加。`useElementalChain` に `getDamageModifierForPlay(card)`、`useClassAbility` にオプショナル戻り値追加（swordsman 非影響）。`useBattleOrchestrator` の damage modifier 経路を play-aware に差し替え + `useCallback` 切り出し
- **V-ENM-02 修正（敵AI preview/execute 乖離）**: `enemyAI.ts` に `resolveEnemyAction`/`clearResolvedActionCache` 新設。`(id,hp,maxHp,turn,callIndex)` でメモ化し execute/preview/EnemyFrame レンダーの3経路が同一結果を共有。確率分布維持。`initializeBattle` 冒頭で cache クリア（バトル間リーク防止）
- **回帰テスト追加（4ファイル44件）**: `elementalSystem.test.ts`(13)・`bleedDamage.test.ts`(8)・`phaseLogic.test.ts`(15)・`enemyAI.test.ts`(8)。既修正項目（V-DMG-06/10・V-CLASS-13/04・V-PHASE-01/02・V-DMG-01）の固定化 + 新規修正検証
- **脆弱性ガイド正本化**: `vulnerability-remediation-guide.md` を実態へ更新（Phase 4=7/7、Phase 5 全 ✅FIXED、証拠 file:line 併記、冒頭カウント 35 fixed / 71 remaining）。README は元から正のため変更なし
- **QA 独立監査**: 別コンテキスト role-qa が型/lint/test 再実行（全 PASS、119テスト）、engineer 委譲懸念4点を裁定。Major1件 = `getResonanceEffects` の resonance-debuff 1-card-lag 非対称が残存（damage 側のみ修正で非対称化）→ known-issue 001 として起票
- **フォローアップ起票**: `docs/known-issues/001-resonance-debuff-card-lag.md` 新規 + INDEX 更新 + MEMORY 予定 #2

#### 次

known-issue 001（resonance debuff 1-card-lag）の修正は別タスク。本変更は未コミット（main ブランチ + .claude リファクタ塊と分離コミット必要、ユーザー承認待ち）。

### 2026-05-17 - .claude ハーネス構造を life-editor 準拠へリファクタ（Phase A-E 完了）

#### 概要

グローバル CLAUDE.md 標準構造との乖離を解消するため、`.claude/` を life-editor 運用に合わせて全面再編。ユーザー確認で「フル移行（git mv 標準化）」「MEMORY/HISTORY へ移行」「プロジェクト固有エージェント作成」の 3 方針を決定。Phase A（ディレクトリ移行）〜 Phase E（課題整理）を 1 セッションで完遂。

#### 変更点

- **Phase A — ディレクトリ移行**: `git mv` で `.claude/code_overview/` → `.claude/docs/code-explanation/`（15 ファイル + サブディレクトリ）、`feature_plans/` の tracked 4 ファイル → `docs/vision/plans/`、untracked PixiJS 4 ファイルは `mv`、`memories/LESSONS_LEARNED.md` → `docs/known-issues/`、`memories/basecamp_consolidation_completed.md` → `archive/`。空ディレクトリ削除
- **クロスリンク一括更新**: 非 worktree の .md 11 ファイルで `.claude/code_overview/`→`.claude/docs/code-explanation/` 等を sed 置換。残存ゼロ確認。`worktrees/` 配下は別 git worktree のため非対象
- **Phase B — 標準インフラ新設**: `docs/vision/core.md`（Vision・設計原則）、`docs/known-issues/INDEX.md`（8 知見カタログ + 脆弱性ガイドへのポインタ）、`MEMORY.md`（進行中/直近完了/予定、旧 TODO バックログを移管）、`HISTORY.md`（本ファイル）を作成。`docs/requirements/` ディレクトリも用意。`docs/INDEX.md` を標準構造 + ゲーム設計書の二層構成に書き換え
- **Phase C — CLAUDE.md 再構成**: §0 Meta（役割/更新規則/タスク運用/関連ドキュメント表）追加、Task Completion Rule を TODO/README → MEMORY/HISTORY モデルへ書き換え、Document System 節新設、References 表を更新。223 行（400 行以下目標を維持）。`TODO.md` を MEMORY.md への薄いポインタへ縮小
- **Phase D — プロジェクト固有エージェント**: `agents-lib/projects/original-card-battle/` に分析特化 3 体作成（`card-battle-balance-auditor` = データ vs 設計書整合 / `card-battle-state-invariant-checker` = Context・React19・不可侵コード・セーブ網羅 / `card-battle-battle-logic-validator` = バトルフェーズ・ダメージ・バフ整合）。`.claude/agents/` にシンボリックリンク、`AGENT_INDEX.md` に節 + 最終更新追記
- **Phase E — 課題整理**: 脆弱性ガイドを精読し MEMORY.md「予定」を優先度付きで具体化。**ドキュメント不整合を発見**: README は V-EXEC/V-PHASE 系を「2026-02-05 完了」と記載するが、vulnerability-remediation-guide.md の Phase 5 では未修正扱い。最優先で実コード確認・寄せ先決定が必要（MEMORY.md 課題 #1）
- **方針**: タスク管理は TODO.md/README 履歴 → MEMORY.md/HISTORY.md へ移行（task-tracker 経由運用）。README の Development History は完了履歴の要約として継続

#### 次

MEMORY.md 課題 #1（README vs 脆弱性ガイドの V-EXEC/V-PHASE 不整合）を `card-battle-battle-logic-validator` で検証し寄せ先決定。未コミット（ユーザー確認後に commit 予定）。
