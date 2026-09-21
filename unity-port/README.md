> **2026-09-12 決定: C# が正。** 戦闘コアは `BattleCore/` が持つ。TS の `src/ui/battle-lab/core/` は v2 のまま凍結。
>
> **2026-09-21（#69）: v3 を v4.2 で置き換えた。** 投入量 0〜3・近 / 中 / 遠の 3 段・空振り回避は消え、固定コスト（列 1〜4）・近間 / 遠間の 2 値・手札 5 枚・スタック制の状態になった（正本は `.claude/docs/battle_document/battle_core_v4.md`）。v3 は git の履歴に残る。
>
> **いま入っているのは骨だけ**。型・定数・列とコスト・ダメージ式・特性の評価器・状態・デッキ操作がある。ターン進行（`BattleReducer`）と敵データと View 契約は外してあり、縦切りの #70（敵）/ #71（デッキ）/ #72（ターン進行）が v4.2 で建て直す。縦切りの範囲は `.claude/docs/vision/plans/2026-09-21-vertical-slice-polearm.md`。
>
> **使い方**: `dotnet test unity-port/UnityCorePort.slnx` → `npm run unity:sync`（コア + View を Unity プロジェクトへ）→ `unity test C:/Users/user/Unity/RPG-by-card --mode EditMode`。

# Unity Core Port — Battle Core (C#)

戦闘コアを **Unity Editor 抜きの純 C# クラスライブラリ**として持ち、`dotnet test` で規則を固定するプロジェクトです。はじまりは TypeScript（`src/ui/battle-lab/core/`）からの移植で、2026-09-12 に C# が正になりました。以後は設計書（`battle_core_v4.md`）が正本で、TS は追いません。Unity プロジェクトへ渡す土台（Unity キット・Phase 3 手順）を同じ場所に載せています。

計画の原本: `.claude/docs/vision/plans/2026-06-28-unity-first-step-core-port.md`

## なぜ Web リポのサブフォルダに置いているか

はじめは移植元の TS 実装を **パリティ検証の基準**として同一リポ内で参照するためでした。いまは設計書（`.claude/docs/`）とカード / 敵のデータが同じリポにあり、規則の変更とコアの変更を 1 コミットで並べられることが理由です。専用リポへの分離は将来の判断（未決）とし、まずは同居させています。

## 構造

```
unity-port/
├── UnityCorePort.slnx          ソリューション
├── BattleCore/                 戦闘コア v4.2 の骨（netstandard2.1 / C# 9・engine-free）
│   ├── IRng.cs                 乱数の注入口（SystemRng / FixedRng）。乱数はここだけ
│   ├── IsExternalInit.cs       record/init を netstandard2.1 で使うためのポリフィル
│   ├── Types.cs                §1 の持つ値・§2 の属性と面・特性・カードと敵行動・BattleState
│   ├── Constants.cs            §10 の数値の正本（縦切りが読む行だけ）
│   ├── Columns.cs              §3 の列とコスト・§3.1 の目盛り
│   ├── Combat.cs               §5.1 のダメージ式・Guard・構え・回復とドローの clamp
│   ├── Traits.cs               §2.3 の特性評価器（面より前に 1 回）
│   ├── Statuses.cs             §5 のスタック制（StatusSet と減り方 2 型）
│   ├── Cards.cs                §8 のデッキ生成・シャッフル・ドロー・全捨て・デッキ検証
│   └── CardCatalog.cs          カードデータ（レコードの一覧）と試作デッキ。縦切りは 80 種のうち 10 種 × 2 枚
├── BattleCore.Tests/           NUnit（net10.0）
│   ├── Fixtures.cs             テスト用の最小のカード / 敵行動 / 戦闘者
│   ├── ColumnTests.cs          固定コスト（列 = コスト、列 4 は 3）
│   ├── CombatTests.cs          ダメージ式・Guard・構え・定数
│   ├── PositionTests.cs        近間 / 遠間の 2 値・push・位置なしの敵
│   ├── TraitTests.cs           特性 4 条件 × 3 効果
│   ├── StatusTests.cs          鈍足のスタックと位置の封じ
│   ├── CardsTests.cs           手札 5 枚・全捨て・再シャッフル・同じ種で同じ結果
│   ├── PrototypeDeckTests.cs   試作デッキ 10 種の数値が正本と一致・選定の条件・重撃と向きのあるムーブ
│   └── Fixtures/
│       └── parity-fixture.json v2 移植の証跡（下記「パリティ」参照。テストは読まない）
├── tools/
│   ├── gen-parity.mjs          fixture を live TS から再生成
│   └── parity-check.mjs        再生成 → ドリフト検出 → dotnet test（ワンコマンド）
├── unity-project-kit/          Unity プロジェクトへの drop-in（asmdef / View 雛形 / gitignore）
└── PHASE3-KICKOFF.md           Phase 3（実 Unity + UGUI）の Windows 手順 + MCP 選定
```

