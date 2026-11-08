---
name: merge-round
description: chat-main 専用（例外は issue-workflow で「merge-round まで回す」を選んだ回の work-finishers）。open PR を事前確認してから順番に merge し、文書・lockfile だけの衝突は chat-main が一時 worktree で直し、残りの衝突と CI の赤はレーンごとの /goal 指示文にまとめ、回を閉じて戻す地点を記録する。merge 後に戻す手順も持つ。作業レーンでの merge と Issue の close には使わない。Triggers include "merge して", "まとめて merge", "PR を片付けて", "merge-round", "マージの回", "revert したい", "merge を戻したい".
---

# merge-round — chat-main の merge を 1 回ぶんまとめる

life-editor の merge-round（2026-11-02）をこのリポジトリに写したもの（2026-11-04 こうだいさん依頼）。こうだいさんがこのスキルを起動したら、それがその回の merge の承認になる。`issue-workflow` の起動前の確認で「merge-round まで回す」を選んだ回も承認に数え、仕上げ役の `work-finishers` が回す（下の「work-finishers が回すとき」）。事前確認を通った PR は確認ダイアログなしで merge する。`hooks/merge-gate.mjs` が、回の一覧にある PR の決まった形の merge だけを通す。

宅配便の集荷にたとえると、積める荷物を全部積んでから、積めなかった荷物の送り主にまとめて連絡するやり方です。1 本 merge するたびに止まって聞くことはしない。

## 前提

- chat-main（`C:\Users\user\orca\original-card-battle`、ブランチは `main`）で動かす。worktree の中では hook が merge を止める。
- 最初に `git pull --ff-only` を打つ。手元に変更があって pull できないときは、その変更を報告して止まる。
- スクリプトは GitHub に書き込まない。merge は Claude が `gh pr merge` を 1 本ずつ打つ。
- merge の形は merge commit（`--merge`）。このリポジトリの履歴がそうなっているため（`Merge pull request #n`）。squash にはしない。
- このリポジトリは PR に CI を走らせていない（2026-11-04 時点）。チェックが付いていない PR は「通った」とみなす。PR 用のワークフローを足したら、`scripts/merge-round.mjs` の `CI_WORKFLOW` にファイル名を書く。それまで「CI が赤い」の判定は、PR に何かのチェックが付いて落ちたときだけ起きる。

## 手順

### 1. 事前確認と順番

```bash
node .claude/scripts/merge-round.mjs plan
```

表の「判定」は次の 3 つになる。

| 判定 | 当たる PR |
| --- | --- |
| merge | 手元の `git merge-tree` で main と衝突せず、CI があれば通り、レーンの手元と PR の先頭が一致し、依存先が main に着地済みか今回の回で先に merge される |
| 直してもらう | main と衝突している、または CI が赤い |
| 今回は見送り | 下書き / `merge:hold` ラベル / `Closes #番号` も `Refs #番号` もブランチ名の番号も無い / Issue が `status:frozen` / CI 実行中 / レーンに未コミットの変更がある / 依存先が未着地 |

PR が片付ける Issue は、本文の `Closes #n`（CLAUDE.md の決まり）か `Refs #n`、無ければブランチ名の末尾の番号（`feat/battle-822` の 822）から読む。依存は Issue 本文の「## 依存」節から読む（`npm run issues:next` と同じ読み方）。

順番は、依存の前後を守ったうえで、実装と revert を先に、記録（`chore/tracker-*`）を後にする。同じ回の中では、他の PR と同じファイルを触る数が少ない PR から、古い PR から並べる。衝突しない PR を先に積み、衝突しうる PR を後ろに寄せるためです。

終了コード 3 は「main の CI が赤い」を表す（CI を設定したあとだけ起きる）。そのときは revert の PR（`revert/` で始まるブランチ）だけが「merge」になり、ほかは見送りになる。下の「main が赤いとき」に進む。

### 2. 回を始める

```bash
node .claude/scripts/merge-round.mjs start
```

plan の「merge」の PR を今回の一覧として `.git/merge-round/active.json` に書き、戻る地点（この回の前の main の commit）を表示する。plan が 30 分より古いときと、plan の後に main が進んだときは断るので、plan からやり直す。

### 3. 1 本ずつ merge する

順番どおりに、各 PR で次の 2 つを続けて打つ。

