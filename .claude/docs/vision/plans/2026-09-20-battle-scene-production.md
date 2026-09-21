# Plan: 戦闘シーンを本番仕様で作る

- **Status**: PLANNED（2026-09-20）
- **Created**: 2026-09-20
- **Task**: 描写デモではない、実戦の戦闘シーンを Unity に作る
- **Issue**: #46〜#62（`lane:battle`）
- **前のプラン**: `2026-09-13-battle-v4-implementation.md`（ON HOLD）。本プランが引き継ぎ、そちらは archive へ送る

---

## 1. Context

### いま何があるか

**規則の正本は揃いました。** 2026-09-20 に v4.2 の本文改訂を終え、`battle_core_v4.md` / `swordsman_cards_v4.md` / `enemy_roster_v4.md` と周辺 10 文書から、投入量・T0〜T3・共有の距離（近 / 中 / 遠）が消えました。残るのは画面側の正本（`battle_ui_ux_v2.md` §1〜§7）だけです（#46）。

**実装は v3 のままです。** `unity-port/BattleCore/`（C#、`dotnet test` 54 件が緑）は、投入量 0〜3・3 段の間合い・手札 3 枚・カード 7 種・敵 1 体の世界で動いています。v4.2 の要素（属性 5 つ / 特性 / 状態 / スタンス / 位置 / 固定コスト / 手札 5 枚 / 2 体戦 / 連戦）は 1 つも入っていません。

**画面の部品はあります。** 戦闘描写（`Assets/View/Depiction/`）に、受け皿・投げ上げ線・一字札・5 枚の扇・カードの表示が揃っています。#36 で `IDepictionSource` と `LiveTurn` が入り、プレイヤーが離した札から 1 手ぶんの台本を作れるようになりました。ただし規則は `DemoDeck` のハードコードです。

**実戦のシーンがありません。** Unity リポ（`C:\Users\user\Unity\RPG-by-card`）にあるのは `BattleDepiction.unity`（撮影用）と `SampleScene.unity` だけです。Build Settings に載っているのは後者だけで、戦闘画面 v1.1（`BattleScreenView`）はどのシーンにも置かれていません。

### 何が問題か

規則と画面が別々の世代で止まっていて、その間を繋ぐものがありません。描写は v4.2 の見た目を持ち、コアは v3 の計算を持ち、両者を変換する器がまだ 1 行も書かれていません。

### 制約

- **View は計算しません。** 値は全て台本から取ります（`.claude/CLAUDE.md`）。実戦にするには、コアが台本を作る側に回ります
- **C# は `unity-port/` が正**で、`npm run unity:sync` で Unity リポへ写します。**シーン・プレハブ・`.meta` は Unity リポが正**です
- **凍結**: アーマー（AP / 装備耐久 / 修理）、装備・Gold・ソウル経済、ショップ / 鍛冶屋 / サンクチュアリ。防御は Guard だけです
- **TS 側（`src/`）は凍結**です。触りません

### Non-Goals

探索側（刻限 / 瘴気 / ノード）、遺産と継承、手記の実データ、立ち絵の本番アート、魔術師クラス。いずれも戦闘が 80% に届いてから着手します。

---

## 2. 着手の前提（必要な条件の洗い出し）

### 2.1 決まっていること

| 領域       | 状態                                                                                                                       |
| ---------- | -------------------------------------------------------------------------------------------------------------------------- |
| 戦闘の規則 | `battle_core_v4.md` v4.2（2026-09-20 改訂）。列 1〜4 = コスト、近間 / 遠間、状態のスタック制、回復 3 固定、デッキ 20〜40   |
| カード     | `swordsman_cards_v4.md` v4.2。80 種にコストを割り当て済み（1 / 2 / 3 が 25 / 30 / 25 枚）。位置条件 16 枚の 4 マス配分済み |
| 敵         | `enemy_roster_v4.md` v4.2。通常 6 / 精鋭 2 + 取り巻き / ボス 3 + エクストラボスの枠。位置を持つのはボスと精鋭だけ          |
| 操作       | 受け皿と投げ上げ線の併用（`battle_ui_ux_v2.md` §11.1）                                                                     |
| 画面の方向 | 上帯を角へ、投入帯を消す、立ち位置と一字札、状態チップは自分 6 種（§10 / §11）                                             |
| 開示度     | 3 段（0 / 1 / 2）。1 手目は種別 + 咎める側の一字（§11.3）                                                                  |

### 2.2 決まっていないこと

着手を止めるものと、止めないものを分けます。

**止めないもの**（叩き台で進め、試験台で測ってから直す）

- 二属性の 65%、回復（HP）の列の目盛り、特性の「威力 +n」の幅
- 切り替えの札が手札に来ない危険（約 24%）、両者が遠間のままの膠着
- 精鋭の 2 行動が理不尽でないか、瘴気纏いの最大スタミナ例外

**止めるもの**（先に決める）

