# MEMORY (chat-main)

## 進行中

### 🔧 長柄の歪み兵の立ち絵（polearm_warped）— C0 完了・人の生成待ち（着手日: 2026-09-20）

**対象**: `.claude/docs/art_document/briefs/polearm_warped.md` / Krita AI Diffusion
**引き継ぎ書**: `.claude/docs/handover/2026-09-20-krita-art-pipeline.md`

- 前回: Krita AI Diffusion 1.53.0 を入れ、1024×1024 の試し生成に成功（28 step / 21 秒）
- 現在: **C0（仕様カード）完了**。表示枠 140×300 / 基準点 / 反転 / 行動 5 種 / 貼るだけの英語の指示文を置いた
- 次: プロンプト翻訳を Disabled にしてから、1024×1536 で候補を 8〜16 枚。**主人公の見た目が未決**なので、先に敵から進める

## 直近の完了

- 戦闘シーン本番化の地ならし ✅（2026-09-20）— 正本 14 文書を v4.2 へ改訂し、本番の戦闘シーンに要る作業を Issue #46〜#62（`lane:battle`）に分け、実装計画書 `2026-09-20-battle-scene-production.md` を置いた。投入量・T0〜T3・共有の距離（近 / 中 / 遠）が設計書から消え、列 1〜4 = コスト / 近間・遠間 / 状態のスタック制になった。**未コミット**。レポート `docs/reports/2026-09-20-battle-scene-production-prep.html`（Artifact `https://claude.ai/artifact/2opwQcHTqfW4mkH7E7C283`）
- 開発環境を life-editor から移植（hooks / per-chat / Issue 駆動）✅（2026-09-20）— `.claude/settings.json` を新設し hook 5 本と危険コマンドの deny 18 件を入れた。タスク管理を per-chat へ移し、`.github/ISSUE_TEMPLATE/` 3 種とラベル 19 件を作った。**持ち越し**: #40（共有 hooks-lib の Windows バグ、暫定対応済み）
- 戦闘描写の札の読みやすさ（出せない理由、種別と説明文、重なり）✅（2026-09-20）— 手札の上に台本の次の一手を出し、順番外の札に暗い幕をかけ、違う場所で離すと理由を赤字で出す。札の間隔 204 → 160。**人手待ち**: 実際に触って重なり具合と文字の大きさの好み

## 予定

- 🔜 **戦闘シーンの実装（Issue #46〜#62、`lane:battle`）** — 計画書 `.claude/docs/vision/plans/2026-09-20-battle-scene-production.md`。依存順に 4 つの束（コアの骨 #47〜#50 → データと検証 #51〜#53 → 画面 #46 / #54〜#57 → 繰り返す理由 #58 / #59 / #62）。いま着手できるのは #46 / #47 / #62 の 3 件。人手は #60（実機の手触り）と #61（設計原則 V1 / V2 の言い直し）
- 🔜 **#44 の赤を片付ける** — `npm run build` と `npm run lint` が main で赤。原因は凍結済みの `src/ui/battle-lab/core/__tests__/parity/parityFixture.test.ts` の `@types/node` 不足（4 エラー）。レーンの検証ゲートが最初から届かない
- 🔜 **探索側（戦闘が 80% に届いてから）** — 刻限 / 瘴気 / ノードの実装、`BattleInit` との接続、手記の実データ、遺産と継承。アートは `2026-06-28-unity-migration-character-art.md` と Krita の引き継ぎ書が持つ
