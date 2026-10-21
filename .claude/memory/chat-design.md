# MEMORY (chat-design)

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `history/chat-design.md`。
> レーン `design` = 見た目（UI / UX 設計・演出・立ち絵・素材）。既定の `area:` は `ui` `art`。

## 進行中

（なし）

## 直近の完了

- design 宛ての 4 件を 1 件 1 PR で進める（#623 / #85 / #287 / #244）✅（2026-10-21）— #623 は `battle-visual-v1.md` v1.10 に技名と説明を 12 px まで縮める例外を書いた（PR #662、Closes）。#85・#287 の仕様カード 3 枚を、#455 の人型 226 px とロースター v4.23 に合わせた（PR #671 / #672）。#244 の依頼文を v1.9 に上げ、勝ったときの結果画面を頼む項目を足した（PR #673）。PR はすべて open。申し送り 3 件（cards 宛て 1、battle 宛ての食い違い 2）
- 見た目の文書の食い違い 3 件と、戦闘の速さの切り替えの置き場（#322 / #366 / #324 / #327）✅（2026-10-06）— `battle-visual-v1.md` を v1.2 にし、手札の間隔を 126 px にそろえ（#322、PR #373）、速さのボタンを手記のボタンの左に置いた（#366、PR #374。#373 の上に積んだ）。`style-guide.md` の参照を `battle-visual-v1.md` へ向け直し（#324、PR #376）、`asset-intake.md` の絵のファイル名をロースターの内部 ID に決めた（#327、PR #375）。PR はすべて open。#213 と #234 の design の分のうち、`battle-visual-v1.md` の行も入れた
- 戦闘画面とカードの見た目を Claude Design の案 1a で確定する（#240）✅（2026-09-27）— 元になる文書 `art_document/battle-visual-v1.md` を足し、`SOURCES.md` に行を追加。PR #245（merged）
- Claude Design に渡す戦闘画面の刷新の依頼文を作る（#228）✅（2026-09-27）— `docs/prompts/2026-09-27-claude-design-battle-redesign.md` と画像 5 枚。PR #229（merged）

## 予定

- 🔜 **PR #373 / #374 / #375 / #376 の merge（こうだいさんの作業）** — #374 は #373 の上に積んであるので、#373 を先に merge する
- 🔜 **#234 の design の残り** — `CAMP_FACILITIES_DESIGN.md` §2 / §3.3 を 3 つのタブと用語の頁に直し、所持カードと設定・セーブを置く手記の外の画面を決める。速さの項目をその画面にも置くかもここで決める（`battle-visual-v1.md` §10）
- 🔜 **`style-guide.md` §17 の条件 5 を 4 層向けに決め直す** — `battle-visual-v1.md` §2.4 の 4 層が明るい地になったため（#324 で §19.3 に未決として残した）
- 🔜 **#244 の案づくり（こうだいさんの作業）** — PR #673 の依頼文で、こうだいさんが Claude Design で 3 画面と勝ったときの結果画面の案を作る。選んだ形を `battle-visual-v1.md` に足す
- 🔜 **`style-guide.md` §2 と `asset-intake.md` §3 の「実画素 450〜900」を直す** — #455 で人型が 226 px で描かれるようになり、線の太さとミップマップの理由が古い（2026-10-21 の調べで判明）
- 🔜 **`CAMP_FACILITIES_DESIGN.md` §3.2 のツール枠を「共通の 3 枠」に直す** — `concept-v3.md` §14 と `tier1-core.md` R1-14 に合わせる
- 🔜 **#210 の見た目の確認（メインのチャット）** — `npm run unity:sync` → Unity Editor で `DemoFlowPlaybackTests` → `Logs/DemoShots/03-battle.png` を #210 に貼る