`BattleCore` は Unity 2021.2+ がそのままコンパイルできる設定（netstandard2.1 / LangVersion 9.0 / ImplicitUsings disable / Nullable enable）で書いています。将来 `Assets/Core/` へコピーしても無改変で通ることを狙っています。

## 骨の並び（#69 時点）

いずれも状態を持たない純関数です。状態を進めるのはターン進行（#72）の仕事で、まだありません。

- **型と数値**: `Types.cs`（属性・位置・面・特性・カードと敵行動・`BattleState`）/ `Constants.cs` / `Columns.cs`。
- **計算**: `Combat.cs`（`(面 + 特性) → 丸め → − Guard`、構え、回復とドローの clamp）/ `Traits.cs`（条件 4 語 × 効果 4 語。重撃は #71 で追加）/ `Statuses.cs`（スタックと減り方 2 型）。
- **カード**: `Cards.cs`（デッキ生成・シャッフル・ドロー・全捨て・20〜40 と同種 3 枚の検証）。乱数は `IRng` 注入だけで、同じ種なら同じ並びになります。`CardCatalog.cs` は `swordsman_cards_v4.md` の数値をそのまま写したデータで、`PrototypeDeck.Build()` が 10 種 × 2 = 20 枚を返します。#51 は `CardCatalog.All` に行を足して 80 種へ広げます。

`CardDef` と `EnemyActionDef` は同じ `Face` / `Trait` / 列の表から書けます。#70（敵データ）と #71（試作デッキ）は型を足さずにデータだけ足せます。特性の語彙を 12 × 10 へ広げる #48 も、enum に行を足して `Traits.Evaluate` の switch を伸ばすだけで済みます。

**外してあるもの**: ターン進行（`BattleReducer`）、敵データ、View 契約（`IBattleView` / `ViewModel` / `BattleStore`）。v3 の実装は git の履歴にあります。戦闘描写の画面は `unity-project-kit/Assets/View/Depiction/` が担い、BattleCore に依存しません。

## 乱数と丸め

- **乱数は `IRng`（`double NextDouble()`）の注入だけ**です。`SystemRng`（実プレイ用。種を渡せる）と `FixedRng`（固定値・既定 0）を用意しています。グローバルな乱数は呼びません。同じ種を渡せば同じシャッフル・同じ手札になります。
- **丸めは `MidpointRounding.AwayFromZero`**（2.5 → 3）です。C# の既定は銀行丸め（偶数寄せ）で、設計書の表はその読み方をしていません（2026-09-12 決定）。

## 実行（Windows）

**開発マシン = この Windows 11 デスクトップ**（決定済み 2026-07-05）。node/npm は導入済み。dotnet SDK が未導入なら:

```powershell
winget install Microsoft.DotNet.SDK.10
```

ターミナルを開き直してから:

```powershell
dotnet test unity-port/UnityCorePort.slnx
```

BattleCore 79 件と Depiction 53 件が green になります（2026-09-21 #69 時点）。

## パリティ（v2 移植の証跡。もう回らない）

2026-09-12 に C# が正になるまで、TS コアとの同一出力を `ParityTests.cs` が証明していました。v3 で状態の形が変わって `V2_PARITY` 付きの死んだコードになり、v4.2（#69）で削除しました。

`tools/gen-parity.mjs` / `parity-check.mjs` と `BattleCore.Tests/Fixtures/parity-fixture.json` は当時の記録として残しています。`npm run parity:check` の `dotnet test` 部分は通りますが、照合するテストはもうありません。fixture は `.gitattributes` で LF 固定です。

## Phase 3（実 Unity + UGUI）へ

実 Unity プロジェクト作成と戦闘画面は Unity Editor 作業（人間主体、一部 MCP）。手順は **`PHASE3-KICKOFF.md`**、drop-in は **`unity-project-kit/README.md`** を参照。

`PHASE3-KICKOFF.md` は 2026-09-05 時点の記録です。そこに出てくる `BattleStore` / `IBattleView` / `ViewModel` の配線は #69 で外れました。v4.2 の戦闘画面は `unity-project-kit/Assets/View/Depiction/` を土台に #74 が作ります。
