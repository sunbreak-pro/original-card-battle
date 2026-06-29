# Plan: Unity 移行 First Step — 戦闘コア C# 移植 + 最小プレイアブル

> **Status**: PLANNED（**実装は次セッション**）
> **Created**: 2026-06-28
> **Task**: MEMORY.md 進行中「Unity 移行 + 2.5D アニメキャラ art」の最初の実装一歩
> **親プラン**: `2026-06-28-unity-migration-character-art.md`（全体戦略・ロードマップ）。本書はその **Phase 1 を具体化した実行計画**
> **Project（移行先）**: 新規 Unity プロジェクト（現 Web リポは参照・並走保全）

---

## このプランの狙い（なぜ「コア移植」が最初の一歩か）

ユーザー方針は「ゲーム本体ごと Unity へ移行」。その**最初の一歩**を、絵（art）ではなく**検証済み戦闘ロジックの C# 移植 + 最小の戦闘画面**に置く。理由は 3 つ。

1. **移行リスクを最小化できる**。描画や art に手を出す前に「面白さの核（間合い×スタミナ）」が Unity 上でも**同じ数値・同じ挙動**で動くことを先に固定する。ここがズレたら何を綺麗に描いても無意味。
2. **Claude が最も効く領域**。後述の調査どおり、Claude は「Editor の GUI 操作」は苦手だが「純 C# のロジック・テスト」は実用水準で得意。最初の一歩を純ロジック移植にすると、AI 支援の効きが最大になり、学習コストを「動くもの」で早く回収できる。
3. **検証台が移植向きに作ってある**。`src/ui/battle-lab/core/` は描画と完全に切り離した純関数 reducer + 47 テスト。これは「ロジックと UI を分離してあると移植が楽」という原則をすでに満たしており、機械的に C# 化できる。

art（Live2D / アニメ立ち絵）は**この次のステップ**。本プランには含めない（Non-goals 参照）。

---

## Unity × Claude の開発しやすさ（本プランの作り方を規定する前提）

調査（2026-06-28）の要点。**この前提が「ロジック先行・MonoBehaviour を薄く」という設計判断の根拠**。

- **Claude が得意**: C# スクリプト記述・リファクタ、MonoBehaviour 非依存の純 C# クラス、ScriptableObject のデータ定義、EditMode テスト生成、エディタ拡張・シェーダ。テキスト中心の作業は実用水準。
- **Claude が苦手 / できない**: Inspector での参照割当・ドラッグ&ドロップ、シーンへのオブジェクト配置、Game ビューでの「手触り」確認。**シーン/プレハブの YAML を直接手編集すると GUID/fileID 参照が壊れる**ため危険（Editor API 経由が安全）。「コンパイルは通るが挙動を微妙に壊す変更（quiet bug）」のリスクもある。
- **Unity MCP（2026 の現状）**: Claude から Unity Editor を操作できる橋渡しが整ってきた。Unity 公式（`com.unity.ai.assistant`、70+ ツール）、OSS の `CoderGamester/mcp-unity`、`IvanMurzak/Unity-MCP`、`CoplayDev/unity-mcp`（Claude Code 連携ガイドあり）など。GameObject 作成・コンポーネント追加・Transform 操作・シーン作成/ロード・Console/コンパイルエラー取得・EditMode テスト実行・Play モード制御まで可能。**ただし発展途上**: GUID/参照整合性は実装差あり、ドメインリロードでブリッジ切断、パスにスペースで不調等。**read-only ツールから始め、確認ゲートを挟む**のが推奨スタート。
- **AI を効かせる設計原則**: ①MonoBehaviour を薄く保ちロジックは純 C# へ ②データは ScriptableObject に分離 ③headless（EditMode）テストを整備 ④シーン依存を減らす（DI / SO イベントチャンネル）。**これらは「移植のしやすさ」とも一致する**ので、本プランはこの形を採る。

