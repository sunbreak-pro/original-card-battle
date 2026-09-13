# HISTORY.md - 変更履歴

> セッション単位の変更履歴（降順）。各エントリは「概要」+「変更点」。要約は `README.md` の Development History、進行状況は `MEMORY.md`。古いエントリは肥大化したら `HISTORY-archive.md` へ退避。

### 2026-09-13 - 戦闘コア v4 の設計確定（7 決定の反映、カード 80 種、敵 9 体、次セッション用プラン）

#### 概要

棚卸への 7 決定（移動も属性にして特性の基準にする / ドラッグ + 矢印 + 場 / T0 に弱い効果を残す / スタンス枠 1 / 状態 10 語 + ボス専用 / 敵 9 体と連戦モード / 設計に集中して実装は次セッション）を設計書に落とした。`battle_core_v4.md` を新設して数値の正本を v3 から移し、剣士カード 80 種（初期 40 / 習得 40）と 80% 用の敵 9 体を具体化した。実装は次セッションで、冒頭プロンプトを実装プランに置いた。

#### 変更点

- **規則**: `battle_document/battle_core_v4.md`（§0 差分表、§2 属性 5 つと面・解決順・特性 8 条件 × 7 効果・スキーマ追加項目 push / hits / ally、§3 T0 の扱いと minInvest 0 は 20 種まで、§4 スタンス枠 1、§5 状態 10 語 + ボス専用 2、§7 敵ごとの間合いと 2 体戦、§8 手札 5 枚 / 全捨て / デッキ 15〜80 / 同種 3、§9 ターン進行、§10 定数、§11 実装への写像、§12 連戦モード、§13 試験台の基準 9 項目、§14 未確定 6 点）
- **カード**: `card_document/swordsman_cards_v4.md`（80 種。単属性 40 / 二属性 40、minInvest 0 は 20、アタック面 38 種で 近 15 / 中 14 / 遠 9、特性 20 枚、探索の面候補 12。ボス 瘴気の司祭の固有 3 種を含む）
- **敵**: `enemy_document/enemy_roster_v4.md`（通常 6: 長柄の歪み兵 / 影走りの犬 / 錆びた鎧の亡者 / 弩の狩人 / 靄の射手 / 双刃の歪み兵、精鋭 2: 甲冑の番人 / 群れの長 + 取り巻きの犬、ボス 1: 瘴気の司祭（適応 3 条件、専用の状態 瘴気纏い / 呪縛）。100% 用の残り 10 体は名前と役割。連戦の順 §6）
- **UI**: `battle_ui_ux_v1.md` v1.2 §3.6（帯 = 投入量、矢印 = 相手、場 = 自分と全体、手札 5 枚の扇、HUD 追加部品、演出の文法）
- **要件 / 索引 / プラン**: tier2 R2-2 / R2-3 に v4 の起票と受け入れ基準を追記。`INDEX.md` に v4 の行。`vision/plans/2026-09-13-battle-v4-implementation.md`（完了条件 5 つ + 次セッション冒頭のプロンプト）
- **レポート**: `docs/reports/2026-09-13-battle-v4-cards-enemies.html`（決定の反映、規則の差分、カード 80 種の表、敵 9 体の表、次セッションのプロンプト）。前編 `2026-09-13-battle-100-inventory.html` に決定済みの注記
- **未確定**: T0 アタックの目盛り（v3 の 3 と解釈。こうだいさんの例は 5）

### 2026-09-13 - 戦闘 100% の棚卸（7 条件 × core v3、8 領域 58 項目、80% までの順番）

#### 概要

こうだいさんの方針「バトルを 80% まで仕上げてから探索へ」を受け、7 条件（1 ターン 5 枚ドロー、タイプ 4 種と 2 つまでの重複、同種 3 枚まで、80 種のうち 40 種は習得、ホバーとドラッグのカード感、カードごとの攻撃エフェクト）を `battle_core_v3.md` に突き合わせ、100% に要る要素を棚卸した。衝突 4 点は全て v3 を改訂する側で解き、実装済みの投入 / 間合い / 予兆 / 構え / 崩しは残す。現在地は重み付きで約 20%。

#### 変更点

- **レポート**: `docs/reports/2026-09-13-battle-100-inventory.html`（Artifact `https://claude.ai/code/artifact/3f7541f9-f0fe-4e74-aef4-d07737d40e85`）。§1 条件の突き合わせ、§2 タイプ 4 種と 10 パターン（移動は属性）、§3 状態 10 語とスタンス枠 1、§4 ターンの流れ v4 案と「0 投入 × 5 枚」対策（T0 は威力 0）、§5 カード 80 種の配分（初期 40 / 習得 40、単独 40 / 二面 40）、§6 敵 19 体（80% は 9 体）、§7 試験台の基準 9 項目、§8 ドラッグの高さで投入量を決める操作と HUD の追加部品、§9 演出の文法（系統 8 × 投入段 × 固有の飾り）、§10 棚卸表 58 項目、§11 80% までの 6 段階
- **タスク**: MEMORY の予定に「戦闘を 80% へ（順 1〜6）」を追加。判断待ち 7 点
- **触っていないもの**: 設計書本体（v4 は判断後に書く）、`unity-port/`、`src/`

