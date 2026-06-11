# Vision — Original Card Battle

> 抽象構想・設計原則の正本。CLAUDE.md は「現状の実装規約」、本ファイルは「目指す姿」を持つ。

## 1-line

オクトパストラベラー / Slay the Spire 系のターン制カードバトル RPG に**リアルタイムのターン持ち時間（速度将棋的緊張）**を載せた、本格的な戦略 × 緊張のカードゲーム。TypeScript + React の学習を兼ねた個人開発作品。

## Primary user

作者本人（N=1）。ブラウザ（Vite dev / 静的ビルド）で動く単一プレイヤー向けローグライク。

## Core Value

- **V0 — リアルタイム・ターン制限（根幹改定 / 計画中）**: プレイヤーのターンに持ち時間（≈10 秒・バトルごとに変動・常時進行）を設け、制限内での即断を迫る。戦略は事前ビルドへ front-load。詳細: [`requirements/realtime-turn-timer.md`](../requirements/realtime-turn-timer.md)
- **V1 — 戦術的カードバトル**: フェーズキュー（速度順）+ クラス固有リソース（剣気 / 元素共鳴）による読み合い
- **V2 — ローグライク進行**: 5 深度 × 5 フロアの手続き生成ダンジョン。死亡でランリソース喪失・ソウルは保存
- **V3 — ラン間成長**: ベースキャンプ 5 施設（ショップ / ギルド / 鍛冶屋 / サンクチュアリ / ダンジョンゲート）でデッキ・装備・スキルツリーを構築
- **V4 — 熟練度システム**: カード使用回数で派生カードを解放（masteryManager）

## Non-Goals

- マルチプレイヤー / オンライン同期
- 課金・サーバーサイド
- 3 クラス目以降の拡張は当面しない（剣士 / 魔術師の 2 クラス確定。召喚士は Phase 0 で完全削除済）
- モバイルネイティブ化

## 設計原則

- **データとロジックの分離**: ゲームデータは `src/constants/data/`、振る舞いは `src/domain/`。両者を混在させない
- **状態の単一所有**: 各 state は 1 つの Context が所有し、他は hook 経由で読むのみ（gold / magicStones は ResourceContext が唯一の真実）
- **不可侵コード**: `deck.ts` / `deckReducer.ts` はシャッフル決定性の核。改変禁止
- **React 19 パターン厳守**: render 中の `ref.current` 参照禁止。派生 state は render-time 比較パターン（CLAUDE.md §React 19 Patterns）
- **CSS スコープ必須**: 汎用クラス名は親でスコープ（`.battle-screen .card`）。命名衝突は既知の頻出バグ（[known-issues/LESSONS_LEARNED](../known-issues/LESSONS_LEARNED.md) #1）
- **設計書駆動**: カード・敵・施設の数値は `docs/*_document/` の設計書を正とし、実装との差分は設計書側か実装側のどちらかに寄せて解消する