> 役割分担の原則: **コード（純ロジック・テスト・データ定義・エディタ拡張）は Claude、Editor の配置・参照つなぎ・手触り確認は人間**。MCP を入れると Claude の守備範囲が広がるが、最初は read-only + 確認ゲートで様子を見る。

---

## Context

### 制約

- **スコープは「戦闘コア + 最小1戦（Web パリティ）」に固定**。art・全敵全カード・新戦闘要素（剣気/崩し）・3D・デプロイは持ち込まない（個人ゲーム失敗の 7 割超がスコープ膨張）。
- **数値・ロジックは検証台と完全一致**させる。`constants` の値はそのまま。挙動はシード固定で Web 版と突き合わせて一致を証明する。
- **現 Web リポは変更せず並走保全**（移植の正しさを照合する基準として生かす）。
- TS→C# 移植時、**乱数を注入可能にする**（後述）。これが言語をまたいだ決定的パリティテストの肝。

### Non-goals

art / Live2D / アニメ立ち絵（次ステップ）／全敵・全カードの横展開（パリティ確立後）／剣気・崩し等の新設計／3D 化／WebGL・デスクトップ配布（後フェーズ）。

---

## アーキテクチャ（View / Logic / Data の 3 層）

Chickensoft 流の 3 層。React の構造と自然に対応し、Claude が効きやすい。

```
Unity プロジェクト
├── Core/ (純 C#・MonoBehaviour 非依存・Claude 主担当)
│   ├── Types.cs           enum（RangeBand/CardType/...）+ record（Card/EnemyAction/BattleState）
│   ├── Constants.cs        数値の正（検証台と同一）
│   ├── Combat.cs           純関数（相性・疲労・距離・ダメージ）
│   ├── Cards.cs            カード定義 + 自前デッキ操作（乱数は IRng 注入）
│   ├── Enemy.cs            敵定義 + 決定的 AI
│   ├── BattleReducer.cs    (BattleState, BattleAction) => BattleState
│   └── ViewModel.cs        表示用導出（予測ダメージ・不可理由・ラベル）
├── Logic/ (薄い駆動層)
│   └── BattleStore.cs       state 保持 + dispatch（手書き reducer ストア or AppUI Redux）
├── Data/ (ScriptableObject)
│   └── ※ Phase 1 では最小。カード/敵データの SO 化は横展開フェーズで
├── View/ (UGUI・MonoBehaviour は薄く)
│   └── BattleScreen 一式（間合い/パネル/手札/ログ/結果）
└── Tests/ (EditMode・NUnit)
    └── Core のユニットテスト + Web パリティ harness
```

**React → Unity 対応**: `useReducer` → `BattleStore`（`(State,Action)=>State` を保持）／`Context` で配っていた state → Store 参照／`viewModel` の純関数はそのまま移植して UGUI から呼ぶ。

**乱数の扱い（重要）**: TS の `shuffle` は `Math.random` を使う。JS と C# は乱数列が一致しないため、**`IRng` インターフェースを切って注入**する。テストでは固定 RNG（例: 常に 0 を返す＝検証台の `vi.spyOn(Math,'random').mockReturnValue(0)` 相当）を使い、**両言語で同一の決定的出力**を作ってパリティを証明する。

---

## Steps（次セッションの実行手順）

### Phase 0 — プロジェクト準備

- [ ] 0a. Unity 6（LTS 系最新）インストール、新規 2D プロジェクト作成。Unity 用 `.gitignore`（Library/ Temp/ 等除外）。
- [ ] 0b. **リポジトリ配置を決定**（推奨: Web リポと別の新規リポ。Unity プロジェクトは構成が大きいため分離が綺麗。現 Web は参照・並走で残す）。
- [ ] 0c. パッケージ: Test Framework（標準）。任意で AppUI（Redux 実装）。**Unity MCP は任意**（入れるなら read-only から + 確認ゲート）。
- [ ] 0d. フォルダ骨組み（Core/Logic/Data/View/Tests）を作成。

