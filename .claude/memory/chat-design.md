# MEMORY (chat-design)

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `history/chat-design.md`。
> レーン `design` = 見た目（UI / UX 設計・演出・立ち絵・素材）。既定の `area:` は `ui` `art`。

## 進行中

（なし）

## 直近の完了

- design 宛ての 6 件を 1 件 1 PR で進める（#916 / #905 / #906 / #287 / #85 / #891）✅（2026-11-04）— PR #923（#916、背景の 4〜6 層の明るさ、Closes）→ #926（#905、#923 の上に積んだ、Closes）、#927（#906、Closes）、#928（#287、Refs）→ #930（#891、#928 の上に積んだ、Refs）、#929（#85、Refs）。書いた時点ですべて open。報告書 https://claude.ai/artifact/XR9Sh4RCuSwnJmd4R8QZn6 。申し送り 4 件（体幹崩しの演出、竜の体の色の食い違い、正典への見た目の案、試遊版の音の死海の名前）
- 上級の槍 2 体と錆槍の穂先の幅を、試遊版でも縦横比を保って切らない決まりにする（#287 / #85）✅（2026-11-03）— PR #879（#287、`style-guide.md` v1.9 と 2 枚の仕様カード）と PR #881（#85、錆槍の仕様カード）。どちらも Refs で、書いた時点で open。条件 5 を v1.8 で判定し直した。差分の絵は #772 待ち
- 7 層の背景を層ごとの色とコードで描く遠景に描き直し、Krita の指示文の手引きを作る（#89）✅（2026-11-03）— こうだいさんの依頼。PR #851（Refs、`docs/design-829` の上に積んだ。書いた時点で open）。報告書は https://claude.ai/artifact/1kyJZVAt6rSUGM7sCXfjec と https://claude.ai/artifact/Q2TauXF6hDfUJ4zpc5hiQQ。申し送り `2026-11-03-design-world-layer-colours.md`（world-v1 の色の食い違い）
## 予定

- 🔜 **PR #923 → #926、#927、#928 → #930、#929 と RPG-by-card#15 の merge（こうだいさんの作業）** — #926 は #923 の、#930 は #928 の上に積んである。#923 の merge 後に `npm run unity:sync` で写し、`Trial.unity` で 4〜6 層の背景を見る（#916 の Gate）
- 🔜 **#891 の残り 7 項目を選ぶ（こうだいさんの作業）** — 竜王・爛れた世界樹・死霊王・龍神・還らずの英雄の姿、暗き死海の骨、日数の色。選んだら `style-guide.md` §16.5 と `battle-visual-v1.md` を書き換える。2026-11-04 に決まった 3 つ（色は自由で系統は形で見せる、第 7 層は「三王の座」、寄生の 2 層は星空より明るく）の正典の分は main（申し送り `2026-11-04-design-891-canon-look-proposals.md` と `-dragon-body-colour.md`）
- 🔜 **明るい地（1〜6 層）での影絵の条件を決める** — 2026-11-04 に暗い層が 7 層だけになり、§17 の条件 5 は 7 層だけの条件になった（`style-guide.md` §19.3）
- 🔜 **立ち絵を試遊版の人型へ差し込む** — 2026-11-01 こうだいさん決定。Issue は main が作る（申し送り `2026-11-01-design-trial-art-insert.md`）。差分（#85、#287）はこれが入ってから作る
- 🔜 **#608 の merge 後の確認（メインのチャット）** — PR #741 は merge 済み。Unity でコンパイルしてから `unity:check` と階層 4 の背景を見る
- 🔜 **PR #373 / #374 / #375 / #376 の merge（こうだいさんの作業）** — #374 は #373 の上に積んであるので、#373 を先に merge する
- 🔜 **#234 の design の残り** — `CAMP_FACILITIES_DESIGN.md` §2 / §3.3 を 3 つのタブと用語の頁に直し、所持カードと設定・セーブを置く手記の外の画面を決める。速さの項目をその画面にも置くかもここで決める（`battle-visual-v1.md` §10）
- 🔜 **#244 の案づくり（こうだいさんの作業）** — PR #673 の依頼文で、こうだいさんが Claude Design で 3 画面と勝ったときの結果画面の案を作る。選んだ形を `battle-visual-v1.md` に足す
- 🔜 **`style-guide.md` §2 と `asset-intake.md` §3 の「実画素 450〜900」を直す** — #455 で人型が 226 px で描かれるようになり、線の太さとミップマップの理由が古い（2026-10-21 の調べで判明）
- 🔜 **`CAMP_FACILITIES_DESIGN.md` §3.2 のツール枠を「共通の 3 枠」に直す** — `concept-v3.md` §14 と `tier1-core.md` R1-14 に合わせる
- 🔜 **#210 の見た目の確認（メインのチャット）** — `npm run unity:sync` → Unity Editor で `DemoFlowPlaybackTests` → `Logs/DemoShots/03-battle.png` を #210 に貼る
