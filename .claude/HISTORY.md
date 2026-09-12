# HISTORY.md - 変更履歴

> セッション単位の変更履歴（降順）。各エントリは「概要」+「変更点」。要約は `README.md` の Development History、進行状況は `MEMORY.md`。古いエントリは肥大化したら `HISTORY-archive.md` へ退避。

### 2026-09-12 - 戦闘コア v3 設計 + 単一ダンジョン化 + 三者評価

#### 概要

前セッションの上書き（concept-v3）を受け、(1) 戦闘の基礎要素を整理して `battle_document/battle_core_v3.md` v1 に設計として落とし、(2) ダンジョンを「一つを深く潜る」構造に変更（ノード式マップは継続、後で変更の余地あり）、(3) 旧個別施設設計 4 本を archive へ移動、(4) 独立エージェント 3 体（ゲームデザイン / 実装 / 反対弁護人）で新設計を旧設計と比較評価した。3 体とも「方向は改善、現状の完成度は旧を下回る（6 → 5）」で一致。評価で見つかった転記ミスは修正し、設計判断は判断待ちとして残した。

#### 変更点

- **戦闘コア v3 設計書（R1-2 成果物）**: 持つ値を HP / Guard / スタミナ（現在・最大） / 間合い / 予兆 の 5 つに限定。間合い相性 1.0 / 0.5 / 0.15 と間合い依存回復（近 1 / 中 2 / 遠 3）は v2 実機検証済みを継承。スタミナ基礎 10（3〜14）、投入 0〜3 がコスト、minInvest、疲労減衰は投入制に吸収。構え（残 3 以上で Guard +2）、崩し = スタミナ削り（体勢ゲージ不採用）、予兆コミット式 + 空振り回避、EnemyDef 決定木 + homeRange、剣士 6 種と長柄兵の投入表、C# 写像表、プレイテスト 6 観点
- **concept-v3 §12**: 3 / 7 / 16 / 18 を決定、8 は基礎部分を決定、14 は単一ダンジョンに決定。評価で指摘の 20〜23（HP 回復 / 遺産の連鎖消失 / 生存ルートの図鑑 / クラス選択）を追加
- **単一ダンジョン化**: concept-v3 / core.md / tier1 / tier2 R2-1 / master / CAMP から「ステージ選択」を除去。R2-1 を「単一ダンジョン・ノード式の設計書化（マップ方式を差し替えられる境界）」に改稿
- **archive**: `shop / blacksmith / sanctuary / guild_design.md` を `.claude/archive/camp_document/` へ git mv。INDEX と CAMP の参照を更新
- **評価で修正した転記ミス**: 遺産の受け取り場所を「死亡地点で選ぶ」に統一（concept-v3 / master / CAMP / CLAUDE.md / tier1 R1-12）。「企画書に経済の記述なし」を「通貨と店の記述なし」に訂正。バナー未付与 11 本（enemy_document 6 / element_system_spec / buff_debuff / ui_ux_design_guide / inventory_design / kickoff）に付与。tier1 R1-1 の受け入れ基準を事実に合わせ「v3 版ギャップ分析」を追加
- **レポート**: `docs/reports/2026-09-12-concept-v3-evaluation.html`（Artifact 発行）
- **判断待ち**: HP 回復モデル / 遺産の連鎖消失 / 図鑑消失の方針 / セーブ前倒し / TS-C# パリティ継続 / 戦闘 v1 でのプロト。README は Development History 節が無いため未更新

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