### Phase 1 — 戦闘コアを純 C# へ移植

- [ ] 1. `Types.cs`: `RangeBand`/`CardType`/`GameResult` を enum、`Card`/`EnemyAction`/`EnemyOutcome`/`LogEntry`/`BattleState` を **immutable record**、`BattleAction` を sealed record 階層（PlayCard/EndTurn/Restart）で。
- [ ] 2. `Constants.cs`: `MAX_STAMINA`/`STAMINA_RECOVERY`/`FATIGUE_*`/`RANGE_MULT`/`RANGE_ORDER`/`RANGE_LABEL`/HP 等を**検証台と同値**で。
- [ ] 3. `Combat.cs`: `RangeToIndex`/`ClampDistance`/`ShiftDistance`/`StaminaDamageMultiplier`/`RangeMultiplier`/`ComputeAttackDamage` を純関数移植。
- [ ] 4. `Cards.cs`: カード定義 + `CreateInitialDeck`/`Shuffle(IRng)`/`DrawToHandSize`。**乱数は `IRng` 注入**。
- [ ] 5. `Enemy.cs`: `ENEMY_DEF`/`ENEMY_ACTIONS`/`ChooseEnemyAction`/`ResolveEnemyTurn`（決定的・乱数なし）。
- [ ] 6. `BattleReducer.cs`: `InitState(IRng)` + `Reduce(BattleState, BattleAction)`。敵フェーズは END_TURN 内で同期解決（検証台と同じ）。
- [ ] 7. `ViewModel.cs`: `DescribeHand`/`DescribeCard`/`DistanceLabel`/`IsBattleOver`/`EnemyRangeHint` を移植。

### Phase 2 — テスト移植 + Web パリティ証明

- [ ] 8. 検証台の **47 テスト相当**を EditMode（NUnit）へ移植（combat / battleReducer / viewModel）。
- [ ] 9. **パリティ harness**: 固定 RNG で `InitState` → 既定のアクション列を流し、検証台（TS）と**同一の state 系列・ダメージ・ログ**になることを突き合わせる（TS 側で期待値を JSON 出力 → C# テストで照合、等）。
- [ ] 10. 純 C# 層は **headless（`dotnet test`）** でも回せるよう分離（Editor 起動なしの高速ループ）。

### Phase 3 — 最小戦闘画面（UGUI・Web パリティ）

- [ ] 11. `BattleStore` でコアを駆動（dispatch → 再描画）。
- [ ] 12. UGUI で戦闘画面: 間合いトラック（近/中/遠 + 現在地）／両者パネル（HP・スタミナ・疲労・ガード）／手札（`DescribeHand` を描画・クリックで PlayCard）／ターン終了／ログ（新着順）／結果オーバーレイ（勝敗 + もう一度）。
- [ ] 13. **受け入れ**: Unity Editor で **1 戦を最後までプレイ**（カードプレイ・ターン終了・勝敗・リスタート）できる。テスト green。固定シードで Web 版と挙動一致。

> Phase 1〜2 は Claude 主担当（純ロジック + テスト）。Phase 3 の UGUI 配置・参照つなぎ・手触り確認は人間（または Unity MCP で一部自動化）。

---

## Files

| File / 範囲                         | Operation              | Notes                                |
| ----------------------------------- | ---------------------- | ------------------------------------ |
| 本ドキュメント                      | Create（本セッション） | first step 実装計画                  |
| 新 Unity プロジェクト `Core/*.cs`   | Create（次セッション） | 検証台 `battle-lab/core/` の C# 移植 |
| 新 Unity プロジェクト `Tests/*.cs`  | Create                 | 47 テスト相当 + パリティ harness     |
| 新 Unity プロジェクト `Logic/View/` | Create                 | BattleStore + UGUI 最小戦闘画面      |
| `src/ui/battle-lab/core/**`（Web）  | 参照のみ               | 移植元・パリティ基準（変更しない）   |

