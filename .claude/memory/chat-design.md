# MEMORY (chat-design)

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `history/chat-design.md`。
> レーン `design` = 見た目（UI / UX 設計・演出・立ち絵・素材）。既定の `area:` は `ui` `art`。

## 進行中

（なし）

## 直近の完了

- 見た目の文書の食い違い 3 件と、戦闘の速さの切り替えの置き場（#322 / #366 / #324 / #327）✅（2026-10-06）— `battle-visual-v1.md` を v1.2 にし、手札の間隔を 126 px にそろえ（#322、PR #373）、速さのボタンを手記のボタンの左に置いた（#366、PR #374。#373 の上に積んだ）。`style-guide.md` の参照を `battle-visual-v1.md` へ向け直し（#324、PR #376）、`asset-intake.md` の絵のファイル名をロースターの内部 ID に決めた（#327、PR #375）。PR はすべて open。#213 と #234 の design の分のうち、`battle-visual-v1.md` の行も入れた
- 戦闘画面とカードの見た目を Claude Design の案 1a で確定する（#240）✅（2026-09-27）— 元になる文書 `art_document/battle-visual-v1.md` を足し、`SOURCES.md` に行を追加。PR #245（merged）
- Claude Design に渡す戦闘画面の刷新の依頼文を作る（#228）✅（2026-09-27）— `docs/prompts/2026-09-27-claude-design-battle-redesign.md` と画像 5 枚。PR #229（merged）

## 予定

- 🔜 **PR #373 / #374 / #375 / #376 の merge（こうだいさんの作業）** — #374 は #373 の上に積んであるので、#373 を先に merge する
- 🔜 **#234 の design の残り** — `CAMP_FACILITIES_DESIGN.md` §2 / §3.3 を 3 つのタブと用語の頁に直し、所持カードと設定・セーブを置く手記の外の画面を決める。速さの項目をその画面にも置くかもここで決める（`battle-visual-v1.md` §10）
- 🔜 **`style-guide.md` §17 の条件 5 を 4 層向けに決め直す** — `battle-visual-v1.md` §2.4 の 4 層が明るい地になったため（#324 で §19.3 に未決として残した）
- 🔜 **#244 残りの 3 画面** — 結果画面・デッキを組む画面・連戦の休憩画面の依頼文を作り、こうだいさんが Claude Design で案を作る
- 🔜 **#210 の見た目の確認（メインのチャット）** — `npm run unity:sync` → Unity Editor で `DemoFlowPlaybackTests` → `Logs/DemoShots/03-battle.png` を #210 に貼る