| 未確定                      | 誰が決めるか            | 効く先                   |
| --------------------------- | ----------------------- | ------------------------ |
| 演出の強弱 4 段の閾値       | #30                     | #57（演出の文法）        |
| 投げ上げ線に出す予測値の形  | #46 / #30               | #56（画面の実装）        |
| 敵側の状態チップの並べ方    | #46                     | #56                      |
| ボスの適応 AI の仕様        | `concept-v3.md` §12-8   | #50 / #51                |
| 設計原則 V1 / V2 の言い直し | #61（こうだいさん判断） | 文書のみ。実装は止めない |
| 主人公の見た目              | アート側（handover）    | 立ち絵。仮絵で進められる |

### 2.3 環境の前提

| 項目               | 値                                                           | 確認方法                              |
| ------------------ | ------------------------------------------------------------ | ------------------------------------- |
| Unity              | 6000.5.5f1                                                   | `ProjectSettings/ProjectVersion.txt`  |
| URP                | 17.5.0                                                       | `Packages/manifest.json`              |
| dotnet SDK         | 10.0.302                                                     | `dotnet --version`                    |
| Unity プロジェクト | `C:\Users\user\Unity\RPG-by-card`（別リポジトリ）            | —                                     |
| 同期               | `npm run unity:sync`（一方向・冪等）                         | `--dry-run` で 0 file(s) would change |
| テスト             | `dotnet test`（54 件 + depiction 53 件）、EditMode、PlayMode | —                                     |

### 2.4 いま赤いもの

**`npm run build` と `npm run lint` が main で赤です**（#44）。原因は `parityFixture.test.ts` の `@types/node` 不足で、戦闘シーンの作業とは無関係です。レーンの検証ゲートが最初から届かないので、#44 を先に片付けるか、ゲートを `dotnet test` に絞るかを決めてから着手します。

### 2.5 人手が要る工程

Claude が代われないものは 3 つです。

1. **Unity Editor での実プレイ**（#60）。Editor を前面にしないとフレームが進みません
2. **設計原則の言い直し**（#61）
3. **絵の選定**（アート側）

---

## 3. Scope

### 触ってよいもの

- `unity-port/BattleCore/`、`unity-port/BattleCore.Tests/`、新規 `unity-port/BattleCore.Sim/`
- `unity-port/unity-project-kit/Assets/View/`（`Depiction/` を含む）
- Unity リポ `sunbreak-pro/RPG-by-card` の `Assets/Scenes/Battle.unity` と `EditorBuildSettings.asset`
- `.claude/docs/battle_document/battle_ui_ux_v2.md`（#46 のみ）

### 触らないもの

- `src/`（TS、凍結）
- `.claude/docs/battle_document/battle_core_v4.md` / `card_document/` / `enemy_document/`（2026-09-20 に改訂済み。実装で矛盾を見つけたら直さず Issue へ）
- `Assets/Core/`（同期先。編集は `unity-port/BattleCore/` で行う）
- `BattleDepiction.unity`（撮影用として残す）

---

## 4. Steps

Issue の依存順です。上から 4 つの束に分かれ、束の中は並行できます。

### 束 1 — コアの骨（#47 → #48 / #49 → #50）

- [ ] #47 BattleCore を v4.2 の骨へ（属性・列とコスト・位置・ダメージ式）
- [ ] #48 特性 12 × 10 の評価器と、状態 10 語のスタック制
- [ ] #49 ターン進行を v4.2 にし、スタンス枠と除外置き場を入れる
- [ ] #50 敵 AI を v4.2 にする（決定木の 2 枝 / 4 枝、予兆 2 段、適応の口）

### 束 2 — データと検証（#51 → #52 → #53）

- [ ] #51 カード 80 種と敵 12 体を C# のデータに入れる
- [ ] #52 2 体戦と連戦モードを入れる
- [ ] #53 試験台 `BattleCore.Sim` を作り、基準 12 項目を測る

### 束 3 — 画面（#46 → #54 → #55 → #56 → #57）

- [ ] #46 戦闘の画面と操作の正本を v2.1 へ書き直す（束 1 と並行できる）
- [ ] #54 v4.2 コアの出力を戦闘描写の台本型へ変換する
- [ ] #55 実戦の戦闘シーンを作り、起動経路を通す
- [ ] #56 戦闘画面を v2.1 の形に合わせる
- [ ] #57 演出の文法を入れる（系統 8 × 強弱 4）

### 束 4 — 戦闘を繰り返す理由（#58 / #59 / #62）

- [ ] #58 出立の画面を作る（デッキ 20〜40 / ツール / 開始位置 / 才能）
- [ ] #59 習熟と才能を戦闘の結果に繋ぐ
- [ ] #62 投入量をなくした代わりのシナジーを設計する

### 人手

- [ ] #60 実機で 3 戦通して手触りを確かめる
- [ ] #61 設計原則 V1 / V2 の文面を言い直す

---

## 5. Files

