# .claude/comm/ — チャット間の受け渡し

並行チャットが互いの作業を踏まないための置き場です。**1 ファイル 1 書き手**（single writer）を守ります。

| 置き場 | 書いてよいのは | 何を置くか |
| --- | --- | --- |
| `.session-name` | 自分（git 非追跡） | 自分のチャット名 1 行。`chat-` 接頭辞は付けない（例: `main`） |
| `.session-branch` | 自分（git 非追跡） | いま作業しているブランチ名。切り替えるたびに書き換える |
| `handoff/` | 作るのはレーン、消すのは main（git 非追跡） | main への申し送り。Issue にしてほしい課題を 1 件 1 ファイルで置く |
| `outbox/chat-<self>/` | 自分だけ | 他チャットへの連絡、PR 本文の下書き、停止したループの仮説。起票の依頼は置かない |

## 申し送り（Issue を立てるのは main だけ）

**GitHub Issue を立てる（`gh issue create`）のは chat-main だけです**（2026-10-06 こうだいさん決定、#367）。レーンは Issue を立てず、課題を申し送りとして `handoff/` に書いて先へ進みます。main が申し送りを回収し、重複を確かめてから `issue-dispatch` で Issue にします。life-editor の「起票は chat-main 一元」（2026-07-11）を移植したものです。

レーンは自分の担当範囲しか見ていないので、同じ課題が別々のレーンから立ち、ラベルの付け方もばらつきます。正本の食い違いは、どちらの文書を直すかをレーン単独では決められません。2026-10-03〜06 には、立った Issue の約半分がレーン発の「正本の食い違い」でした。

共通の hook（claude-dotfiles の `bash-guards.mjs`）が、`.claude/issue-workflow.md` の `issueCreation` が `main-only` なのを読んで、`.session-name` が `main` でないチャットの `gh issue create` を止めます。

### レーンがすること

1. `cp .claude/comm/handoff/_TEMPLATE.md .claude/comm/handoff/YYYY-MM-DD-<self>-<slug>.md` で 1 件 1 ファイルを作る
2. frontmatter の `kind` を選び、`area` `lane` `prio` に案を書く。値は案で、main が決め直す
3. 本文に根拠（`file:line` か `file:§`）、直し方の案、DoD の案を書く。`kind: conflict` は「どちらに従って進めたか」も書く
4. 止まらずに自分の Issue を続ける。PR 本文には「申し送り `<ファイル名>` を main に回した」と 1 行書く

書いた後のファイルは書き換えません。直したいときは新しいファイルを足します。main が読んでいる最中に中身が変わるのを避けるためです。

レーンがしてよいことは、自分宛ての Issue へのコメント（進捗・調査結果・PR へのリンク）と、PR の `Closes #n` による close です。Issue の作成、他レーン宛ての Issue のラベル変更、`gh api` で issues へ直接書き込むことはしません。

### main がすること

作業の区切りと、`issue-workflow` を回す前と後に、全 worktree の申し送りを集めます。`issue-workflow` が作る `wf-*` の worktree も含みます。

```bash
git worktree list --porcelain | sed -n 's/^worktree //p' | while read -r wt; do
  ls "$wt"/.claude/comm/handoff/*.md 2>/dev/null | grep -v '/_TEMPLATE\.md$'
done
```

1 件ずつ、共通の `issue-dispatch` の手順 2 から 4（重複・ファイルの重なり・起票）を通します。既存の Issue と重なれば、新しく立てずにその Issue へコメントします。前提がコードで否定されたら Issue にしません。どの場合も、処理を終えたファイルは main が消します。Issue の本文には「申し送り: `<lane>/<ファイル名>`」と 1 行残します。

起票するときは、このリポジトリだけの次の決まりも当てます。

