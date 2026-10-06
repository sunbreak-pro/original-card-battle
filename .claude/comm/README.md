# .claude/comm/ — チャット間の受け渡し

並行チャットが互いの作業を踏まないための置き場です。**1 ファイル 1 書き手**（single writer）を守ります。

| 置き場 | 書いてよいのは | 何を置くか |
| --- | --- | --- |
| `.session-name` | 自分（git 非追跡） | 自分のチャット名 1 行。`chat-` 接頭辞は付けない（例: `main`） |
| `.session-branch` | 自分（git 非追跡） | いま作業しているブランチ名。切り替えるたびに書き換える |
| `handoff/` | 作るのはレーン、消すのは main（git 非追跡） | main への申し送り。Issue にしてほしい課題を 1 件 1 ファイルで置く |
| `outbox/chat-<self>/` | 自分だけ | 他チャットへの連絡、PR 本文の下書き、停止したループの仮説。起票の依頼は置かない |
| `decisions/chat-<self>.md` | 自分だけ | 判断が割れて止まった点。A/B と推奨、放置したときの挙動を書く |
| `decisions/ANSWERS.md` | こうだいさんだけ | `decisions/` の問いへの回答。1 問 1 行 |

## 申し送り（Issue を立てるのは main だけ）

**GitHub Issue を立てる（`gh issue create`）のは chat-main だけです**（2026-10-06 こうだいさん決定、#367）。レーンは Issue を立てず、課題を申し送りとして `handoff/` に書いて先へ進みます。main が申し送りを回収し、重複を確かめてから `issue-dispatch` で Issue にします。life-editor の「起票は chat-main 一元」（2026-07-11）を移植したものです。

レーンは自分の担当範囲しか見ていないので、同じ課題が別々のレーンから立ち、ラベルの付け方もばらつきます。正本の食い違いは、どちらの文書を直すかをレーン単独では決められません。2026-10-03〜06 には、立った Issue の約半分がレーン発の「正本の食い違い」でした。

`.claude/hooks/pre-issue-create-guard.sh` が、`.session-name` が `main` でないチャットの `gh issue create` を止めます。

### レーンがすること

1. `cp .claude/comm/handoff/_TEMPLATE.md .claude/comm/handoff/YYYY-MM-DD-<self>-<slug>.md` で 1 件 1 ファイルを作る
2. frontmatter の `kind` を選び、`area` `lane` `prio` に案を書く。値は案で、main が決め直す
3. 本文に根拠（`file:line` か `file:§`）、直し方の案、DoD の案を書く。`kind: conflict` は「どちらに従って進めたか」も書く
4. 止まらずに自分の Issue を続ける。PR 本文には「申し送り `<ファイル名>` を main に回した」と 1 行書く

書いた後のファイルは書き換えません。直したいときは新しいファイルを足します。main が読んでいる最中に中身が変わるのを避けるためです。

レーンがしてよいことは、自分宛ての Issue へのコメント（進捗・調査結果・PR へのリンク）と、PR の `Closes #n` による close です。Issue の作成、他レーン宛ての Issue のラベル変更、`gh api` で issues へ直接書き込むことはしません。

### main がすること

作業の区切りと `issue-prompter` を回す前に、全 worktree の申し送りを集めます。

```bash
git worktree list --porcelain | sed -n 's/^worktree //p' | while read -r wt; do
  ls "$wt"/.claude/comm/handoff/*.md 2>/dev/null | grep -v '/_TEMPLATE\.md$'
done
```

1 件ずつ `issue-dispatch` の手順 2（重複チェック）と 3（起票）を通します。既存の Issue と重なれば、新しく立てずにその Issue へコメントします。前提がコードで否定されたら Issue にしません。どの場合も、処理を終えたファイルは main が消します。Issue の本文には「申し送り: `<lane>/<ファイル名>`」と 1 行残します。

### なぜ git で追跡しないか

申し送りは各 worktree に置いたまま、main が `git worktree list` のパスを直接読みます。追跡すると PR がマージされるまで main に届かず、レーンが Issue ごとにブランチを切り替えるたびに付いて回ります。life-editor は outbox を追跡していて、マージ前のエントリを main が取りこぼしました（2026-07-31）。

worktree を消す前に、`handoff/` が空であることを確かめます。`git worktree remove` は無視されたファイルを黙って消します。

## 使いかた

**判断が要るとき**: 止まらずに `decisions/chat-<self>.md` へ A/B と推奨を書いて次へ進みます。回答は `ANSWERS.md` に 1 行で返ります。

**他チャットに何かしてほしいとき**: 相手のファイルを直接書かず、`outbox/chat-<self>/` に依頼を append します。Issue にしてほしい課題だけは `handoff/` です。

**読むとき**: 他チャットの `outbox/` と `decisions/` は読み取りのみ。書き換えません。

## 規約

- `outbox/` と `decisions/` の更新は、実装コミットに混ぜない（tracker と同じ理由で衝突する）
- `.session-name` が未宣言だと `task-tracker` が per-chat モードで止まります。`echo main > .claude/comm/.session-name` で宣言してください