| File                                                        | Operation | Notes                                                            |
| ----------------------------------------------------------- | --------- | ---------------------------------------------------------------- |
| `unity-port/BattleCore/Types.cs`                            | 書き直し  | `Attribute` / `Position` / `Faces` / `Cost` / `Trait` / `Status` |
| `unity-port/BattleCore/Constants.cs`                        | 書き直し  | §10 の表へ。`RANGE_MULT` を削除                                  |
| `unity-port/BattleCore/Combat.cs`                           | 書き直し  | §5.1 のダメージ式、`EvaluateTrait`、状態の 2 型                  |
| `unity-port/BattleCore/BattleReducer.cs`                    | 書き直し  | §9 の 12 手順                                                    |
| `unity-port/BattleCore/Enemy.cs`                            | 書き直し  | 2 枝 / 4 枝、予兆 2 段、適応                                     |
| `unity-port/BattleCore/Cards.cs`                            | 書き直し  | 80 種                                                            |
| `unity-port/BattleCore/Enemies.cs`                          | 新規      | 12 体                                                            |
| `unity-port/BattleCore/DeckRules.cs`                        | 新規      | 20〜40、同種 3                                                   |
| `unity-port/BattleCore/Chain.cs`                            | 新規      | 連戦                                                             |
| `unity-port/BattleCore/Mastery.cs`                          | 新規      | 刻み・段・才能                                                   |
| `unity-port/BattleCore.Sim/`                                | 新規      | 試験台                                                           |
| `Assets/View/Depiction/Script/`（変換器）                   | 新規      | コア → 台本                                                      |
| `Assets/View/BattleHud.cs` / `HandView.cs` / `ArenaView.cs` | 書き直し  | v2.1                                                             |
| Unity リポ `Assets/Scenes/Battle.unity`                     | 新規      | 実戦シーン                                                       |
| `.claude/docs/battle_document/battle_ui_ux_v2.md`           | 書き直し  | §1〜§7 を v2.1 へ                                                |

---

## 6. Verification

| ゲート   | コマンド                                                                         | 目安                                          |
| -------- | -------------------------------------------------------------------------------- | --------------------------------------------- |
| 単体     | `cd unity-port && dotnet test`                                                   | 緑。件数は改訂で増える                        |
| 型・静的 | `npm run build` / `npm run lint`                                                 | #44 の赤を先に片付ける                        |
| Unity 内 | `unity test C:/Users/user/Unity/RPG-by-card --mode EditMode` / `--mode PlayMode` | 緑                                            |
| 同期     | `npm run unity:sync -- --dry-run`                                                | 0 file(s) would change                        |
| 数値     | `dotnet run --project unity-port/BattleCore.Sim`                                 | 基準 12 項目。最初に見る 3 つを外していないか |
| 画面     | Editor を前面にして Play                                                         | 実戦 3 戦が通る。スクリーンショットを撮る     |
| 手触り   | 人手（#60）                                                                      | 1 ターン 20 秒以下、情報量の所感が解消        |

---

## 7. 冒頭に貼るプロンプト

> `original-card-battle` の戦闘シーンを本番仕様にします。計画書は `.claude/docs/vision/plans/2026-09-20-battle-scene-production.md`、課題は GitHub の `lane:battle`（#46〜#62）です。
>
> 読む順は `.claude/CLAUDE.md` → 計画書 → `.claude/docs/battle_document/battle_core_v4.md`（v4.2、本文が正本で §16〜§18 は記録）→ 着手する Issue の本文です。カードと敵の数値は `swordsman_cards_v4.md` と `enemy_roster_v4.md` が正本で、どちらも 2026-09-20 に v4.2 へ改訂済みです。
>
> 前提は 4 つです。(1) 戦闘コアの正は `unity-port/BattleCore/`（C#）で、TS の `src/` は凍結です。(2) View は計算せず、値は全て台本から取ります。実戦にするにはコアが台本を作る側に回ります。(3) シーン・プレハブ・`.meta` は Unity リポ（`C:\Users\user\Unity\RPG-by-card`）が正で、C# は `unity-port/` が正です。(4) 設計書の数値に矛盾を見つけたら直さず Issue へ回します。
>
> 進め方は計画書の §4 の束の順です。1 つの Issue につき 1 ブランチ 1 PR で、merge はしません。検証は `dotnet test` → EditMode → PlayMode → `unity:sync --dry-run` の順です。報告は html-report で出します。

---

## 8. この後の予定

戦闘が 80% に届いたら探索側へ進みます。刻限 / 瘴気 / ノードの実装、`BattleInit` との接続、手記の実データ、遺産と継承（死亡地点に残す仕組み）の順です。立ち絵と背景の本番アートは、`2026-06-28-unity-migration-character-art.md` と Krita の引き継ぎ書が持ちます。

---

## 9. Worklog

- **2026-09-20**: 正本を v4.2 へ改訂した。`battle_core_v4.md` は本文（§0〜§14）を書き直し、§16〜§18 を決定の記録へ降格した。`swordsman_cards_v4.md` は 80 種にコストを割り当て、位置条件を 16 枚にした。`enemy_roster_v4.md` は位置を持つ敵をボスと精鋭に絞った。周辺 10 文書を追従させた。Issue #46〜#62 を `lane:battle` で起票し、本プランを作成した。
