# Plan: Unity 移行 First Step — 戦闘コア C# 移植 + 最小プレイアブル

> **Status**: COMPLETED（2026-09-06）— Phase 3（実 Unity プロジェクト + UGUI 最小戦闘画面）完了。固定 RNG で Web トレース 18/18 一致、EditMode 57/57。詳細は末尾 Worklog
> **Created**: 2026-06-28
> **Task**: MEMORY.md 進行中「Unity 移行 + 2.5D アニメキャラ art」の最初の実装一歩
> **親プラン**: `2026-06-28-unity-migration-character-art.md`（全体戦略・ロードマップ）。本書はその **Phase 1 を具体化した実行計画**
> **Project（移行先）**: 新規 Unity プロジェクト（現 Web リポは参照・並走保全）
> **追記 (2026-07-04)**: 間合い UI の表現方針を追記（実現性は親プラン Phase 0b で検証、詳細は Phase 3 参照）

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

art / Live2D / アニメ立ち絵（次ステップ）／全敵・全カードの横展開（パリティ確立後）／剣気・崩し等の新設計／3D 化／WebGL・デスクトップ配布（後フェーズ）。**間合いの体勢表現（差分スプライト）は最小の仮アセットに限定**し、本格 art 制作はここに含めない（2026-07-04 追記）。

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

## 環境セットアップ（Unity → Claude Code → MCP の 4 段階）

> 次セッションで「上から順になぞれば環境が立ち上がる」ための手順。**開発マシンは新規購入の Windows 11 デスクトップを想定**（Unity エディタと、後フェーズのローカル AI 画像生成の両方で GPU が効くため。現 Web リポは Mac に残しつつ、照合用にデスクトップへ `git clone` して並走させる）。

全体像は「新しい店（Unity）を建てて、外部スタッフ（Claude）が出入りできるよう、店内に受付兼通訳（MCP サーバー）を 1 人置き、その内線番号を Claude に渡す」流れ。段階は 4 つ。

### 段階 1 — Unity 本体の土台

- [ ] 1a. **Unity Hub** をインストール（Unity 本体を管理する親アプリ = 受付台）。
- [ ] 1b. Hub から **Unity Editor（LTS 系最新 = Unity 6 系）** を導入。インストール時のモジュール選択で Windows ビルド等・必要なものにチェック。
- [ ] 1c. **2D テンプレートで新規プロジェクト**を作成（＝空の店舗）。Unity 用 `.gitignore`（Library/ Temp/ 除外）を配置。

### 段階 2 — Claude の入り口（Claude Code on Windows）

- [ ] 2a. **Windows 版 Claude Code** をインストール（ネイティブ or WSL 経由）。Mac で使っている今とほぼ同じ。
- [ ] 2b. この時点で **C# のファイル読み書き + `dotnet test` は今と同じ感覚で可能**。

> **重要な順序メモ**: 本プランの Phase 1〜2（戦闘コアの C# 移植 + headless テスト）は **Editor を触らないので、段階 2 までで着手できる**。MCP（段階 3）が未完でも、純ロジック移植は今と地続きで進む。MCP は Phase 3 の UGUI 配置以降＝「エディタ作業」になって初めて効いてくる。**MCP セットアップを完璧にしてから始める必要はない**。

### 段階 3 — Unity と Claude をつなぐ通訳窓口（MCP サーバー）

- [ ] 3a. Unity の **Package Manager から MCP サーバーのパッケージを追加**（Unity エディタと Claude の間の通訳。「GameObject 作って」「コンソールのエラー見せて」を Unity 操作へ翻訳）。
- [ ] 3b. Claude Code 側に **MCP サーバーを登録**（`claude mcp add ...` 相当。登録内容はテキスト設定ファイルに書かれる）。

> どの MCP 実装を使うかで 3a/3b の具体手順（パッケージ URL・登録コマンド）が変わる。候補は Unity 公式（`com.unity.ai.assistant`）／OSS（`CoderGamester/mcp-unity`・`IvanMurzak/Unity-MCP`・`CoplayDev/unity-mcp`）。**この領域は 2025〜2026 で急変中のため、着手時に「今いちばん安定して Claude Code 連携できる実装」を Web で再確認し、版とコマンドを確定**する（本節末の「実装時に pin する事項」）。