```bash
node .claude/scripts/merge-round.mjs check <n>
gh pr merge <n> -R sunbreak-pro/original-card-battle --merge
```

`gh pr merge` はこの形で、`check` の直後に単独で打つ。`&&` でつなぐ、`--admin` を付ける、`--delete-branch` を付けると hook が確認を出す。ブランチを消さないのは、レーンがそのブランチを開いたままのことがあるからです。ブランチの掃除は `finished-work` スキルで別にやる。

`check` の終了コードで次を決める。

| 終了コード | 意味 | 次 |
| --- | --- | --- |
| 0 | merge できる | `gh pr merge` を打つ |
| 3 | 前の merge で main と衝突した | 飛ばす。手順 4 で指示文になる |
| 4 | mergeable が 60 秒たっても決まらない | 飛ばす |
| 5 | plan の後に push された | 飛ばす。次の回で確認し直す |
| 11 | CI が通っていない（チェックが付いて落ちた等） | 飛ばす。手順 4 で指示文になる |
| それ以外 | close 済み・`merge:hold`・下書き・merge 済み・スクリプトの失敗 | 飛ばす。理由を報告に書く |

0 以外はすべて飛ばす。hook は `check` が 0 を返した PR しか確認なしで通さないので、`check` を省いて merge すると確認ダイアログが出る。`gh pr merge` が失敗したら、その出力を報告に書いて次の PR に進む。

### 4. 衝突と CI の赤を振り分ける

```bash
node .claude/scripts/merge-round.mjs fixes
```

`fixes` の前に、`issue-workflow` が作った `wf-*` の worktree のうち PR を開いたものを、申し送りを集めてから消しておく（`.claude/issue-workflow.md` の「実行のあと」）。`fixes` は `workspaces/` の下の worktree をすべてレーンとして数える。`wf-<n>` が残っていると、だれもいない worktree 宛ての `/goal` を出し、そのブランチは「レーンが開いている」扱いで 4a の自動の直しからも外れる。

最新の main に対して全 open PR を `git merge-tree` で確かめ、衝突した PR と CI が赤い PR を 2 つに分ける。

- **chat-main が直す**: 衝突が文書（`.md`）と lockfile だけで、CI は赤くなく、どのレーンもそのブランチを開いていない PR。手順 4a で直す。task-tracker の記録（`.claude/memory/chat-*.md` `.claude/history/chat-*.md`）の衝突はほぼここに入る。
- **レーンに直してもらう**: それ以外。手順 4b の指示文になる。

レーンがブランチを開いている PR を chat-main が直さないのは、同じブランチに書き手が 2 人いると、レーンの手元と push がずれるからです（one writer per artifact）。

#### 4a. chat-main が直す

PR ごとに一時 worktree（`C:\Users\user\orca\workspaces\original-card-battle\autofix-<n>`）を作り、main を merge して衝突を出す。

```bash
node .claude/scripts/merge-round.mjs autofix-start <n>
```

出力の「サブエージェントへの指示」をそのまま Agent ツールに渡し、`model: sonnet` で起動する。複数あれば 1 メッセージで並べて起動する（同時 5 体まで）。サブエージェントは衝突を解いて merge の commit まで作り、push はしない。

サブエージェントが終わったら、PR ごとに確かめて push する。

```bash
node .claude/scripts/merge-round.mjs autofix-push <n>
```

`autofix-push` は次をすべて確かめてから、PR のブランチへ fast-forward で push し、一時 worktree を消す。1 つでも外れたら push せずに止まる。

- HEAD が「PR の先頭に、そのときの main を merge した commit」になっている
- 自動の merge の結果と比べて、変わったのが衝突したファイルだけ
- 衝突の印が残っておらず、JSON が読める
- 作業の間にレーンが push しておらず、ブランチを開いてもいない

止まったときと、サブエージェントが「判断できない」と返したときは、`autofix-abort <n>` で片付けて、もう一度 `fixes` を打つ。`autofix-abort` はその PR の先頭を覚えておくので、同じ先頭のあいだ `fixes` はその PR をレーンへの指示文に回す。

push した PR は次の回の plan で merge できる。レーンがあとでそのブランチを開くときは、`worktree-policy` のとおり `git pull --ff-only` で取り込む。

#### 4b. レーンに直してもらう

