---
name: issue-prompter
description: open GitHub Issue を依存順に並べ、貼り付け用の /goal コマンド文字列を組み立てて表示する（読み取りのみ・起票も実装もしない）。Triggers include "goal プロンプト", "次どれやる", "貼り付け用プロンプト", "issue-prompter", "Issue を並べて", "着手順".
---

# issue-prompter — Issue → 貼り付け用 `/goal` プロンプト

起票済みの open Issue を**依存順に並べ直し、貼り付け用の `/goal` 文字列**にして表示します。配達物を順番に並べて送り状を書く役で、荷造り（起票）も配送（実装）も別の担当です。

- **上流 = `issue-dispatch`**（起票・ラベル付け）。本スキルは起票しません
- **下流 = `loop-implement`**（実装）。本スキルは実装しません
- **`execution-router` の本リポ特化版**。モード選定の一般則はそちらが正本で、ここは「Issue → 依存順 → `/goal` 文面」の組み立てだけを持ちます
- **読み取り専用**: Issue・git・ファイルへ一切書きません。出力はチャットに表示するテキストだけです

## 起動条件

「次どれやる」「goal プロンプト作って」「今 open な Issue を並べて」。起票そのものを頼まれたら `issue-dispatch`、1 件の実装の進め方なら `lead-pipeline` か `loop-implement` へ。

## 手順

### 1. 収集（並列）

```bash
git fetch origin main
gh issue list -R sunbreak-pro/original-card-battle --state open --limit 200 --json number,title,labels,url,body
gh pr list -R sunbreak-pro/original-card-battle --state open --json number,title,headRefName,body
```

**open PR が既に紐づく Issue は除外**します（PR の本文かブランチ名に `#<n>` を含むもの）。着手済みを再度配ると二重実装になります。

ただし **`chore/tracker-*` ブランチの PR は数えません**。tracker の PR 本文は「今日触った Issue」を列挙するため、実装 PR と同じ扱いにすると未着手の Issue まで除外されます。

`status:frozen` ラベルの Issue も配りません。着手判断がこうだいさん待ちなので、采配欄に理由付きで残します。

本文が**着地済み PR を明示している** Issue（「実際の着地は PR #<n>（merged）」等）も配りません。「open PR あり」の除外は merged PR を拾えないので、規則どおり配ると**空の PR を作らせます**。残りが実測・判断だけなら采配欄へ回します。

### 2. 並べ替え（上から順に当てはめる）

| 判定                                                                                        | 扱い                                                             |
| ------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| 本文に依存宣言があり、依存先が未 close（`依存` 節 / `Parent: #<n>` / 「〜が終わらないと」） | **配らない**。依存先が close されるまで待ち行列へ                |
| `type:human` ラベル                                                                         | **配らない**。人手の作業なので、こうだいさんへの一覧に分けて出す |
| `sev:blocking` > `sev:important` > `sev:minor`                                              | この順で先に置く                                                 |
| 同じ `sev:` 内                                                                              | Issue 番号の若い順                                               |

**依存の判定はラベルでは代替できません。** ラベル上は独立に見えても、本文が「〜が終わらないと始められない」と書いていることがあります。必ず本文を読みます。実例として #39（竜）は #36 / #37 / #38 の 3 件に依存していて、無視して配ると台本モードのまま「戦ってみる」に着手して空振りします。

同じファイルを 2 件に同時に触らせません（one writer per artifact）。Scope が重なる Issue は片方を待ち行列へ落とします。

### 3. `/goal` 条件の組み立て

条件は**英語・観測可能・4,000 文字以内**（判定モデルは Haiku）。1 本の `/goal` に入れるのは**依存の無い Issue だけ**です。

```
/goal in original-card-battle: every open issue below has a branch off origin/main, a green local run of npm run build, npm run lint, npm run test:run and dotnet test under unity-port, and an opened PR referencing it — #<n1> <title1>, #<n2> <title2>. Read .claude/CLAUDE.md Development Workflows first, keep the tracker out of implementation commits, and merge nothing yourself.
```

**終端は「PR を開くまで」で切ります。** merge と Issue の close はこうだいさんの手番（`settings.json` の `ask` で `gh pr merge` が止まる）なので、そこを条件に入れると人待ちで永久に達成されません。

### 4. 出力フォーマット

ブロックごとにヘッダと、続けて貼り付け用の 1 行。

```
## いま着手できる  ← 2 件（#36 #37）
<`/goal` 1 行>
停止: 2 本の PR が open になったら（merge は待たない）
```

最後に 3 行だけ添えます。

- **待ち行列**: 依存先が close されていない Issue を `#<n> ← #<依存先>` の形で列挙（無ければ「なし」）
- **人手待ち**: `type:human` の Issue 番号（無ければ「なし」）
- **除外**: open PR 済みでスキップした Issue 番号

## 安全則

- `/goal` は Claude が実行しません。**文字列を出すだけ**です。こうだいさんが貼ります
- 1 回の出力に `/goal` は 1 本まで。複数並べない（後勝ちで前が消える）
- 各ブロックに「いつ止めるか」を必ず 1 行添える
- `/goal` は CLI v2.1.139+ が要ります。古ければ `claude --version` を促す

## Gotchas

- **`gh pr list` は origin が古いと取りこぼします。** 収集前に `git fetch origin main`
- **`.claude/memory/INDEX.md` は git に遅れます。** 「merge された？」の判定は `gh pr list --json number,state,headRefName` の state が正。`git diff` / `git log` / `git cherry` は squash merge を誤判定します
- **worktree はまだ導入していません。** レーンへの振り分け（`section:` ラベル）が入るのは worktree の導入後です。それまでは「いま着手できる」1 本にまとめます

## 未導入（worktree 導入時に足すもの）

`section:<id>` ラベルによるレーン振り分けと、レーンごとの `/goal` 分割。導入の条件は `.claude/CLAUDE.md` の「Development Workflows」末尾の注記を参照。