### 2026-09-12 - 戦闘 UI v1.1: 見た目 A 採用 + 鮮やかさと描き込み + 戦闘コア v3 と UGUI View を Unity に実装

#### 概要

こうだいさんの決定（A 採用、実画面はもう少し鮮やかで描き込みを、敗北画面と手記は凝ってよい）を設計書 v1.1 とモックアップに反映し、そのまま Unity に実装した。`unity-port/BattleCore/` を battle_core_v3（投入量 0〜3、予兆、敵 Guard、構え、崩し、冷静 / 死力、瘴気、探索からの入力、演出用イベント列）へ全面改修し、`unity-project-kit/Assets/View/` に L1 レイアウトと A+ トークンの View を 11 ファイルで書いた。`dotnet test` 54 / 54、Unity EditMode 54 / 54、Windows プレイヤーをビルドしてスクリーンショットで配置を確認し、重なり 4 か所を直した。

#### 変更点

- **設計書 / モックアップ**: `battle_ui_ux_v1.md` v1.1（§4.4 A 採用、§4.5 A+ トークンと描き込み 11 要素、§4.6 敗北画面、§4.7 手記ドロワー、§6.4 実装状況）。モックアップの skin A を A+ に更新し、敗北の例と手記ドロワーを作り替え（Artifact v2）
- **戦闘コア v3（C#）**: `Types.cs`（Tier / CardDef / EnemyDef / Omen / BattleInit / BattleEvent 群）、`Constants.cs`（§10 の定数）、`Combat.cs`（間合い補正・丸めは AwayFromZero・Guard 適用・構え・瘴気ペナルティ・投入量の選択）、`Cards.cs`（剣士 6 種 + 応急処置の 4 段表）、`Enemy.cs`（長柄の歪み兵 5 行動・決定木・予兆・空振り回避）、`BattleReducer.cs`（§9 のターン進行）、`ViewModel.cs`（TierView / OmenView / JournalView / 既定投入）、`IBattleView.cs`（HUD 全項目）、`BattleStore.cs`（`BattleInit` 受け取り）
- **テスト**: 4 ファイルを v3 向けに書き直し（54 件）。`ParityTests.cs` は `V2_PARITY` 定義時だけコンパイル。`tools/gen-trace-actions.mjs` は `TRACE_V2=1` が無いと停止。`Resources/trace-actions.txt` を v3 形式（勝利まで 77 手）で再生成
- **View（UGUI）**: `BattleTheme` / `UiTween` / `ProceduralArt` / `UiKit` / `ArenaView` / `BattleHud` / `HandView` / `JournalDrawer` / `ResultOverlay` / `BattleDirector` / `BattleScreenView`。イベント列を §5 の ms で再生してから確定値を描く。`Application.runInBackground`、`-captureDir` の定期スクリーンショット、`-replayTrace` を追加
- **同期 / 文書**: `sync-unity-project.mjs` が View の全 .cs を写す。`unity-port/README.md` に v3 の使い方。レポート `docs/reports/2026-09-12-battle-uiux-80.html` に実装節とスクリーンショット
- **人手が要る残り**: Editor 前面での手触り確認（乱数「実戦」）、本物の立ち絵 / 書体 / 効果音

### 2026-09-12 - 戦闘 UI / UX の 80% 設計（情報設計・操作・見た目 3 案・演出仕様・Unity 写像）

#### 概要

戦闘画面の UI / UX を「完成度 80%」まで設計した（実装は先、コードは書かない）。`battle_core_v3.md` の値 5 つと予兆を「敵の頭上 → 床の狙い帯 → 手札の上のスタミナ」の縦一列に置き、1 枚のプレイをカード選択 → 投入量チップの 2 操作に収めた。見た目は A 霧と灯り / B 鉄と革 / C 墨と朱 の 3 案をモックアップで比べ、A を推奨（最終選択はこうだいさん）。演出 10 節を ms / 補間 / 音の有無で表にし、Unity の Canvas 6 層と DOTween / Animator の分担、アセット一覧に落とした。

#### 変更点