PR のブランチを開いているレーン（無ければ Issue の `lane:` ラベル、それも無ければ `area:` から CLAUDE.md の表で決めたレーン）ごとに 1 本の `/goal` にまとめる。出力はそのまま報告に貼る。モデルは衝突したファイルで決める。

- Haiku 5.5: 衝突が文書（`.md`）と lockfile だけ
- Sonnet 5.5: 衝突がテスト（`__tests__/` `tests/`）に収まる
- Opus 5.5: それ以外のコードが衝突している、または CI が赤い

指示文の検証は `npm run build` `npm run lint` `npm run test:run` で、衝突に C# が混じるときは `dotnet test` も入る。製品の判断が要る衝突は、レーンが `.claude/comm/handoff/` に申し送りを書いて止まる。

前回と同じ head の PR には「前回と同じ指示」と付くので、レーンがまだ直していないと分かる。レーンが別のブランチで作業中なら、いまの `/goal` が終わってから貼るよう注意が付く。宛先が決まらない PR（`area:world` など main が持つもの）は main が自分で直す。

### 5. 回を閉じる

```bash
node .claude/scripts/merge-round.mjs finish
```

merge した PR と merge commit を新しい順に表示し、`.git/merge-round/rounds/<回の ID>.json` に残す。次の回の plan が結果を確かめる。

### 6. 報告

次の順で書く。

1. merge した PR の表（PR・題・merge commit の先頭 8 文字）と、戻る地点の commit
2. chat-main が直して push した PR（解いたファイルと、どちらを残したか）と、直せずにレーンへ回した PR
3. `fixes` のレーン宛ての出力（レーンごとの `/goal`、モデル、停止条件）
4. 見送った PR と理由
5. merge 後に残る作業: 試運転（`npm run dev` と Unity Editor はメインだけ）、Issue の close（`Closes #n` なら merge で閉じる）、merge 済みブランチと worktree の掃除（`finished-work`）

レーンが直し終えたら、こうだいさんがもう一度このスキルを起動する。2 回目以降も手順は同じです。

## work-finishers が回すとき

`issue-workflow` の仕上げ役（サブエージェント `work-finishers`）が、その回の最後に 1 度だけこのスキルを回すことがある。手順は上と同じで、違うのは次のところだけ。このリポジトリの細かい決まりは `.claude/issue-workflow.md` の「仕上げ」にある。

- 承認は、こうだいさんが `issue-workflow` の起動前の確認（chat-main の AskUserQuestion）で「merge-round まで回す」を選んだことで、その回に限る。これを人の起動に数える。選ばれていない回では回さない。選んだ記録は chat-main が `.git/merge-round/finisher-approved.json` に書き、hook は印が無いか 6 時間より古いと `work-finishers` の merge を deny する。手順 5 の `finish` の後と、途中でやめたときに、`work-finishers` が印を消す。
- 手順 1 は `node .claude/scripts/merge-round.mjs plan --only <その回が開いた PR をカンマでつないだもの>` にする。ほかの open PR は一覧に入れない。
- 手順 4a はサブエージェントを起動せず、自分で直す（サブエージェントはサブエージェントを起動できない）。`autofix-start <n>` の「サブエージェントへの指示」に自分で従い、一時 worktree で衝突を解いて merge の commit まで作る。push は `autofix-push <n>` に任せる。判断できない衝突は `autofix-abort <n>` で片付けて、`fixes` を打ち直す。
- `gh pr merge` に hook が確認（ask）か拒否（deny）を返したら、その PR を飛ばして報告に書く。打ち直しも、形を変えた回り道もしない。hook は `work-finishers` に対してもサブエージェントの deny を外すだけで、ほかの判定は chat-main と同じに効く。
- 報告は手順 6 の順で書き、chat-main に返す。4b の `/goal` は chat-main がレーンに貼る。

## 止める・戻す

### merge の前に止める

- 特定の PR を外す: `gh pr edit <n> -R sunbreak-pro/original-card-battle --add-label merge:hold`。plan は見送りにし、回の途中なら `check` が終了コード 8 で飛ばす。
- 回ごと止める: 残りの PR を merge せずに `finish` を打つ。merge 済みの分はそのまま残る。

### merge した後に戻す

