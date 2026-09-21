> **2026-09-12 決定: C# が正。** 戦闘コアは `BattleCore/` が **battle_core_v3**（`.claude/docs/battle_document/battle_core_v3.md`: 投入量 0〜3、予兆、敵 Guard、構え、崩し）を実装している。TS の `src/ui/battle-lab/core/` は v2 のまま凍結。`parity:*` スクリプトと fixture は v2 移植の証跡として残し、`ParityTests.cs` は `V2_PARITY` を定義したときだけコンパイルされる（v3 では状態の形が違うため常時は外している）。
>
> **v3 での使い方**: `dotnet test unity-port/UnityCorePort.slnx`（ユニット 54）→ `npm run unity:sync`（コア + View を Unity プロジェクトへ）→ `unity test C:/Users/user/Unity/RPG-by-card --mode EditMode`（Editor 抜きでコンパイルと EditMode テスト）。View は `unity-project-kit/Assets/View/`（`BattleScreenView.cs` + 補助クラス。設計は `.claude/docs/battle_document/battle_ui_ux_v1.md`）。`Resources/trace-actions.txt` は v3 形式（`play <instanceId> <invest>` / `end`、`#` 行が期待状態）。

# Unity Core Port — Battle Core (C#)

検証済みの戦闘コア（TypeScript, `src/ui/battle-lab/core/`）を **Unity Editor 抜きの純 C# クラスライブラリ**として移植し、`dotnet test` で TS 版とのパリティ（同一入力 → 同一出力）を証明するプロジェクトです。Unity 移行の「第一歩」に相当し、その上に Unity 実装へ渡す**作業土台**（Logic/View 層・パリティ同期・Unity キット・Phase 3 手順）を載せています。

計画の原本: `.claude/docs/vision/plans/2026-06-28-unity-first-step-core-port.md`

## なぜ Web リポのサブフォルダに置いているか

移植元の TS 実装（`src/ui/battle-lab/core/`）を **パリティ検証の基準**として同一リポ内で参照し続けたいためです。別リポに切り出すと、TS 側の変更に C# 側が追従できているかを 1 コミットで確認しづらくなります。専用リポへの分離は将来の判断（未決）とし、まずは同居させています。

## 構造

```
unity-port/
├── UnityCorePort.slnx          ソリューション
├── BattleCore/                 移植した戦闘コア（netstandard2.1 / C# 9・engine-free）
│   ├── IRng.cs                 乱数の注入口（SystemRng / FixedRng）
│   ├── IsExternalInit.cs       record/init を netstandard2.1 で使うためのポリフィル
│   ├── Types.cs                enum・record・enum↔TS文字列トークン変換
│   ├── Constants.cs            数値定数（TS constants.ts と一致）
│   ├── Combat.cs               間合い・スタミナ・ダメージ計算
│   ├── Cards.cs                デッキ生成・シャッフル・ドロー
│   ├── Enemy.cs                敵 AI と行動解決
│   ├── BattleReducer.cs        状態遷移（Init / PlayCard / EndTurn / Restart）
│   ├── ViewModel.cs            UI 表示用の派生ビュー
│   ├── BattleStore.cs          Logic 層: state 保持 + dispatch（React useReducer 相当）
│   └── IBattleView.cs          View 契約 + BattleViewModel（描画用フラット射影）
├── BattleCore.Tests/           NUnit（net10.0）
│   ├── CombatTests.cs
│   ├── BattleReducerTests.cs
│   ├── ViewModelTests.cs
│   ├── BattleStoreTests.cs     Logic/View 層のヘッドレステスト
│   ├── ParityTests.cs          TS ゴールドデータとのフルトレース照合
│   └── Fixtures/
│       └── parity-fixture.json TS 実装を FixedRng(0) 相当で走らせた正解データ
├── DungeonCore/                探索コア（netstandard2.1 / C# 9・engine-free・BattleCore を参照しない）
│   ├── IRng.cs                 SplitMix64。同じ種なら .NET でも Mono でも同じ地図
│   ├── NodeKind.cs             ノードの種類（dungeon_exploration_v4.md §2.1 の残す 13 件）
│   ├── LayerMapSpec.cs         1 階層の形（行の幅・種類の数・分岐の出やすさ）
│   ├── MapGenerator.cs         仕様 + 種 → 地図
│   ├── MapValidator.cs         到達できないノードが無いことなどの検査
│   └── LayerMap.cs             生成された地図（ノード・辺・指紋）
├── DungeonCore.Tests/          NUnit（net10.0）。種の再現・到達性・分岐の回避可能性
├── tools/
│   ├── gen-parity.mjs          fixture を live TS から再生成
│   └── parity-check.mjs        再生成 → ドリフト検出 → dotnet test（ワンコマンド）
├── unity-project-kit/          Unity プロジェクトへの drop-in（asmdef / View 雛形 / gitignore）
└── PHASE3-KICKOFF.md           Phase 3（実 Unity + UGUI）の Windows 手順 + MCP 選定
```