- **設計書**: `.claude/docs/battle_document/battle_ui_ux_v1.md` を新設（§1 情報設計 20 項目、§2 L1 / L2、§3 操作、§4 見た目 3 案と階層 5 段、§5 演出、§6 Unity 写像、§7 開示度の仮置き、§8 残り 20%、§9 ルールへの要望 5 件）
- **モックアップ**: `docs/mockups/2026-09-12-battle-uiux-mockup.html`（1 ファイル完結。見た目 3 案 × 階層 3 段 × レイアウト 2 種 × 開示度、手札が実際に操作でき、1 ターン再生と 被弾 / 崩し / 勝敗 の例）。Artifact `https://claude.ai/code/artifact/750029ad-d93f-4cdc-b923-9dce0b1bd041`
- **レポート**: `docs/reports/2026-09-12-battle-uiux-80.html`（採用 / 保留 / 見送り、達成状況、演出の主要値、残り 20%、仮定と要望）。Artifact `https://claude.ai/code/artifact/4712190b-73c3-48b1-a07e-b725cfb6fcd1`
- **要件**: `requirements/tier2-support.md` R2-5 の内容と受け入れ基準を 9 項目に更新。`docs/INDEX.md` に設計書を追記
- **ルール側への要望**: 開示度の段階定義 / 崩し後の低投入見込みの表示可否 / clamp のイベント通知 / 敵 Guard の対称表示 / 端数処理（MidpointRounding）の確定
- **ブランチ**: `docs/battle-uiux-80`（未マージの `docs/concept-v3-inheritance-loop` から分岐）

### 2026-09-12 - 三者評価を受けた 5 決定の反映（瘴気 / 手記 / HP 回復 / 遺産の残存 / セーブ前倒し / C# 正本化）

#### 概要

三者評価の判断待ち 6 点にこうだいさんが回答し、設計書へ反映した。衰弱を「瘴気」に改名して階層ごとの濃度で蓄積する仕組みにし、図鑑を「手記」（持ち歩く帳面、メモあり、戦闘中は読むだけ、死亡地点に残り選んだ頁だけ継ぐ）に改めた。HP の回復モデル（戦闘中カード / 階層間休憩 / 探索イベント）と遺産の残存規則（痕跡は回収まで残る、1 つの生で 1 件）を決め、セーブ要件を Phase 4 の頭へ前倒しし、戦闘コアの正を C# に切り替えた。

#### 変更点

- **瘴気**（concept-v3 §6、battle_core_v3 §3.1）: 階層 n の濃度 = min(n, 5)。刻限 1 行動ごとに濃度 × 1% 蓄積。20% ごとに最大スタミナ -1、100% で瘴気死。和らげる手段: 防瘴の面（濃度 -1）/ 浄化の香（蓄積 -10%）/ 階層間休憩（蓄積しない）。定数 MIASMA_* を追加
- **HP と回復**（concept-v3 §7.1、battle_core_v3 §4 / §7.2）: HP は戦闘をまたいで持ち越す。戦闘中は `heal` 型カード（応急処置 T1 3 / T2 5 / T3 7）、階層間休憩 30%、階層内休息 15%（刻限 -1）、泉・薬草イベント。無償の全回復は無い
- **手記**（concept-v3 §5、tier1 R1-10、CAMP §3.3）: 敵の頁 / ダンジョンの頁 / メモ。拠点・探索・戦闘のどこでも読めるが、戦闘中は現在の敵の頁を表示するだけで戦闘に効果を与えない。死亡時は痕跡に含めて死亡地点に残し、遺産の枠で選んだ頁だけ継ぐ。concept-v2 A1 は「一部復活」に分類変更
- **遺産の残存**（concept-v3 §8.3、tier1 R1-12、CAMP §3.1）: 痕跡は回収されるまで残る（次の死亡で上書きしない）。1 つの生で受け取れるのは 1 件。企画書 §16「前回死亡者だけ」からの変更として明記
- **セーブ前倒し**（tier1 R1-16）: Phase 6 → Phase 4 の頭。依存を R1-8〜10 に変更し、R1-11 が R1-16 に依存。死亡と生の終了は確定と同時に保存（セーブスカム防止）
- **C# が正**（tier1 冒頭・R1-3、core.md、battle_core_v3 §13、CLAUDE.md、`unity-port/README.md` バナー）: TS の `src/ui/battle-lab/core/` は凍結。`parity:*` は履歴として残し更新しない。R1-3 の受け入れ基準を `dotnet test` に変更
- **名称の一括置換**: 衰弱 → 瘴気、図鑑 → 手記を concept-v3 / core / tier1〜3 / master / CAMP / battle_core_v3 / CLAUDE.md で置換。concept-v3 §13 に決定表を追加、§12 の 2 / 20 / 21 を決定済みに
- **未変更**: コード（`src/` / `unity-port/*.cs`）。評価レポート 2 本は当時の記録として据え置き
- **追加決定（同日）**: 生存ルートでは手記を丸ごと受け継ぐ（死亡より見返りを大きくする）。釣り合いとして探索にランダムな報酬・イベントを持たせる（concept-v3 §7.3 / §8.2 / §12-22、tier1 R1-13、tier2 R2-4、CAMP、master）
- **次セッションのプロンプト**: `docs/prompts/2026-09-12-next-session-battle-uiux.md`（戦闘 UI / UX を 80% に。設計のみ）

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