- **frontmatter の `prio` と `lane` は案です。** main が、台帳（`.claude/docs/SOURCES.md` §2）とほかの open な Issue を見て決め直します。
- **Scope に設計書を書くときは、台帳の持ち主と `lane:` を揃えます。** 持ち主でない担当に設計書を書かせる Issue は立てません。
- **設計書どうしの食い違いは、題を「元になる文書の食い違い: <何と何が>」で始めます。** 本文には両方の `file:§` と、直し方ごとに変わる結果を書きます。どちらを直すかが決まるまで `lane:` を付けません。#116「正本の食い違い: 鈍足と予兆の一字が長柄の歪み兵と噛み合わない（battle_core_v4 §5 / §6 と roster §2.1）」が型です。#116 は書き出しを変える前の題なので、旧い書き出しのままです。

### `pdca:auto` を付ける判断

進行役（pdca-harness）に任せる Issue には `pdca:auto` を付けます。付けるのは chat-main だけで、担当の作業場所のチャットは付けません。付いた Issue は、`issue-workflow` が実装に回しません。

付けてよい Issue は、コードだけで完結し、完了条件を `dotnet build` `dotnet test` `npm run lint` `npm run build` `npm run test:run` で確かめられるものです。次の Issue には付けません。

- 本文が `.claude/` のパスを挙げるもの（設計書を直す Issue を含む）。進行役は `.claude/` に書けず、機械の足切りで外れます。本文に `.claude/` のパスを書くと拾われないので、付けたい Issue では、参照先を設計書の名前だけにするか、Scope に書きません。
- `area:art` `area:world` `type:human` `status:frozen` が付いたもの、題が判断を求めるもの、子 Issue を持つ親。
- 判断（数値・規則・画面の流れ）を Issue が決めていないもの。進行役は仮定を置かず、`state:needs-human` にします。

Unity Editor での確認が要る完了条件があっても、付けてかまいません。その条件は PR の本文の「人が確かめること」に回り、こうだいさんが確かめます。`unity:check` と `parity:check` は、進行役が動かせません。

試運転が済むまでは、付ける Issue を少なくします。いまの試運転は #457「[戦闘] 試験台 BattleCore.Sim で層ごとの難しさ（M1・M2・M3）を測れるようにする」です。#99「[探索] 探索と戦闘を繋ぐ（BattleInit へ渡す、結果を持ち帰る）」は、試運転に問題がなければ足します。2 件とも、2026-11-15 の時点で close 済みです。広げるときは、PR の質と merge の手間を見てから決めます（merge 待ちの上限は 3 件です）。

### 起票しない範囲

`.claude/CLAUDE.md` で凍結した範囲は Issue にしません。アーマー（AP / 装備耐久 / 修理）、装備・Gold・ソウル経済、ショップ / 鍛冶屋 / サンクチュアリが当たります。防御は Guard だけです。TS 側の `src/ui/battle-lab/core/` も凍結していて、戦闘コアの正は `unity-port/BattleCore/` です。

### なぜ git で追跡しないか

申し送りは各 worktree に置いたまま、main が `git worktree list` のパスを直接読みます。追跡すると PR がマージされるまで main に届かず、レーンが Issue ごとにブランチを切り替えるたびに付いて回ります。life-editor は outbox を追跡していて、マージ前のエントリを main が取りこぼしました（2026-07-31）。

worktree を消す前に、`handoff/` が空であることを確かめます。`git worktree remove` は無視されたファイルを黙って消します。

## 使いかた

**判断が要るとき**: 止まらずに仮定を置いて進め、作業の最後の報告のあとに AskUserQuestion（選択式の質問）でまとめて聞きます。聞き方の元は、共通の決まり（`~/.claude/CLAUDE.md` の前提「ユーザーは見ていない」）です。

**他チャットに何かしてほしいとき**: 相手のファイルを直接書かず、`outbox/chat-<self>/` に依頼を append します。Issue にしてほしい課題だけは `handoff/` です。

**読むとき**: 他チャットの `outbox/` は読み取りのみ。書き換えません。

## 規約

- `outbox/` の更新は、実装コミットに混ぜない（tracker と同じ理由で衝突する）
- `.session-name` が未宣言だと `task-tracker` が per-chat モードで止まります。`echo main > .claude/comm/.session-name` で宣言してください