### 段階 4 — 疎通確認

- [ ] 4a. **Unity を起動した状態で**、Claude に「今のシーンの GameObject を一覧して」や「テストを 1 つ走らせて」と依頼し、返答が返れば接続成功。

### 運用上の注意

- **MCP は Unity 起動中だけ動く**（通訳は店の中にいるので、店＝Unity を閉じると窓口も閉じる）。MCP 経由で作業させる間は Unity を開けっぱなしにする。
- **MCP はまだ発展途上**。作業中に接続が切れる／コード修正直後のドメインリロードで一時的に落ちる等がある。**最初は「読み取り中心 + 書き換えは確認あり」で慣らし**、安定したら任せる範囲を広げる。
- 役割分担は変わらない。**コード（純ロジック・テスト・データ）は Claude、Editor の配置・参照つなぎ・手触り確認は人間**。MCP は人間側の作業の一部を肩代わりするだけ。

### 実装時に pin する事項（未確定）

| 項目               | 決めること                                                   |
| ------------------ | ------------------------------------------------------------ |
| 開発マシン         | Windows 11 デスクトップ（推奨・GPU 有無を確認）              |
| MCP サーバー実装   | 公式 / OSS のどれか（着手時に最新の安定度を再確認して選定）  |
| MCP 登録コマンド   | 選んだ実装の `claude mcp add` 相当の正確なコマンド・設定内容 |
| Web リポの並走配置 | デスクトップへ `git clone`（照合基準として同一マシンに）     |

---

## Steps（次セッションの実行手順）

### Phase 0 — プロジェクト準備

> 環境の立ち上げ手順（Unity → Claude Code → MCP → 疎通確認）は前章「環境セットアップ（Unity → Claude Code → MCP の 4 段階）」を参照。本 Phase 0 はその上でのプロジェクト骨組み作成に集中する。

- [ ] 0a. Unity 6（LTS 系最新）インストール、新規 2D プロジェクト作成。Unity 用 `.gitignore`（Library/ Temp/ 等除外）。**未実施** — Unity Editor は Claude からは操作不可のため、代わりに Unity 非依存の netstandard2.1 単体ライブラリ（`unity-port/`）で Phase 1〜2 を先行（2026-07-05）。実 Unity プロジェクト化はこの Phase を人間が改めて行う。
- [ ] 0b. **リポジトリ配置を決定**（推奨: Web リポと別の新規リポ。Unity プロジェクトは構成が大きいため分離が綺麗。現 Web は参照・並走で残す）。**未決のまま暫定**: 現 Web リポのサブフォルダ `unity-port/` に配置（別リポ移行はいつでも可能な軽い作業）。
- [ ] 0c. パッケージ: Test Framework（標準）。任意で AppUI（Redux 実装）。**Unity MCP は任意**（入れるなら read-only から + 確認ゲート）。**未実施**（Unity プロジェクト自体が未作成のため）。
- [ ] 0d. フォルダ骨組み（Core/Logic/Data/View/Tests）を作成。**Core/Tests のみ相当を作成**（`unity-port/BattleCore/` + `unity-port/BattleCore.Tests/`）。Logic/View は Phase 3 で。

### Phase 1 — 戦闘コアを純 C# へ移植