---

## Verification

- [ ] EditMode テスト（47 相当）green。
- [ ] パリティ harness が固定シードで Web 版と一致（state 系列・ダメージ・ログ）。
- [ ] 純 C# 層が `dotnet test`（headless）でも green。
- [ ] Unity Editor で 1 戦プレイ可能（勝敗・リスタート動作）。
- [ ] スコープ逸脱なし（art・全敵全カード・新要素を入れていない）。

## リスクと対策

| リスク                                | 対策                                                                       |
| ------------------------------------- | -------------------------------------------------------------------------- |
| スコープ膨張                          | 「コア + 最小1戦」に固定。art・横展開・新要素は次フェーズへ追い出す        |
| 言語間の乱数不一致でパリティ崩れ      | `IRng` 注入で決定的化。固定 RNG で TS と同一出力を突き合わせる             |
| quiet bug（コンパイル通るが挙動破壊） | パリティ harness + 47 テストで logic drift を機械検出                      |
| Editor 作業は Claude 不可             | UGUI 配置・参照つなぎは人間。MCP 導入で一部自動化（read-only から）        |
| Unity 学習コスト                      | 純ロジック移植から入り「動くもの」で早期に回収。最初の山は言語でなく考え方 |

---

## 次セッション キックオフプロンプト（コピペ用）

```
Unity 移行の First Step を実装する。計画書は
.claude/docs/vision/plans/2026-06-28-unity-first-step-core-port.md（実装契約）。
親の全体戦略は 2026-06-28-unity-migration-character-art.md。

目的: 検証済み戦闘コア（src/ui/battle-lab/core/ の純 reducer + constants 数値 + 47テスト）を
新規 Unity プロジェクトへ C# 移植し、最小の UGUI 戦闘画面で1戦遊べる状態にする（Web パリティ）。

進め方: View/Logic/Data の3層、MonoBehaviour を薄く、ロジックは純 C#、データは ScriptableObject、
テストは EditMode/NUnit + headless dotnet test。乱数は IRng 注入で決定的化し、固定シードで
TS 版と同一出力を突き合わせてパリティを証明する。

厳守: スコープは「コア + 最小1戦」に固定（art・全敵全カード・剣気/崩し等の新要素・3D・配布は入れない）。
数値・挙動は検証台と完全一致。現 Web リポは参照・並走で残す。
Claude はコード（純ロジック・テスト・データ定義）主担当、Editor の配置/参照つなぎ/手触り確認は人間。
Unity MCP を使うなら read-only + 確認ゲートから。
```

---

## 出典（Unity × Claude 開発しやすさ・2026-06-28 調査）

- Claude Code × Unity ゲーム開発: https://claudelab.net/en/articles/claude-code/unity-claude-code-game-dev-accelerate
- AI エージェント × Unity MCP: https://medium.com/@jengas/advanced-agentic-game-development-in-unity-with-mcp-5add91c579e9 ／ 研究: https://dl.acm.org/doi/10.1145/3757376.3771417
- Unity 公式 AI/MCP: https://unity.com/blog/unity-ai-mcp-how-to-get-started ／ mcp-unity: https://github.com/codergamester/mcp-unity ／ Unity-MCP: https://github.com/IvanMurzak/Unity-MCP
- ScriptableObject アーキテクチャ: https://unity.com/how-to/architect-game-code-scriptable-objects ／ コード設計: https://unity.com/how-to/advanced-programming-and-code-architecture

> 確度: medium。Unity MCP 周辺は 2025〜2026 に急変中のため、導入時に各実装の最新機能を公式で再確認すること。

---

## 決定記録（着手後に追記）

> リポジトリ配置（別リポ / サブツリー）: （未定 — 推奨は別リポ）
> ストア方式（手書き reducer / AppUI Redux）: （未定）
> Unity MCP 導入の可否: （未定 — read-only から推奨）