main を書き換えない（`reset --hard` と force push は deny 済み）。revert の PR を作り、それもこの回の手順で merge する。

1. 戻す commit を決める。1 本なら `finish` の表か `gh pr view <n> --json mergeCommit` の commit。回ごとなら `.git/merge-round/rounds/<回の ID>.json` の `merged` を新しい順に並べる。
2. 一時 worktree で revert する（パスは `worktree-policy` のとおり絶対パスで書く）。merge commit を戻すので `-m 1` を付ける。

   ```bash
   git fetch origin
   git worktree add C:/Users/user/orca/workspaces/original-card-battle/revert-<n> -b revert/pr-<n> origin/main
   cd C:/Users/user/orca/workspaces/original-card-battle/revert-<n>
   echo revert/pr-<n> > .claude/comm/.session-branch
   git revert --no-edit -m 1 <新しい merge commit> <その次> ...
   git push -u origin revert/pr-<n>
   gh pr create -R sunbreak-pro/original-card-battle --title "revert: <元の題> (#<n>)" --body "Reverts #<n>. Refs #<Issue>"
   ```

3. chat-main に戻り、`plan --only <revert PR>` → `start` → `check` → `gh pr merge` → `finish` で merge する。
4. 回ごと戻したときは、`git fetch origin` のあと `git diff <戻る地点> origin/main --stat` を打ち、差分が空か、戻した回より後に merge した PR の分だけになっているかを確かめる。
5. 元の PR が `Closes #n` で Issue を閉じていたら、Issue を reopen して戻した理由をコメントする。直した PR は chat-main が `issue-workflow` の次の回で出し直す。
6. 元の PR のブランチを消す。`git push origin --delete <元のブランチ>` と `git branch -d <元のブランチ>` を打つ（merge 済みなので `-d` で消せる）。

`issue-workflow` は revert の PR（ブランチ `revert/pr-*`）を着地の照合から外し、本文の `Reverts #<n>` で戻された元の PR も着地に数えない。ただし元の PR のブランチが origin か手元に残っていると、reopen した Issue は「着手済み（ブランチあり）」で外れる。6 でブランチを消すと、Issue は次の回の候補に戻る。

### main が赤いとき

CI を設定したあとだけ起きる。`plan` が終了コード 3 で止まる。`gh run view <run の URL>` で落ちたステップを見て、直近の回の PR のどれが原因かを決める。原因の PR を上の手順で revert するか、原因の Issue を `issue-dispatch` で起票して直す。どちらにするかはこうだいさんに聞く（AskUserQuestion、推奨は revert）。main が緑に戻るまで新しい回を始めない。

## 安全則

- 作業レーンでは使わない。レーンからの `gh pr merge` は hook が deny する。サブエージェントも、`work-finishers` のほかは deny する。
- 人の起動なしに回を始めない（定時実行・無人ループ・pdca-harness に載せない）。人の起動に数えるのは、こうだいさんがこのスキルを起動したときと、`issue-workflow` の起動前の確認で「merge-round まで回す」を選んだその回だけ（上の「work-finishers が回すとき」）。
- Issue の close はこのスキルで行わない（`Closes #n` による自動 close は除く）。
- 回の一覧にない PR を merge したくなったら、plan からやり直して一覧に入れる。hook の確認ダイアログで押し通さない。
- chat-main がレーンの PR のブランチへ push するのは `autofix-push` だけにする。fast-forward だけで、force push はしない。レーンが開いているブランチには push しない。

## Gotchas

- GitHub の `mergeable` は merge の直後しばらく `UNKNOWN` になる。plan と fixes は手元の `git merge-tree` で衝突を決め、`check` だけが GitHub の値を最大 60 秒待つ。
- merge 済みの判定は `gh pr view` の state で行う（`worktree-policy`）。
- レーンとして数える worktree は `C:\Users\user\orca\workspaces\original-card-battle\<slug>` だけ。リポジトリの中の `.claude/worktrees/`（サブエージェントと Workflow の一時 worktree）は数えない。
- 状態は `.git/merge-round/` にあり、commit されない。回は 2 時間で切れ、切れた後の merge は hook が確認を出す。
- settings.json には `gh pr merge` の allow も ask も置かない。ask を置くと hook の allow が効かず（Claude Code の仕様）、allow を置くと hook が落ちたときに素通りするためです。