- [x] 1. `Types.cs`: `RangeBand`/`CardType`/`GameResult` を enum、`Card`/`EnemyAction`/`EnemyOutcome`/`LogEntry`/`BattleState` を **immutable record**、`BattleAction` を sealed record 階層（PlayCard/EndTurn/Restart）で。（2026-07-05, `unity-port/BattleCore/Types.cs`）
- [x] 2. `Constants.cs`: `MAX_STAMINA`/`STAMINA_RECOVERY`/`FATIGUE_*`/`RANGE_MULT`/`RANGE_ORDER`/`RANGE_LABEL`/HP 等を**検証台と同値**で。
- [x] 3. `Combat.cs`: `RangeToIndex`/`ClampDistance`/`ShiftDistance`/`StaminaDamageMultiplier`/`RangeMultiplier`/`ComputeAttackDamage` を純関数移植。
- [x] 4. `Cards.cs`: カード定義 + `CreateInitialDeck`/`Shuffle(IRng)`/`DrawToHandSize`。**乱数は `IRng` 注入**。
- [x] 5. `Enemy.cs`: `ENEMY_DEF`/`ENEMY_ACTIONS`/`ChooseEnemyAction`/`ResolveEnemyTurn`（決定的・乱数なし）。
- [x] 6. `BattleReducer.cs`: `InitState(IRng)` + `Reduce(BattleState, BattleAction, IRng)`。**意図的な逸脱**: `Reduce` は3引数（`rng` 明示）にした——Combat/Cards/Enemy が全て静的純関数のため、この層も同スタイルに揃えた（RESTART の再初期化・END_TURN 内の山札尽き reshuffle にも rng が要るため）。敵フェーズは END_TURN 内で同期解決（検証台と同じ）。
- [x] 7. `ViewModel.cs`: `DescribeHand`/`DescribeCard`/`DistanceLabel`/`IsBattleOver`/`EnemyRangeHint` を移植。

### Phase 2 — テスト移植 + Web パリティ証明

- [x] 8. 検証台の **47 テスト相当**を EditMode（NUnit）へ移植（combat / battleReducer / viewModel）。実際は describeHand 分割等で 49 ユニットテストに（内容の欠落なし、role-qa 確認済み）。
- [x] 9. **パリティ harness**: 固定 RNG で `InitState` → 既定のアクション列を流し、検証台（TS）と**同一の state 系列・ダメージ・ログ**になることを突き合わせる。想定通り「TS 側で期待値を JSON 出力 → C# テストで照合」を実施（一時テストで TS を実走させ `Fixtures/parity-fixture.json` を生成、`ParityTests.cs` が22ステップ全フィールドを突き合わせ）。
- [x] 10. 純 C# 層は **headless（`dotnet test`）** でも回せるよう分離（Editor 起動なしの高速ループ）。`dotnet test unity-port/UnityCorePort.slnx` で 58/58 green（Phase 2 時点は 50/50、作業土台の BattleStore/View 8 件を加えて 58）。

### Phase 3 — 最小戦闘画面（UGUI・Web パリティ）

- [x] 11. `BattleStore` でコアを駆動（dispatch → 再描画）。
- [x] 12. UGUI で戦闘画面: **間合い表現はタブ/トラック型ではなく、キャラクターと敵の画面上の実距離・体勢（差分スプライト）で表現**（2026-07-04 方針転換 — 検証は親プラン Phase 0b のミニ実装で先行実施。`DistanceLabel`/`EnemyRangeHint` 等 ViewModel の出力自体は変更なし、見せ方のみ差し替え）／両者パネル（HP・スタミナ・疲労・ガード）／手札（`DescribeHand` を描画・クリックで PlayCard）／ターン終了／ログ（新着順）／結果オーバーレイ（勝敗 + もう一度）。
- [x] 13. **受け入れ**: Unity Editor で **1 戦を最後までプレイ**（カードプレイ・ターン終了・勝敗・リスタート）できる。テスト green。固定シードで Web 版と挙動一致。

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

- [x] EditMode テスト（47 相当）green。（49件、`dotnet test` で確認・role-qa 監査済み）
- [x] パリティ harness が固定シードで Web 版と一致（state 系列・ダメージ・ログ）。
- [x] 純 C# 層が `dotnet test`（headless）でも green。（58/58: 49 unit + 8 BattleStore/View + 1 parity。Phase 2 時点は 50）
- [x] Unity Editor で 1 戦プレイ可能（勝敗・リスタート動作）。2026-09-06: fixture 操作列の再生で敗北 → リスタート → 続行を Unity 上で確認（トレース 18/18 一致）。人手の実プレイ手触り確認は残
- [x] スコープ逸脱なし（art・全敵全カード・新要素を入れていない）。role-qa 独立監査で確認済み

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

