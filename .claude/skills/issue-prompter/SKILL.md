---
name: issue-prompter
description: open GitHub Issue を lane ラベルで worktree レーンごとに束ね、各レーンのチャットへ貼る /goal コマンド文字列を組み立てて表示する（chat-main 専用・読み取りのみ）。Triggers include "goal プロンプト", "各 worktree に配る", "レーンに投げる", "次どれやる", "貼り付け用プロンプト", "issue-prompter", "Issue を並べて", "着手順".
---

# issue-prompter — Issue → 各レーンの貼り付け用 `/goal` プロンプト

起票済みの open Issue を**宛先レーンごとに束ね直し、レーンごとに 1 本の `/goal` 文字列**にして表示します。配達物を宛先ごとに仕分けて送り状を書く役で、荷造り（起票）も配送（実装）も別の担当です。

- **上流 = `issue-dispatch`**（起票・ラベル付け）。本スキルは起票しません
- **下流 = 各レーンのチャット**（実装は `loop-implement`）。本スキルは実装しません
- **レーンの定義は `worktree-policy`** が正本です。ここは「Issue → レーン → `/goal` 文面」の組み立てだけを持ちます
- **`execution-router` の本リポ特化版**。モード選定の一般則はそちらが正本です
- **読み取り専用**: Issue・git・ファイルへ一切書きません。出力はチャットに表示するテキストだけです

## 起動条件

「各レーンに配って」「goal プロンプト作って」「今 open な Issue を並べて」。起票そのものを頼まれたら `issue-dispatch`、1 件の実装の進め方なら `lead-pipeline` か `loop-implement` へ。

## 手順

### 1. 収集（並列）

```bash
git fetch origin main
gh issue list -R sunbreak-pro/original-card-battle --state open --limit 200 --json number,title,labels,url,body
gh pr list -R sunbreak-pro/original-card-battle --state open --json number,title,headRefName,body
git worktree list
```

**open PR が既に紐づく Issue は除外**します（PR の本文かブランチ名に `#<n>` を含むもの）。着手済みを再度配ると二重実装になります。

ただし **`chore/tracker-*` ブランチの PR は数えません**。tracker の PR 本文は「今日触った Issue」を列挙するため、実装 PR と同じ扱いにすると未着手の Issue まで除外されます。

`status:frozen` ラベルの Issue も配りません。着手判断がこうだいさん待ちなので、采配欄に理由付きで残します。

本文が**着地済み PR を明示している** Issue（「実際の着地は PR #<n>（merged）」等）も配りません。「open PR あり」の除外は merged PR を拾えないので、規則どおり配ると**空の PR を作らせます**。残りが実測・判断だけなら采配欄へ回します。

着手順の下書きは `npm run issues:next -- --all` で出せます（`prio` → `sev` → 番号の順に並べ、依存待ち・人手待ち・親 Issue を分けます）。PR の除外と本文の読み込みはこのコマンドではやらないので、下の判定は省きません。

### 2. 宛先解決（上から順に当てはめる）

| 判定                                                                                        | 宛先                                                                        |
| ------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| 本文に依存宣言があり、依存先が未 close（`依存` 節 / `Parent: #<n>` / 「〜が終わらないと」） | **配らない**。依存先が close されるまで待ち行列へ（ラベルより先に判定する） |
| `type:human` ラベル                                                                         | **配らない**。人手待ちの一覧に分けて出す                                    |
| `lane:<slug>` ラベル                                                                        | その slug のレーン直行                                                      |
| `lane:` 無し。下の既定表で `area:` から 1 レーンに決まる                                    | そのレーン                                                                  |
| `area:` が複数レーンに跨る / どれにも当たらない / レーンの worktree が無い                  | **配らない**。「chat-main 采配」欄に番号だけ並べる                          |

`area:` からの既定（`lane:` が無いときだけ使う）:

| area:                             | レーン    |
| --------------------------------- | --------- |
| `area:cards` `area:enemy`         | `cards`   |
| `area:world` `area:ui` `area:art` | `design`  |
| `area:battle` `area:unity`        | `battle`  |
| `area:dungeon`                    | `dungeon` |
| `area:docs` `area:tooling`        | `audit`   |

推測で宛先を埋めません。`git worktree list` に無い slug 宛の Issue も采配欄へ回します。

