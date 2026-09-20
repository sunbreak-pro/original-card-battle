---
name: loop-triage
description: open Issue を 1 件ずつ着手可否で判定し、着手順を出すループ。着手前・手が空いた時に明示起動する。判定までが範囲で、起票・実装・close はしない。
disable-model-invocation: true
---

# /loop-triage — open Issue の仕分け

## 目標

open Issue すべてに「着手可 / 保留（理由 1 行）」の判定をつけ、着手順の 1 位を根拠つきで出す。

## 完了条件（機械検証可能）

- `gh issue list` で取得した open Issue の件数と、判定をつけた件数が一致する
- 着手可の Issue に順位がつき、1 位に「なぜ先か」が 1 行ついている
- 二義的と判断した Issue が `.claude/comm/decisions/chat-<self>.md` にエントリとして残っている
- open Issue が 0 件なら「担当なし」の 1 行で終わる（沈黙しない）

## 予算

- 反復上限: **12 件**（1 反復 = 1 Issue の判定）。超えたら残りを判定せず、「未判定 N 件・そのうち効きそうなのはどれか」の**仮説**を出して停止する
- 時間上限: **20 分**。開始直後に `START_TS=$(date +%s)` を取り、数件ごとに経過を確認する（上限の宣言だけでは信用しない）
- どちらかに当たったら成果を切り上げる。上限超過は失敗ではない

## 停止条件（人間に返す）

- 判定の前提が**未回答の判断に依存**している（`.claude/comm/decisions/ANSWERS.md` に答えが無い）
- 判定の結果、**Issue 本文の修正や新規起票が必要**と分かった（起票は `issue-dispatch` の担当）
- **凍結領域に触る Issue** が混ざっていた（CLAUDE.md の凍結範囲。判定せずこうだいさんへ返す）
- 上のいずれでもない曖昧さは停止せず、`decisions/chat-<self>.md` に書いて**次の Issue へ進む**

## 使ってよい道具

- `issue-dispatch` スキル — ラベルの意味・Issue と `docs/known-issues/` の境界の正本
- `issue-prompter` スキル — 依存の読み取り方（本文の「依存」節）
- `gh issue list` / `gh issue view`（`-R sunbreak-pro/original-card-battle`）— **読み取りのみ**
- 書き込んでよいのは `.claude/comm/decisions/chat-<self>.md` と `.claude/comm/outbox/chat-<self>/` の 2 か所だけ

## 判定の順序（上から当てはめる）

1. **依存先が未 close** → 保留。「#<n> 待ち」と書く
2. **`type:human`** → 保留。こうだいさん宛の一覧へ
3. **`status:frozen`** → 保留。理由をそのまま書く
4. **open PR が紐づく** → 着手済み。判定対象から外す
5. **Scope が他の着手可 Issue と重なる** → 片方を保留（one writer per artifact）
6. 残り → 着手可。`sev:` の順、同じなら番号の若い順

## 環境の事実（推論では埋まらないので明記する）

- 自分のチャット名は `.claude/comm/.session-name`（`chat-` 接頭辞なし）
- **worktree はまだ無い**。宛先ラベル（`section:`）も無いので、open Issue は全部自分宛として扱う
- **Issue への書き込み（起票・コメント・close）は機械では止まっていない**。このループが読み取りだけで終わる規約は文章なので、自分で守る
- 凍結範囲の正本は `.claude/CLAUDE.md`。アーマー / 装備・Gold・ソウル経済 / ショップ・鍛冶屋・サンクチュアリ / TS 側の `src/ui/battle-lab/core/`

---

- 判定の粒度は「リンク先を開かなくても着手可否が分かる」まで。それ以上は書かない
- 実測した反復数・所要時間は、区切りで `history/chat-<self>.md` に 1 行足す（上限値の改訂根拠になる）