**`DungeonCore` は `BattleCore` を参照しません**（2026-09-21、Issue #97）。戦闘コアは battle レーン、探索コアは dungeon レーンが書くので、片方の作り直しがもう片方のビルドを壊さないように切り離しています。`IRng` と 20% ごとの最大スタミナの規則は両方に同じ形で置いてあり、1 本にまとめるのは探索と戦闘を繋ぐ Issue #99 の仕事です。

`BattleCore` は Unity 2021.2+ がそのままコンパイルできる設定（netstandard2.1 / LangVersion 9.0 / ImplicitUsings disable / Nullable enable）で書いています。将来 `Assets/Core/` へコピーしても無改変で通ることを狙っています。`BattleStore` / `IBattleView` も MonoBehaviour 非依存の純 C# なので、この dotnet ライブラリで型・テストごと検証できます。

## 3 層構成（View / Logic / Data）

- **Core（純関数）**: `Combat`/`Cards`/`Enemy`/`BattleReducer`/`ViewModel`。乱数は `IRng` 注入。
- **Logic（薄い駆動層）**: `BattleStore` が state を保持し、`BattleAction` を `BattleReducer.Reduce` に流して購読者へ通知（React の `useReducer` + `Context` 配布に対応）。
- **View（契約のみ）**: `IBattleView.Render(BattleViewModel)`。実際の UGUI MonoBehaviour は Unity 側（`unity-project-kit/Assets/View/BattleScreenView.cs` が雛形）。

## TS からの意図的な逸脱（数値・ロジックは不変）

数値とロジックは TS と完全一致させています。逸脱は**乱数の扱い 1 点だけ**です。

- **TS**: グローバルな `Math.random()` を `cards.ts` 内で直接呼ぶ（注入なし）。
- **C#**: `IRng`（`double NextDouble()`）を注入する。`SystemRng`（実プレイ用）と `FixedRng`（固定値・既定 0、パリティ用）を用意。
  - `BattleReducer.InitState(IRng rng)` と `BattleReducer.Reduce(BattleState state, BattleAction action, IRng rng)`（**3 引数**）が rng を受け取る。RESTART の再初期化・END_TURN 内の山札切れ時の再シャッフルで rng が要るためです。
  - `PlayCard` は乱数を使わないので内部ヘルパーに rng は渡していません。
- **丸め**: JS の `Math.round` は 0.5 を切り上げる。C# の `Math.Round` は既定が銀行丸め（偶数寄せ）なので、`Math.Round(raw, MidpointRounding.AwayFromZero)` を使って JS 挙動に合わせています（raw は常に非負）。

これ以外の実装補助（`IsExternalInit.cs` のポリフィル、enum ↔ TS 文字列トークンの変換ヘルパー）はロジックに影響しない足回りです。

## 実行（Windows）

**開発マシン = この Windows 11 デスクトップ**（決定済み 2026-07-05）。node/npm は導入済み。dotnet SDK が未導入なら:

```powershell
winget install Microsoft.DotNet.SDK.10
```

ターミナルを開き直してから:

```powershell
# C# コアのユニット + パリティテストだけ回す
dotnet test unity-port/UnityCorePort.slnx

# TS↔C# を一気通貫で照合（推奨・ワンコマンド）
npm run parity:check
```

`dotnet test` は **ユニット 49 + BattleStore/View + パリティ 1** が green になれば、C# 版が TS 版と同一挙動であることの証明になります。

## パリティ同期（TS が正・C# が追従を証明）

TS コアは「面白さの正本」。C# はそれに追従します。2 つの信号で drift を捕まえます。

```powershell
npm run parity:gen     # live TS から fixture を再生成（PARITY_WRITE=1 で vitest を実行）
npm run parity:check   # 上に加え git diff で TS 側 drift を検出 → dotnet test で C# 側を検証
```

- **`npm test` / `test:run`** … `src/ui/battle-lab/core/__tests__/parity/parityFixture.test.ts` が **TS 側ドリフトガード**として常時走る（fixture と live TS の一致を毎回照合）。
- **`git diff` の fixture 差分** … 「TS コアが前回コミットから変わったか」の信号。
- **`dotnet test`** … 「C# 移植が現在の TS を再現できているか」の信号。

TS を意図的に変えたら fixture を再生成してコミット、意図しない変化なら TS 側を戻す。fixture は `.gitattributes` で LF 固定（Windows の autocrlf でも無用な差分を出さない）。

## Phase 3（実 Unity + UGUI）へ

Core 移植・パリティ・Logic/View 層・キットまでが「Editor 抜きで用意できる土台」。実 Unity プロジェクト作成と UGUI 戦闘画面は Unity Editor 作業（人間主体、一部 MCP）。手順は **`PHASE3-KICKOFF.md`**、drop-in は **`unity-project-kit/README.md`** を参照。
