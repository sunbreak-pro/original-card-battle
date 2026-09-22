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
│   ├── IRng.cs                 乱数の注入口（SeededRng / SystemRng / FixedRng）。乱数はここだけ
│   ├── IsExternalInit.cs       record/init を netstandard2.1 で使うためのポリフィル
│   ├── Types.cs                §1 の持つ値・§2 の属性と面・特性・カードと敵行動・BattleState
│   ├── Constants.cs            §10 の数値の正本（縦切りが読む行だけ）
│   ├── Columns.cs              §3 の列とコスト・§3.1 の目盛り
│   ├── Combat.cs               §5.1 のダメージ式・Guard・構え・回復とドローの clamp
│   ├── Traits.cs               §2.3 の特性評価器（面より前に 1 回）
│   ├── Statuses.cs             §5 のスタック制（StatusSet と減り方 2 型）
│   ├── Enemies.cs              敵データ（レコードの一覧）。縦切りは長柄の歪み兵 1 体（roster §2.1）
│   ├── EnemyAi.cs              §6 の決定木 2 枝・払えなければ次の候補へ・予兆 1 段・コミット
│   ├── Cards.cs                §8 のデッキ生成・シャッフル・ドロー・全捨て・デッキ検証
│   ├── CardCatalog.cs          カードデータ（レコードの一覧）と試作デッキ。縦切りは 80 種のうち 10 種 × 2 枚
│   ├── BattleEvents.cs         何が起きたかの列（イベント）。値は確定後のものだけを持つ
│   └── TurnLoop.cs             §9 のターン進行（開始 → ドロー → 行動 → 敵の行動 → 終了処理）
├── BattleCore.Tests/           NUnit（net10.0）
│   ├── Fixtures.cs             テスト用の最小のカード / 敵行動 / 戦闘者
│   ├── ColumnTests.cs          固定コスト（列 = コスト、列 4 は 3）
│   ├── CombatTests.cs          ダメージ式・Guard・構え・定数
│   ├── PositionTests.cs        近間 / 遠間の 2 値・push・位置なしの敵
│   ├── TraitTests.cs           特性 4 条件 × 3 効果
│   ├── StatusTests.cs          鈍足のスタックと位置の封じ
│   ├── PolearmTests.cs         長柄の歪み兵の数値・決定木 2 枝・スタミナ不足の落ち方・押し引き
│   ├── CardsTests.cs           手札 5 枚・全捨て・再シャッフル・同じ種で同じ結果
│   ├── PrototypeDeckTests.cs   試作デッキ 10 種の数値が正本と一致・選定の条件・重撃と向きのあるムーブ
│   ├── TurnLoopTests.cs        縦切りの合格条件（並び 17 件・数値 8 点・終端 3 点）と固定の種で 3 ターン
│   └── Fixtures/
│       └── parity-fixture.json TS 実装を FixedRng(0) 相当で走らせた正解データ
├── Depiction.Script/ ・ Depiction.Script.Tests/   戦闘描写の台本の型（kit の `Assets/View/Depiction/Script/` と `Tests/` を `dotnet test` で回すための器）
├── Depiction.Bridge/ ・ Depiction.Bridge.Tests/   コアのイベント列 → 台本の変換器（kit の `Assets/View/Depiction/Bridge/` を回す器）
├── DungeonCore/                探索コア（netstandard2.1 / C# 9・engine-free・BattleCore を参照しない）
│   ├── IRng.cs                 SplitMix64。同じ種なら .NET でも Mono でも同じ地図
│   ├── NodeKind.cs             ノードの種類（dungeon_exploration_v4.md §2.1 の残す 13 件）
│   ├── LayerMapSpec.cs         1 階層の形（行の幅・種類の数・分岐の出やすさ）
│   ├── MapGenerator.cs         仕様 + 種 → 地図
│   ├── MapValidator.cs         到達できないノードが無いことなどの検査
│   ├── LayerMap.cs             生成された地図（ノード・辺・指紋）
│   ├── SevenLayers.cs          七層の濃度・刻限・ノード数（seven_layers_v4.md §2）
│   ├── Miasma.cs               蓄積・20% ごとの最大スタミナ・和らげる手段の計算
│   ├── RunLoadout.cs           ツール 3 枠・消耗品 3 枠の口（中身は DungeonContent）
│   ├── ExplorationState.cs     1 階層ぶんの状態（不変）
│   └── ExplorationReducer.cs   刻限と瘴気の遷移
├── DungeonCore.Tests/          NUnit（net10.0）。種の再現・到達性・七層を通した潜行
├── DungeonContent/             持ち物の目録（netstandard2.1 / C# 9・データだけ・参照ゼロ）
│   ├── ItemEffect.cs           効果の種類と、どちらのコアが読むか
│   ├── ItemCatalogue.cs        ツール 8 種 / 消耗品 5 種（tools_and_consumables_v4.md）
│   └── Loadout.cs              3 枠 + 3 枠の選択と入れ替えの規則
├── DungeonContent.Tests/       NUnit（net10.0）。枠の規則と目録の不変条件
├── tools/
│   ├── gen-parity.mjs          fixture を live TS から再生成
│   └── parity-check.mjs        再生成 → ドリフト検出 → dotnet test（ワンコマンド）
├── unity-project-kit/          Unity プロジェクトへの drop-in（asmdef / View 雛形 / gitignore）
└── PHASE3-KICKOFF.md           Phase 3（実 Unity + UGUI）の Windows 手順 + MCP 選定
```

**`DungeonCore` は `BattleCore` を参照しません**（2026-09-21、Issue #97）。戦闘コアは battle レーン、探索コアは dungeon レーンが書くので、片方の作り直しがもう片方のビルドを壊さないように切り離しています。瘴気から最大スタミナを引く規則は一時期どちらにもありましたが、#69 が `BattleCore` 側を落としたので、いまは `DungeonCore.Miasma` だけが持ちます。`IRng` は同じ形のものが両方にあり、まとめるかどうかは探索と戦闘を繋ぐ Issue #99 で決めます。

**`DungeonContent` はどのコアも参照しません**（2026-09-21、Issue #100）。持ち物の目録はデータだけなので、探索コアからも戦闘コアからも出立の画面からも、余計なものを引き連れずに読めます。効果を実際に適用するのは読む側です。

`BattleCore` は Unity 2021.2+ がそのままコンパイルできる設定（netstandard2.1 / LangVersion 9.0 / ImplicitUsings disable / Nullable enable）で書いています。将来 `Assets/Core/` へコピーしても無改変で通ることを狙っています。`BattleStore` / `IBattleView` も MonoBehaviour 非依存の純 C# なので、この dotnet ライブラリで型・テストごと検証できます。

## 骨の並び（#72 時点）

いずれも状態を持たない純関数です。状態を進めるのは `TurnLoop` で、これも「状態を受け取り、新しい状態とイベントの列を返す」純関数です。

- **ターン進行**: `TurnLoop.cs`。止まる場所は 2 つだけです。`BeginPlayerTurn` が §9 の手順 1〜5 を進めてプレイヤーを待ち、`PlayCard` が手順 6 を 1 枚ぶん進め、`EndTurn` が手順 7〜12（敵フェーズを含む）を止まらずに進めて次の予兆で終わります。カードと敵の行動は同じ `Resolve`（特性 → アタック → ムーブ → ガード → スキル）で解決します。
- **イベント**: `BattleEvents.cs`。`StaminaRecovered` や `DamageDealt` は確定後の値（`StaminaAfter` / `TargetHpAfter` など）を持ちます。読む側は数字を見せるだけで、計算しません。戦闘描写の台本への変換（#73）がこれを読みます。

- **型と数値**: `Types.cs`（属性・位置・面・特性・カードと敵行動・`BattleState`）/ `Constants.cs` / `Columns.cs`。
- **計算**: `Combat.cs`（`(面 + 特性) → 丸め → − Guard`、構え、回復とドローの clamp）/ `Traits.cs`（条件 4 語 × 効果 4 語。重撃は #71 で追加）/ `Statuses.cs`（スタックと減り方 2 型）。
- **敵**: `Enemies.cs`（敵データ。コードに直書きせずレコードの一覧で持ち、決定木が知らない行動 id を指していたら読み込み時に弾きます）/ `EnemyAi.cs`（決定木を上から見て払える最初の行動を取る。予兆はコミット式で、払えなくなったら安い行動へ替えずに休みます）。#51 は `Enemies.All` に行を足すだけで 12 体へ広げられます。
- **カード**: `Cards.cs`（デッキ生成・シャッフル・ドロー・全捨て・20〜40 と同種 3 枚の検証）。乱数は `IRng` 注入だけで、同じ種なら同じ並びになります。`CardCatalog.cs` は `swordsman_cards_v4.md` の数値をそのまま写したデータで、`PrototypeDeck.Build()` が 10 種 × 2 = 20 枚を返します。#51 は `CardCatalog.All` に行を足して 80 種へ広げます。

`CardDef` と `EnemyActionDef` は同じ `Face` / `Trait` / 列の表から書けます。#70（敵データ）と #71（試作デッキ）は型を足さずにデータだけ足せます。特性の語彙を 12 × 10 へ広げる #48 も、enum に行を足して `Traits.Evaluate` の switch を伸ばすだけで済みます。

**外してあるもの**: スタンス枠と除外置き場（#49）、2 行動の敵と 4 枝の決定木（#50）、複数体（#52）、View 契約（`IBattleView` / `ViewModel` / `BattleStore`）。v3 の実装は git の履歴にあります。戦闘描写の画面は `unity-project-kit/Assets/View/Depiction/` が担います。台本の型（`Script/`）と View は BattleCore に依存しません。

## コアから戦闘描写へ（#73）

BattleCore と台本の型の両方を見るのは `Assets/View/Depiction/Bridge/`（アセンブリ `Depiction.Bridge`）だけです。

- `CoreScriptWriter.cs`: `TurnLoop` が返したイベントの列を、画面が再生する `DepictionEvent` / `Cue` / `DepictionFrame` へ写します。数字はイベントが持つ確定後の値をそのまま写し、計算しません。`BeginPlayerTurn` と `PlayCard` は 1 つの出来事、`EndTurn` は「ターン終了 / 敵の行動 / 次の予兆」の 3 つになります。
- `CoreBattleSource.cs`: `IDepictionSource` の 3 つ目の実装です。`TurnLoop` で戦闘を進め、画面が待っているのがプレイヤーか自動の出来事かだけを決めます。`DemoDeck` は読みません。
- `CoreText.cs`: 札・予兆・状態チップの文言です。ランプ（特性の条件が今成り立つか）と持っている札の予測値は、コアの `TurnLoop.Preview` に聞きます。

- `BattleLaunch.cs`（#74）: 戦闘シーンを何で始めるか（敵の id / 乱数の種 / 開始位置 / 無人実行の 2 項目）を持ち、`CoreBattleSource` を作ります。知らない敵の id はここで弾きます。

縦切り用の戦闘シーン（Unity リポの `Assets/Scenes/Battle.unity`）は、`Assets/View/Depiction/BattleBootstrap.cs` を 1 つ置くだけで始まります。`Awake` で Inspector の値を `BattleLaunch` へ写し、できた source を `DepictionPlayer.UseSource` へ渡します。`BattleBootstrap` の無いシーン（撮影用の `BattleDepiction.unity`）は今までどおり `LiveTurn` か固定台本で動きます。無人で流すときは、`BattleBootstrap` の Auto Play と、`DepictionPlayer` の Auto Play Drags / Auto End Turn を入れます。

敵の構え（Guard +3）は、敵の行動ではなく次の予兆の出来事の頭で再生します。押し込みは「構え → 振り → 盾 → 傷 → 押し出し」で既に長く、同じ出来事に入れると 1 行動 2.0 秒を超えるためです。

## 乱数と丸め

- **乱数は `IRng`（`double NextDouble()`）の注入だけ**です。`SeededRng`（SplitMix64 をこの場に書いた実装。`dotnet test` でも Unity でも同じ種から同じ列が出るので、固定した戦闘はこれを使います）、`SystemRng`（`System.Random` の包み。ランタイムをまたいだ再現は約束されません）、`FixedRng`（固定値・既定 0）を用意しています。グローバルな乱数は呼びません。同じ種を渡せば同じシャッフル・同じ手札になります。
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

BattleCore 162 件、Depiction 53 件、Depiction.Bridge 30 件が green になります（2026-09-21 #74 時点）。

## パリティ（v2 移植の証跡。もう回らない）

2026-09-12 に C# が正になるまで、TS コアとの同一出力を `ParityTests.cs` が証明していました。v3 で状態の形が変わって `V2_PARITY` 付きの死んだコードになり、v4.2（#69）で削除しました。

`tools/gen-parity.mjs` / `parity-check.mjs` と `BattleCore.Tests/Fixtures/parity-fixture.json` は当時の記録として残しています。`npm run parity:check` の `dotnet test` 部分は通りますが、照合するテストはもうありません。fixture は `.gitattributes` で LF 固定です。

## Phase 3（実 Unity + UGUI）へ

実 Unity プロジェクト作成と戦闘画面は Unity Editor 作業（人間主体、一部 MCP）。手順は **`PHASE3-KICKOFF.md`**、drop-in は **`unity-project-kit/README.md`** を参照。

`PHASE3-KICKOFF.md` は 2026-09-05 時点の記録です。そこに出てくる `BattleStore` / `IBattleView` / `ViewModel` の配線は #69 で外れました。v4.2 の戦闘画面は `unity-project-kit/Assets/View/Depiction/` を土台に #74 が作ります。
