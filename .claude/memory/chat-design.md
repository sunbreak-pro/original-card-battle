# MEMORY (chat-design)

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `history/chat-design.md`。
> レーン `design` = 見た目（UI / UX 設計・演出・立ち絵・素材）。既定の `area:` は `ui` `art`。

## 進行中

（なし）

## 直近の完了

- 戦闘画面とカードの見た目を Claude Design の案 1a で確定する（#240）✅（2026-09-27）— 元になる文書 `art_document/battle-visual-v1.md` を足し、`SOURCES.md` に行を追加。こうだいさんの指示 11 件を入れ、設計書と食い違う文 4 件（岩の構え・観察・手薄・集中）を直した。PR #245（open）。追従は #241 / #242（battle）、#243（cards）、#244（design）
- Claude Design に渡す戦闘画面の刷新の依頼文を作る（#228）✅（2026-09-27）— `docs/prompts/2026-09-27-claude-design-battle-redesign.md` と画像 5 枚。PR #229（merged）
- 戦闘の手札で札の名前と特性の行を札に収める（#210）✅（2026-09-26）— `CardView.cs` が名前を 1 行のまま枠に収まる大きさへ、特性の行を右隣の札が重なる手前（`traitVisibleRight` 150 px）で終わる大きさへ縮める。2 行のほうが大きく出せる特性だけ 2 行。80 種を通すテスト付き。PR #214（open）。食い違いは #213

## 予定

- 🔜 **PR #245 の merge（こうだいさんの作業）** — merge 後に battle が #241（`battle_ui_ux_v2.md` の追従）と #242（Unity の作り直し）へ進める
- 🔜 **#244 残りの 3 画面** — 結果画面・デッキを組む画面・連戦の休憩画面の依頼文を作り、こうだいさんが Claude Design で案を作る
- 🔜 **#210 の見た目の確認（メインのチャット）** — PR #214 の merge 後に `npm run unity:sync` → Unity Editor で `DemoFlowPlaybackTests` → `Logs/DemoShots/03-battle.png` を #210 に貼る。プレハブの修正は不要
- 🔜 **#213 の決着待ち** — 画面の設計書（battle）とカードの設計書（cards）のどちらを直すかが決まったら、背水の陣の特性の行とランプの見せ方を合わせる