> 開発マシン: **確定（2026-07-05）= この Windows 11 デスクトップ**。GPU 有（NVIDIA、PhysX を PATH 確認）。Web リポは既に本機へ clone 済で並走中（本セッションが本機）。dotnet SDK は未導入 → `winget install Microsoft.DotNet.SDK.10`（`PHASE3-KICKOFF.md` 0a）。
> リポジトリ配置（別リポ / サブツリー）: 暫定サブフォルダ `unity-port/` 継続。Unity プロジェクトは別リポ推奨（未着手・軽い移行）。
> ストア方式（手書き reducer / AppUI Redux）: **確定（2026-07-05）= 手書き reducer ストア**。`BattleCore/BattleStore.cs`（純 C#・購読型・no-op 抑制・`ToViewModel()`）実装済、`BattleStoreTests.cs` でヘッドレス検証。AppUI Redux は不採用（依存増・Editor 前提のため）。
> Unity MCP 導入の可否: read-only + 確認ゲートから推奨（変わらず）。
> Unity MCP 実装の選定（公式 / OSS）: **暫定（2026-07-05 調査・確度 medium）= IvanMurzak/Unity-MCP 第一候補**（Apache-2.0・最活発・ドメインリロード自動再接続。制約「パスにスペース不可」は本リポ `C:\Users\user\orca\original-card-battle` でクリア）／**CoplayDev/unity-mcp を代替**（Claude Code ガイド有・MIT）。Unity 公式 `com.unity.ai.assistant` は preview + 有料サブスク必須のため個人開発では非採用。着手時に各 README を再確認。詳細は `unity-port/PHASE3-KICKOFF.md`。
> 間合い UI の表現方式: **方針確定**（タブ/トラック型は不採用、実距離・体勢差分を採る）— 実現性・手触りは親プラン Phase 0b のミニ実装で検証中（2026-07-04）

---

## 作業土台（2026-07-05 追加 — Unity 以降のための地固め）

Phase 3 の人間作業に先立ち、Editor 抜きで用意できる土台を `unity-port/` に整備した。

- **① 環境地固め（Windows 本番化）**: `npm install` で TS 依存復旧（test 204/204・build green を本機で確認）。dotnet 導入手順を README/キックオフに明記。`unity-port/README.md` を Mac パス除去し Windows 前提へ全面刷新。
- **② コード土台（Logic/View 層・純 C#）**: `BattleCore/BattleStore.cs`（React useReducer 相当の駆動層）+ `BattleCore/IBattleView.cs`（View 契約 + `BattleViewModel` 射影）。dotnet で型・テスト検証可能。`BattleCore.Tests/BattleStoreTests.cs` 追加。
- **③ パリティ同期（TS↔C# ドリフト検出のワンコマンド化）**: `src/ui/battle-lab/core/__tests__/parity/parityFixture.test.ts`（常時ドリフトガード + `PARITY_WRITE=1` で再生成）/ `unity-port/tools/gen-parity.mjs` / `parity-check.mjs`（`npm run parity:gen` / `parity:check`）。fixture は `.gitattributes` で LF 固定。
- **④ 実行キット + リポ整理**: `unity-port/unity-project-kit/`（asmdef・Unity gitignore・`BattleScreenView` 雛形）+ `unity-port/PHASE3-KICKOFF.md`（Windows 手順 + MCP 選定）。stale な `origin/feat/unity-core-port`（PR #17 マージ済）は削除。

> これで Phase 3 は「Unity Hub でプロジェクト作成 → キットを流し込み → シーン配線」に集約される。純ロジック・テスト・データは Claude、Editor 配置・手触りは人間（一部 MCP）。

---

## Worklog — 完了時の乖離レビュー（2026-09-06）

1. **スコープ逸脱**: なし。コア + 1 戦闘の範囲内。Phase 0（AI アート / Live2D）は 2026-07-05 のユーザー確認で本プランから切り離し済（`2026-06-28-unity-migration-character-art.md`）。追加したのはトレース再生と乱数切替のデバッグ UI のみ
2. **AC 免除**: 「人間が Editor で 1 戦プレイ」は自動再生（fixture 操作列 + `EditorApplication.Step`）で代替した。人手の手触り確認は未実施のまま完了扱い（次プランの冒頭で実施）
3. **途中で出た判断の行き先**: NUnit 暗黙 using の落とし穴 → `docs/known-issues/002`。非フォーカス Editor でフレームが進まない件 → `unity-port/PHASE3-KICKOFF.md` 4 節に注記。Unity リポの初回コミット / シーンへの View 配置（現状は Bootstrap で自動生成）→ MEMORY 予定へ
