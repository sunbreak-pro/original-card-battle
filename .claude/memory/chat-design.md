# MEMORY (chat-design)

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `history/chat-design.md`。
> レーン `design` = 見た目（UI / UX 設計・演出・立ち絵・素材）。既定の `area:` は `ui` `art`。

## 進行中

（なし）

## 直近の完了

- 戦闘の手札で札の名前と特性の行を札に収める（#210）✅（2026-09-26）— `CardView.cs` が名前を 1 行のまま枠に収まる大きさへ、特性の行を右隣の札が重なる手前（`traitVisibleRight` 150 px）で終わる大きさへ縮める。2 行のほうが大きく出せる特性だけ 2 行。80 種を通すテスト付き。PR #214（open）。食い違いは #213

## 予定

- 🔜 **#210 の見た目の確認（メインのチャット）** — PR #214 の merge 後に `npm run unity:sync` → Unity Editor で `DemoFlowPlaybackTests` → `Logs/DemoShots/03-battle.png` を #210 に貼る。プレハブの修正は不要
- 🔜 **#213 の決着待ち** — 画面の設計書（battle）とカードの設計書（cards）のどちらを直すかが決まったら、背水の陣の特性の行とランプの見せ方を合わせる