**`audit` レーンは読み取り専用です**（`worktree-policy`）。コードの修正を含む Issue を `audit` へ配りません。監査・棚卸し・設計書と実装の突き合わせだけを配り、修正が要るものは担当レーンか采配欄へ落とします。

**依存の判定はラベルでは代替できません。** ラベル上は独立に見えても、本文が「〜が終わらないと始められない」と書いていることがあります。必ず本文を読みます。実例として #39（竜）は #36 / #37 / #38 の 3 件に依存していて、無視して配ると台本モードのまま「戦ってみる」に着手して空振りします。

同じファイルを 2 レーンに触らせません（one writer per artifact）。Scope が重なる Issue は片方を采配欄へ落とします。

### 3. `/goal` 条件の組み立て

条件は**英語・観測可能・4,000 文字以内**（判定モデルは Haiku）。レーンの open Issue を 1 本にまとめます。1 本の `/goal` に入れるのは**依存の無い Issue だけ**です。**並べる順は `prio:` の小さい順**（同じなら `sev:`、番号）で、`prio:1` が残っているレーンには `prio:3` 以下を混ぜません。`prio:` が付いていない Issue は配らず、采配欄に「優先順位なし」として出します。

```
/goal in the original-card-battle worktree for <slug>: every open issue below has its own branch off origin/main, a local run of npm run build, npm run lint, npm run test:run and dotnet test under unity-port that all exit 0, and an opened PR referencing it — #<n1> <title1>, #<n2> <title2>. Read .claude/skills/worktree-policy/SKILL.md first, update .claude/comm/.session-branch on every branch switch, keep the tracker out of implementation commits, run no dev server, and merge nothing yourself.
```

`audit` レーンの終端は PR ではなく Issue の起票です。

```
/goal in the original-card-battle worktree for audit: every open issue below is answered with a filed GitHub issue or a comment on the original that cites file:line evidence — #<n1> <title1>. Read .claude/skills/worktree-policy/SKILL.md first; this lane is read-only, so change no source file and open no pull request.
```

**実装レーンの終端は「PR を開くまで」で切ります。** merge と Issue の close はこうだいさんの手番（`settings.json` の `ask` で `gh pr merge` が止まる）なので、そこを条件に入れると人待ちで永久に達成されません。

### 4. 出力フォーマット

レーンごとに 1 ブロック。ヘッダに宛先と件数、続けて貼り付け用の 1 行。

```
## <slug>  ← lane:<slug> / 2 件（#36 #37）
<`/goal` 1 行>
停止: 2 本の PR が open になったら（merge は待たない）
```

最後に 4 行だけ添えます。

- **chat-main 采配**: 宛先が決まらなかった Issue 番号の列挙（無ければ「なし」）
- **待ち行列**: 依存先が close されていない Issue を `#<n> ← #<依存先>` の形で列挙（無ければ「なし」）
- **人手待ち**: `type:human` の Issue 番号（無ければ「なし」）
- **除外**: open PR 済みでスキップした Issue 番号

## 安全則

- `/goal` は Claude が実行しません。**文字列を出すだけ**です。こうだいさんが各レーンのチャットに貼ります
- **1 レーンに 1 本まで**。同じレーンへ複数の `/goal` を並べない（後勝ちで前が消える）
- 各ブロックに「いつ止めるか」を必ず 1 行添える
- `/goal` は CLI v2.1.139+ が要ります。古ければ `claude --version` を促す

## Gotchas

- **検証 4 本は origin/main で緑です**（2026-09-21、#44）。`npm run lint` の警告 2 件（不要になった `eslint-disable`）は exit 0 のまま残しています。レーンで赤が出たら、そのレーンの変更が原因です
- **`gh pr list` は origin が古いと取りこぼします。** 収集前に `git fetch origin main`
- **worktree が `chore/lane-*` の待機ブランチにいるのは正常**です。着手中とは限らないので、手番の判定に使いません
- **`chore/tracker-*` ブランチにいるのも正常**です（tracker 分離）
- **`.claude/memory/INDEX.md` は git に遅れます。** 「merge された？」の判定は `gh pr list --json number,state,headRefName` の state が正。`git diff` / `git log` / `git cherry` は squash merge を誤判定します
- **レーンの手番確認は各 worktree の `.claude/memory/INDEX.md`** です。worktree ごとに別物なので、chat-main の INDEX には他レーンの進捗が出ません。迷ったらブランチの実測で裏取りします
