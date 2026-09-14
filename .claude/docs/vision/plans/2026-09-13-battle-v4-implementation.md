# 戦闘 v4 実装プラン（次セッション用）

> **Status**: ON HOLD（2026-09-14、こうだいさんの所感で差し戻し。着手は `battle_core_v4.md` §16 の回答と v4.2 改訂の後）。旧: PLANNED（2026-09-13）。設計は本セッションで確定（`battle_core_v4.md` / `swordsman_cards_v4.md` / `enemy_roster_v4.md` / `battle_ui_ux_v1.md` §3.6）。実装は次のセッションで行う（こうだいさん決定 7）。
> **目標**: 棚卸レポートの順 1〜2（ルール v4 の C# 実装、試験台、初期 40 種、敵 6 体、連戦モード）。順 3〜4（ドラッグ操作 / HUD / 演出）はその次。

## 完了条件

1. `unity-port/BattleCore/` が v4 の規則で動く。`dotnet test` が緑（v3 の 54 件を v4 に書き直し、属性 / 特性 / 状態 / スタンス / 5 枚 / 同種 3 / 2 体戦 / 連戦を足す）。
2. `BattleCore.Sim`（コンソール）がデッキの型 5 種 × 敵 9 体を回し、基準 9 項目（`battle_core_v4.md` §13）を表で出す。
3. 初期 40 種と敵 9 体がデータとして入っている（`Cards.cs` / `Enemies.cs`）。
4. **連戦モード**（`battle_core_v4.md` §12）が Windows ビルドで動き、3 戦を通せる。既定の並びは 長柄の歪み兵 → 影走りの犬 → 甲冑の番人。
5. 現行 View（v1.1）が v4 の state で壊れない（5 枚の手札、状態チップとスタンス枠は最小の文字表示でよい。ドラッグは次の順）。

## 次セッションの冒頭に貼るプロンプト（2026-09-14: 投入量と間合いの前提が変わったため、このままでは使わない）

```
戦闘コア v4 を Unity 用 C# に実装してください。設計は確定済みで、正本は次の 4 つです。順に読んでから着手してください。
1. .claude/docs/battle_document/battle_core_v4.md（規則。§0 の差分表、§9 のターン進行、§10 の定数、§11 の写像、§12 の連戦モード、§13 の基準）
2. .claude/docs/card_document/swordsman_cards_v4.md（カード 80 種。初期 40 を先に入れる）
3. .claude/docs/enemy_document/enemy_roster_v4.md（敵 9 体。通常 6 / 精鋭 2 / ボス 1）
4. .claude/docs/vision/plans/2026-09-13-battle-v4-implementation.md（このプラン。完了条件 5 つ）

前提:
- 現状のコードは unity-port/BattleCore/（v3、C# 9 / netstandard2.1）と unity-project-kit/Assets/View/（UGUI、v1.1）。dotnet test は 54 / 54。同期は npm run unity:sync、Unity CLI は unity test --mode EditMode と unity build --target StandaloneWindows64。手順は unity-port/README.md と HISTORY.md 2026-09-12 の項。
- v3 で検証済みの部分（投入量 / 間合い補正 / 予兆 / 構え / 崩し / 瘴気 / 端数 AwayFromZero）は変えない。
- 最初の目標は連戦モード（battle_core_v4.md §12）。単発の戦闘を作ってから繋ぐのではなく、最初から 3 戦を続けて通す形で作る。
- 試験台 BattleCore.Sim は dotnet のコンソールプロジェクトとして unity-port/ に足す。デッキの型 5 種 × 敵 9 体 × 貪欲な打ち手で、基準 9 項目を表にする。
- View はドラッグ操作（battle_ui_ux_v1.md §3.6）をまだ作らない。v4 の state で壊れないこと（5 枚の手札、状態とスタンスの文字表示）までにする。
- 触らないもの: src/（Web 版）、battle_core_v3.md、設計書の数値（差分があれば実装に合わせず、報告に回す）。
- ブランチは feat/battle-core-v4（docs/battle-100-inventory から。親は未マージ）。

進め方: 範囲（触るファイル / 完了条件 / 触らないもの）を宣言してから、Types → Combat → Reducer → Cards / Enemies → Sim → 連戦 → View 最小対応 → テスト → ビルド → スクリーンショットの順。人手の確認が要る点（Editor 前面での連戦プレイ）は報告に書く。
報告は html-report スキルで、基準 9 項目の表と連戦のスクリーンショットを載せる。
```

## 触らないもの

- `src/`（Web 版、凍結）。
- 設計書の数値。差分は報告に回す。
- ドラッグ操作と演出の文法（順 3〜4）。

## 順 3〜6 の予定（このプランの後）

| 順  | 内容                                                                      | 出口                                     |
| --- | ------------------------------------------------------------------------- | ---------------------------------------- |
| 3   | ドラッグ / 投入帯 / 矢印 / 場、状態チップ、スタンス枠、5 枚の扇、山札捨札 | 実画面で 1 戦通し、判断時間 20 秒以内    |
| 4   | 演出の文法（系統 8 / 段のスケール / 状態 / スタンス）と初期 40 の飾り     | 全 40 種が実画面で区別できる             |
| 5   | 習得 20 種、精鋭 2 体、ボス 1 体（適応 AI）、デッキ構築画面               | ボス戦 12〜18 ターン、固有カードが落ちる |
| 6   | プレイテスト 3 巡と調整、記録票と調整履歴                                 | 基準 9 項目が全て目標内                  |
