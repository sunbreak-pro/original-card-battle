# Plan: claude-dotfiles の共通の決まりに、このリポジトリを合わせる

- **Status**: IN PROGRESS（Step 1〜4 が終わりました。Step 6・7・9 は PR を作成済みで、merge 待ちです）
- **Created**: 2026-11-14
- **Task**: claude-dotfiles の共通の決まりと重なる決まり・道具をこのリポジトリから消し、文書の置き場を標準に合わせる
- **Project**: original-card-battle
- **計画書**: `.claude/docs/vision/plans/2026-11-14-align-with-shared-rules.md`
- **元になる計画**: claude-dotfiles の `claude/docs/plans/2026-11-11-project-rollout.md`（下位のプロジェクトを共通の決まりに合わせる計画）の Step 12
- **作業の大きさ**: PR 9 本と、スキルの語を直す PR が数本の見込みです。こうだいさんの確認が要る Step は 1・5・8 の 3 個です

## Context

### 何のための計画か

claude-dotfiles の `claude/CLAUDE.md`・`claude/rules/`・`claude/skills/` は `~/.claude/` に入り、どのプロジェクトでも自動で読まれます。そのため、このリポジトリが同じ決まりや同じ道具を別に持つと、片方だけが直されて食い違います。この計画は、重なっているものをこのリポジトリから消し、残すものを「このリポジトリだけの事実と値」に絞ります。

引っ越しにたとえると、新居（共通の決まり）に備え付けの家具があるのに、古い家具も運び込んで二重になっている状態です。古いほうを手放し、自分の持ち物だけを残します。

こうだいさんは 2026-11-14 に、7 つのプロジェクトのうち、このリポジトリを最初に合わせると決めました。ここで試した結果で、検査と進め方を直します。

### いま起きていること

2026-11-14 に、claude-dotfiles のプロジェクトの検査（`check-project.mjs`）を、main の `7f688d71` に読み取りだけで当てました。エラーが 237 件、警告が 3 件です。

| 検査 | 件数 | 中身 |
| --- | --- | --- |
| 内輪の語 | エラー 154 件 | `.claude/CLAUDE.md`・スキル・`.claude/issue-workflow.md` にある語です |
| 記号と番号の組 | エラー 68 件 | 多くは `visual-production-pipeline` スキルの工程の記号です |
| 共通の道具と同じ名前 | エラー 4 件 | スキル 3 本とスクリプト 1 本です |
| リポジトリの外を指す symlink | エラー 3 件 | `.claude/agents/` の 3 本です |
| 案内だけの進捗のファイル | エラー 3 件 | `.claude/MEMORY.md`・`.claude/HISTORY.md`・`TODO.md` です |
| 入口の文書への書き写し | エラー 2 件、警告 3 件 | 計画書の置き場の語と、merge のコマンドです |
| 計画書の置き場 | エラー 1 件 | 2 か所あります |
| 決定の記録の置き場 | エラー 1 件 | `.claude/comm/decisions/` です |
| 追跡された個人の設定 | エラー 1 件 | `.claude/settings.local.json` です |

検査が数えないものも、次のとおり確かめました。

- **入口の文書。** `.claude/CLAUDE.md` は 346 行で、`2026-` で始まる日付が 19 個あります。「いつ誰が決めたか」が、今の決まりと同じ行に入っています。
- **symlink（別の場所を指すだけのファイル）。** `.claude/agents/` の 3 本は、Mac の `/Users/newlife/dev/Claude/agents-lib/projects/original-card-battle/` を指しています。この PC の `C:/Users/user/dev/Claude/` には `hooks-lib` しか無く、指す先の中身がありません。3 本のエージェントは、この PC では動いていません。
- **計画書。** `.claude/docs/vision/plans/` に 14 本、`.claude/feature_plans/` に 4 本あります。後者の 4 本は前者に同じ名前があり、1 本は中身が同じで、3 本は中身が違います。前者のうち日付の無い 8 本には、Status の欄がありません。
- **hook（Claude Code 本体が決まった場面で自動で動かすスクリプト）。** `.claude/settings.json` に 7 本の登録があります。7 本とも、claude-dotfiles の 2 本（`bash-guards.mjs` と `project-session-start.mjs`）に置き換わります。4 本は `$HOME/dev/Claude/hooks-lib/` を先に読み、無ければ同梱の `.claude/scripts/hooks-lib/` を使います。
- **README。** `README.md`（121 行）は、Python 版のころの説明（`python main.py` で起動）のままです。入口の文書が言う「Development History」の節はありません。
- **開いている PR。** #957「chore(harness): let work-finishers publish, check screens and run merge-round」が開いています。`.claude/issue-workflow.md` に「動作確認」と「仕上げ」の節を足す PR で、この計画の Step 4・5 と同じファイルを触ります。このリポジトリの hook と merge の手順にも、仕上げ役のための直しを足しています。
- **main の checkout の状態。** `.claude/settings.local.json` に未コミットの変更があり、`docs/reports/` に追跡されていない報告書が 2 本あります。この計画は、どちらも触りません。

### 前提になる claude-dotfiles の側の作業

- **待っている PR。** claude-dotfiles の PR #106「feat(harness): move merge-round, worktree policy, issue dispatch and the project hooks into the core」と、PR #107「feat(check-project): check projects against the core rules and add the entry template」です。2026-11-14 の時点で、どちらも承認待ちです。Step 5 だけが、#106 が main に入っていることを前提にします。ほかの Step は、待たずに進められます。検査のスクリプトは #107 にあるので、main に入るまでは、claude-dotfiles の作業用の worktree にあるものを使います。
- **#106 を入れる前に、Step 4 を入れます。** #106 が main に入ると、共通の側に、このリポジトリと同じ名前のスキル（`merge-round` など 3 本）ができます。同じ名前のときにどちらが読まれるかは、この PC では確かめていません（未確認）。共通の側が読まれる場合、設定に `mergeMethod` が無いと、merge の手順が終了コード 12 で止まります。Step 4 で値を先に入れておけば、どちらが読まれても動きます。
- **同じ日に行うもの。** claude-dotfiles の `claude/settings.json` に 2 本の hook を登録する作業です。この計画の Step 5 と同じ日に行います。日がずれると、同じ判定が 2 回動くか、どちらも動かない時間ができます。
- **この計画が受け取ったもの。** PR #106 の計画書（`claude/docs/plans/2026-11-11-flow-parts-into-core.md`）の Worklog にある「引き渡しの一覧」です。消してよいファイル、書き換える設定、有効にする日の注意が載っています。Step 4 と 5 は、この一覧を写したものです。

### この計画で変えないもの

- ゲームのコード（`src/`・`unity-port/`）と、設計書（`.claude/docs/*_document/`・`.claude/docs/vision/` の計画書以外）。
- 設計書の持ち主の一覧 `.claude/docs/SOURCES.md` と、その検査。
- 製品のスキル 5 本（`card-creator`・`character-class-creator`・`enemy-creator`・`ui-ux-creator`・`visual-production-pipeline`）の手順の中身。語と番号の直しだけ、Step 8 で扱います。
- 申し送りの置き場 `.claude/comm/` の仕組み。
- GitHub のラベル名（`lane:<slug>` など）、ブランチ名の形（`chore/lane-<slug>` など）、worktree のフォルダ名。名前はコードとして残します。
- main の checkout にある追跡されていない報告書と、未コミットの個人の設定の中身。

### こうだいさんに決めてもらう点

どの点にも仮定を置いてあります。答えが出るまでは、仮定のとおりに書いた Step を、確認が要らないものから進められます。2026-11-14 に、最初の 3 つに答えが出ました。

**1. merge の決まりを、共通の決まりに合わせるか。**

- **何が起きているか。** 入口の文書は「merge は常に人」と書いています。こうだいさんが merge の手順（`merge-round`）を起動した回だけ、確認なしで通します。共通の決まり（`claude/docs/development-flow.md` の「merge」）は、手順と検証の条件を満たした PR を、確認なしで merge してよいとしています。全プロジェクトに効く決まり・hook・設定・権限を変える PR だけ、こうだいさんの承認を待ちます。
- **なぜ決める必要があるか。** 決めないと、Step 5 で `.claude/issue-workflow.md` の `mergePolicy` と「仕上げ」の節を書けません。開いている PR #957 を入れてよいかも決まりません。
- **選ぶと何が起きるか。**
  - 共通の決まりに合わせると、仕上げ役が、条件を満たした PR を merge まで運びます。`.claude/**` や `.github/workflows/**` を変える PR は、hook が止めて、こうだいさんの確認に回します。
  - いまのまま（常に人）にすると、仕上げ役は PR を作るところで止まります。共通の決まりと違うので、例外の一覧に 1 行足します。
- **推奨と根拠。** 共通の決まりに合わせる案を推奨します。こうだいさんが 2026-11-11 に、全プロジェクトの決まりとして決めた内容だからです。このリポジトリには PR に走る CI がありません。そのため、手元の全体確認（`.claude/issue-workflow.md` の「全体確認」）が検証の条件になります。
- **答え（2026-11-14）。** こうだいさんは、共通の決まりに合わせる案を選びました。

**2. 決定の経緯を置く場所を作るか。**

- **何が起きているか。** 入口の文書は「ADR（設計の決定を 1 件ずつ記録する文書）は作らない」と定めています。一方で、入口の文書には、進め方の決定の日付と経緯が 19 個入っています。共通の決まりは、現行の決まりの文書に日付と経緯を書かず、経緯は決定の記録か計画書に置くと定めています。
- **なぜ決める必要があるか。** 決めないと、Step 6 で入口の文書から外した経緯の移し先がありません。
- **選ぶと何が起きるか。**
  - `.claude/decisions/` を作ると、進め方の決定を 1 件 1 ファイルで置けます。名前の付け方は claude-dotfiles の `claude/docs/decisions/` に合わせます。ゲームの設計の決定は、これまでどおり設計書の本文に入れます。
  - 作らずに履歴（`.claude/history/`）へ移すと、ファイルは増えませんが、入口の文書から指せる動かない場所がありません。
  - 入口の文書に残すと、検査の対象ではないのでエラーは増えませんが、346 行のまま読み込まれ続けます。
- **推奨と根拠。** `.claude/decisions/` を作る案を推奨します。共通の標準の置き場と同じ名前なので、検査が通り、ほかのプロジェクトと探し方がそろうためです。「ADR は作らない」の文は、「ゲームの設計の決定は設計書に入れる。進め方の決定だけ、決定の記録に置く」に書き換えます。
- **答え（2026-11-14）。** こうだいさんは、`.claude/decisions/` を作る案を選びました。

**3. 指す先の無いエージェント 3 本をどうするか。**

- **何が起きているか。** `.claude/agents/` の 3 本（バランスの監査・戦闘ロジックの検証・状態の不変条件の検査）は、Mac のフォルダを指す symlink です。この PC には中身がありません。
- **なぜ決める必要があるか。** 決めないと、Step 3 が、Mac から中身を取ってくる作業なのか、消す作業なのかが決まりません。
- **選ぶと何が起きるか。**
  - Mac から中身を写してリポジトリに置くと、3 本がこの PC でも動きます。Mac の電源を入れて、ファイルを取り出す作業が、こうだいさんに要ります。
  - 3 本を消すと、いまの動き（動いていない）と変わりません。入口の文書の言及も消します。必要になったら、書き直します。
- **推奨と根拠。** 消す案を仮定に置きます。この PC では数か月動いていないのに、困った記録が見つからないためです。Mac に中身が残っているかは確かめていません（未確認）。残っていて使いたい場合は、写す案に変えます。
- **答え（2026-11-14）。** こうだいさんは、3 本を消す案を選びました。

**4. 語と番号 222 件を、どこまで直すか。**

- **何が起きているか。** 現行の決まりの文書に、言い換えの表の語が 154 件、記号と番号の組が 68 件あります。語の多くは、担当の区分を指す語と、元になる文書を指す語です。番号の多くは、`visual-production-pipeline` スキルが工程に付けた記号です。
- **なぜ決める必要があるか。** 決めないと、Step 8 の量が決まりません。全部を言い換えると、スキルとラベルの説明が大きく変わります。
- **選ぶと何が起きるか。**
  - 文章の語は言い換え、ラベル名やブランチ名のようなコードはそのまま残す案では、文章だけが変わります。工程の記号は、名前に置き換えます。
  - 例外の一覧に載せて直さない案では、作業はありませんが、こうだいさんが読む文書に、内輪の語が残ります。
- **推奨と根拠。** 文章を言い換える案を推奨します。語の決まりは、こうだいさんが一度読めば分かる文書にするためのものだからです。検査がインラインコードの中の語を数えるかは、確かめていません（未確認）。数える場合は、検査の側を直す依頼を claude-dotfiles に戻します。

**5. 使われていない部品を残すか。**

- **何が起きているか。** ループのスキル 4 本（`loop-implement`・`loop-postmortem`・`loop-triage`・`loop-verify`）は、30 日間の起動が 0 回です。判断待ちの置き場 `.claude/comm/decisions/` も、30 日間使われていません（回答のファイルは空です）。
- **なぜ決める必要があるか。** 決めないと、入口の文書のスキルの表と、Step 7 の置き場の直しが確定しません。
- **選ぶと何が起きるか。**
  - 消すと、スキルの表が 4 行減り、置き場のエラーが消えます。
  - 残すと、何も変わりません。置き場は、例外の一覧に 1 行足します。
- **推奨と根拠。** 判断を AskUserQuestion（選択式の質問）で聞く決まりが共通の側にあるので、判断待ちの置き場は消す案を推奨します。ループの 4 本は、claude-dotfiles の側の仕分け（未決）の答えに従います。

## 検討した代替案

| 案 | 採否 | 却下理由 | 復活条件 |
| --- | --- | --- | --- |
| 重なるものを消し、値は設定に残す | ✓ | — | — |
| 共通の道具をこのリポジトリへ写して配る | ✗ | 写しが古くなり、いまの食い違いが続くため | 共通の決まりを読めない実行環境で、このリポジトリを動かすとき |
| 1 本の PR で全部直す | ✗ | hook の切り替えと文書の直しが混ざり、戻しにくいため | なし |
| 検査のエラーを、全部例外の一覧に載せる | ✗ | 直せるものまで隠れるため | なし |
| 入口の文書を、ひな形から書き直す | ✗ | アーキテクチャの節など、このリポジトリだけの事実を失うおそれがあるため | 節を移し終えて、残りが 100 行を切ったとき |

## Scope (Touchable Paths)

- 触ってよい: `.claude/CLAUDE.md`、`.claude/issue-workflow.md`、`.claude/settings.json`（hook の登録の 7 本だけ）、`.claude/skills/`（同じ名前の 3 本と、語と番号の直し）、`.claude/scripts/merge-round.mjs`、`.claude/scripts/hooks-lib/`、`.claude/hooks/`、`.claude/agents/`、`.claude/MEMORY.md`、`.claude/HISTORY.md`、`TODO.md`、`.claude/feature_plans/`、`.claude/docs/vision/plans/`（置き場の整理だけ）、`.claude/archive/`、`.claude/decisions/`（新設）、`.claude/comm/decisions/`、`.claude/comm/README.md`、`.claude/docs/INDEX.md`、`.gitignore`、`README.md`
- 触らない: `src/`、`unity-port/`、`.claude/docs/*_document/`、`.claude/docs/SOURCES.md`、`.claude/settings.json` の `permissions`、`.claude/settings.local.json` の中身、`.claude/memory/` と `.claude/history/`（進捗の記録は task-tracker が書きます）、`docs/reports/` の報告書、GitHub のラベルとブランチの保護、claude-dotfiles のファイル（直しが要るときは、依頼を戻します）

## Steps

1 つの Step を 1 本の PR にします。作業は、入口の文書の決まりどおり、main の checkout ではなく worktree で行います。`.claude/` を変える PR は、共通の hook が承認の要る種類として扱うので、merge はメインのチャットが行います。

1. [x] Step 1（こうだいさんの確認が要る）: 始める条件を確かめます。検査の基準（エラー 237 件、警告 3 件）を取りました。
   - claude-dotfiles の PR #106 と #107 の状態を、`gh pr view` の出力で確かめます。main に入っていなくても、Step 2〜4 と 6〜9 は進められます。
   - `node <claude-dotfiles>/check-project.mjs <このリポジトリ> --json` を実行し、件数を Worklog に書きます。これを、あとの Step の基準にします。
   - 決めてもらう点のうち、答えが出ていない 2 つ（語と番号をどこまで直すか、使われていない部品）は、Step 7 と 8 に着く前に聞きます。
   - PR #957 は、merge の決まりを共通の決まりに合わせる答えに沿って、Step 5 に取り込みます。
2. [x] Step 2: 個人の設定の追跡をやめます。PR #965「chore(harness): stop tracking the personal Claude Code settings file」で、2026-11-14 に merge しました。
   - `git rm --cached .claude/settings.local.json` を実行し、`.gitignore` に 1 行足します。手元のファイルは消えません。
   - ほかの worktree では、この PR を取り込んだときに、ファイルが作業ツリーから消えます。必要な値は、取り込む前に控えます。PR の本文に、この注意を書きます。
3. [x] Step 3: `.claude/agents/` の symlink 3 本を片付けます（決めてもらう点「指す先の無いエージェント 3 本をどうするか」の答えに従います）。PR #966「chore(harness): remove three agent links that point outside the repository」で、2026-11-14 に merge しました。
   - 3 本を消し、`git grep -n "card-battle-"` で拾った言及を直します。
4. [x] Step 4: `.claude/issue-workflow.md` に、共通の道具が読む値を足します。PR #967「chore(harness): add the settings the shared merge-round and issue tools read」で、2026-11-14 に merge しました。
   - `## 設定値` に足す項目は、`mergeMethod`（`merge`）、`ciCommand` と `ciWorkflow`（どちらも `null`）、`harnessOnlyPaths`、`mechanicalFiles`、`areaWorkspaces`、`issueCreation`（`main-only`）です。値は「引き渡しの一覧」の案を使います。
   - この Step では、値を足すだけにします。いまの `issue-workflow` は、名前を挙げた項目だけを設定から読むので、増えた項目は読まれず、動きは変わりません。このリポジトリの merge の手順と hook は、自分の定数を使い続けます。
   - `mainCheckout` と `worktreeRoot` を相対パスに変える直し、`mergePolicy` の書き換え、「動作確認」と「仕上げ」の節は、Step 5 で行います。相対パスを絶対パスに直して渡す部品と、共通の merge の手順は、claude-dotfiles の PR #106 が main に入るまで使えないためです。先に書くと、`issue-workflow` が、まだ無い道具を呼びます。
5. [ ] Step 5（こうだいさんの確認が要る）: 共通の道具と同じ名前のものと、hook の写しを消します。claude-dotfiles の PR #106 が main に入ったあとで、claude-dotfiles が 2 本の hook を登録する日と、同じ日に入れます。
   - `.claude/settings.json` の hook の登録 7 本を消します。
   - 入口の文書の hook の表と、スキルの表の `merge-round`・`issue-dispatch`・`worktree-policy` の行を外します。Step 6 では、hook とスキルがまだ登録・配置されたままなので、これらの行を残しました。hook の表の `merge-gate.mjs` の行にある merge のコマンドの書き写し（警告 1 件）は、この Step で消えます。
   - `.claude/issue-workflow.md` の `mainCheckout` と `worktreeRoot` を、絶対パスから相対パス（`.` と `../workspaces/original-card-battle`）に変えます。個人の PC のパスを、設定から外すためです。
   - `mergePolicy` を、共通の決まりに合わせて書き換えます（こうだいさんの 2026-11-14 の答え）。「動作確認」と「仕上げ」の節を足します。「動作確認」は、開いている PR #957 の文をもとにします。「仕上げ」には merge のコマンドの形と承認の印を書きません。共通の `merge-round.mjs check` がコマンドを表示し、承認の印は、claude-dotfiles の PR #106 が main に入ったあとの共通の側には無いためです。#957 は、この Step の PR に取り込んだあとで閉じます。#957 がこのリポジトリの hook とスキルに足した直しは、消すファイルへの直しなので取り込みません。
   - 消すファイルは、`.claude/hooks/` の 7 本と `lib/hook-context.mjs` とそのテスト、`.claude/scripts/hooks-lib/` の 4 本、`.claude/scripts/merge-round.mjs`、`.claude/skills/merge-round/`、`.claude/skills/issue-dispatch/`、`.claude/skills/worktree-policy/` です。
   - 共通の `worktree-policy` が範囲の重なりを確かめる道具（`scope-overlap.mjs`）は、担当ごとの作業場所の対応表（設定の `workspaceMapPath`）が無いと止まります。このリポジトリには対応表が無いので、作るか、その道具を使わないと決めるかを、消す前に決めます。共通の側がその道具を必ず使うかは未確認です。
   - 共通の merge の手順は、PR が指す全部の Issue のラベルに、`areaWorkspaces` を上の行から順に当てます。このリポジトリのいまの手順は、Issue ごとに `lane:` を先に当てます。1 本の PR が複数の Issue を指し、ラベルの種類が違うときだけ、選ばれる担当が変わります。
   - 消す前に、共通の側に無い中身を移します。担当の区分の一覧・読み取り専用の区分・待機ブランチ・続きの PR を積む決まりは、入口の文書の「並行作業」の節に残します。`pdca:auto` を付ける判断・申し送りを集める手順・凍結した領域は、`.claude/comm/README.md` に移します。衝突に C# が混じるときに `dotnet test` を足す決まりは、`.claude/issue-workflow.md` の「全体確認」に書きます。
   - 入れたあとに、main の checkout で `gh issue create` と `gh pr merge` を、止まるはずの形で 1 回ずつ試し、hook が止めることを確かめます。止まらなければ、この PR を revert します。
6. [ ] Step 6: 入口の文書から、経緯と、共通の決まりの書き写しを外します。PR を作成済みで、merge 待ちです（#969「docs(harness): move decision history out of the entry doc into decision records」）。
   - 日付と「いつ誰が決めたか」の 19 個を、`.claude/decisions/`（新設）へ移します。入口の文書には、今の決まりと理由だけを残し、経緯が要る所に決定の記録を 1 つ指します。設計書の版の日付（`concept-v3.md`・`battle_core_v4.md` v4.3・`world-v1.md` v5）は、設計書の冒頭の Status が持っているので、入口の文書から消すだけにしました。
   - 「ADR は作らない」の文を、「ゲームの設計の決定は設計書に入れる。進め方の決定だけ、決定の記録に置く」に書き換えます。
   - 計画書の置き場と merge のコマンドの書き写し（エラー 2 件、警告 3 件）を、共通のスキルの名前を指す文に変えます。hook の表の中の 1 件（警告）は、開いている PR #957 と同じ行なので、Step 5 で表ごと外します。
   - 禁止コマンドの節と Git の節のうち、共通の決まりと同じ内容の行を消します。このリポジトリだけの決まり（main の checkout は `main` だけ、ブランチ名の形、未マージの作業をタグで残してから消す手順、`git branch -D` を止める `deny`）は残します。
   - 「README の Development History に 1 行足す」決まりを外します（Step 9 から移しました）。README にその節が無く、完了の記録は履歴のファイルが持っているためです。
   - hook の表と、スキルの表の `merge-round`・`issue-dispatch`・`worktree-policy` の行は、この Step では残します。hook 7 本とスキル 3 本がまだ登録・配置されたままで、消すのは Step 5 だからです。
7. [ ] Step 7: 文書の置き場を、標準に合わせます。PR を作成済みで、merge 待ちです（#970「docs(harness): align document locations with the shared standard」。#969 の上に積んでいます）。PR #969 の上に積みました。`.claude/comm/decisions/` は、決めてもらう点「使われていない部品を残すか」の答えが出ていないので、この PR では残しました。答えが出たら、別の PR で扱います。
   - `.claude/feature_plans/` の 4 本を、`.claude/docs/vision/plans/` の同じ名前のファイルと比べます。中身が違う 3 本は、新しいほうを残します。そのうえで、フォルダを消します。
   - `.claude/docs/vision/plans/` の日付の無い 8 本は、実装が終わっているかをコードで確かめ、終わったものを `.claude/archive/` へ移します。残すものには、Status の欄を足します。
   - `.claude/MEMORY.md`・`.claude/HISTORY.md`・`TODO.md` を消し、指しているファイル（現行の文書 3 本ほか）を直します。過去の記録（`.claude/HISTORY-archive.md`・`.claude/archive/`・日付つきの資料）の中の言及は直しません。
   - `.claude/comm/decisions/` を、決めてもらう点「使われていない部品」の答えに従って扱います。
   - `.claude/docs/INDEX.md` の案内を、新しい置き場に合わせます。
8. [ ] Step 8（こうだいさんの確認が要る）: 語と番号を直します（決めてもらう点「語と番号をどこまで直すか」の答えに従います）。
   - 検査の指摘を、言い換えるもの・コードとして残すもの・例外にするもの、の 3 つに分けた一覧を作り、こうだいさんに見せてから直します。
   - 直す順は、入口の文書、`.claude/issue-workflow.md`、スキルです。スキルは 1 本ずつ PR にします。
   - 例外にするものは、claude-dotfiles の例外の一覧（`claude/docs/exceptions.md`）に足す行の案を作り、claude-dotfiles の側へ渡します。このリポジトリからは書きません。
9. [ ] Step 9: `README.md` を書き直します。PR を作成済みで、merge 待ちです（#968「docs(readme): rewrite the README to match the React and Unity layout」）。
   - Python 版の説明を消し、いまの構成（React と Vite の版、Unity への移植）、起動のコマンド、文書の入口への案内を、60 行以内で書きます。
   - 入口の文書の「README の Development History に 1 行足す」決まりを外す作業は、Step 6 へ移しました。
10. [ ] Step 10（確かめる役）: 検査を取り直し、結果を報告します。
    - `check-project.mjs` のエラーが、例外の一覧に載せたものを除いて 0 件であることを確かめます。
    - `npm run build`・`npm run lint`・`npm run test:run` が通ることを確かめます。文書と設定だけの変更でも、hook の切り替えのあとなので 1 回回します。
    - 検査が誤って指摘したものと、進め方で詰まった点を、claude-dotfiles の計画書の Worklog に書きます。ほかのプロジェクトを合わせるときに使います。
    - この計画書を `.claude/archive/` へ移します。

## Files

大きさの印は、S が 30 行未満、M が 30〜100 行、L が 100 行以上の変更です。

| File | Operation | Notes |
| --- | --- | --- |
| `.gitignore` | edit | 個人の設定を 1 行足します。S |
| `.claude/settings.local.json` | 追跡をやめる | ファイルは手元に残します |
| `.claude/agents/` の 3 本 | delete | こうだいさんの答えに従います |
| `.claude/issue-workflow.md` | edit | 設定の項目と「仕上げ」の節を足します。M |
| `.claude/settings.json` | edit | hook の登録 7 本を消します。M |
| `.claude/hooks/`（7 本、`lib/`、`__tests__/`） | delete | 共通の 2 本に置き換わります |
| `.claude/scripts/merge-round.mjs`、`.claude/scripts/hooks-lib/` | delete | 共通の道具に置き換わります |
| `.claude/skills/merge-round/`・`issue-dispatch/`・`worktree-policy/` | delete | 共通の側に無い中身を移してから消します |
| `.claude/comm/README.md` | edit | Issue にする判断と申し送りの手順を受け取ります。M |
| `.claude/CLAUDE.md` | edit | 経緯 19 個と書き写しを外し、語を直します。L |
| `.claude/decisions/` | create | 進め方の決定の記録を置きます |
| `.claude/feature_plans/` | delete | 4 本を比べてから消します |
| `.claude/docs/vision/plans/` の日付の無い 8 本 | edit / rename | Status を足すか、archive へ移します |
| `.claude/MEMORY.md`、`.claude/HISTORY.md`、`TODO.md` | delete | 案内だけのファイルです |
| `.claude/comm/decisions/` | delete | 決めてもらう点「使われていない部品」の答えによります |
| `.claude/docs/INDEX.md` | edit | 置き場の案内を直します。S |
| スキル（語と番号の直し） | edit | 1 本ずつ PR にします。L |
| `README.md` | edit | 書き直します。M |

## Verification

- [ ] `node <claude-dotfiles>/check-project.mjs <このリポジトリ>` のエラーが、例外の一覧に載せたものを除いて 0 件である。
- [ ] `git ls-files -s | grep ^120000` が何も出さない（外を指す symlink が無い）。
- [ ] `git ls-files | grep settings.local` が何も出さない。
- [ ] `ls .claude/feature_plans` が「無い」と返し、計画書の置き場が `.claude/docs/vision/plans/` の 1 か所である。
- [ ] `grep -v "decisions/2026-" .claude/CLAUDE.md | grep -c "2026-"` が 0 を返す（決定の記録のファイル名に入る日付は数えません。共通の決まりの文書も、決定の記録をファイル名で指しています）。
- [ ] `.claude/settings.json` に、`.claude/hooks/` を指す行が無い。
- [ ] うまくいかない場合の確認: main の checkout で、merge の手順の回の外の `gh pr merge <開いている PR の番号>` を打つと、共通の hook が止める。担当の worktree で `gh issue create` を打つと、同じく止まる。
- [ ] `node ~/.claude/skills/merge-round/scripts/merge-round.mjs status` が、設定の不足を表示せずに終わる。
- [ ] `npm run build`・`npm run lint`・`npm run test:run` が通る。
- [ ] セッションを新しく始めて、SessionStart の hook が、進捗の一覧（`.claude/memory/INDEX.md`）を作り直す。

## Worklog

### 2026-11-14

- **Step 2〜4 の結果。** PR #965・#966・#967 が main に入りました。検査のエラーは 237 件から 233 件に減りました。警告は 3 件のままです。減った 4 件は、外を指す symlink の 3 件と、追跡された個人の設定の 1 件です。
- **Step 6 でやったこと。** 入口の文書の日付 19 個を外し、16 個の経緯を `.claude/decisions/` の 13 本の決定の記録へ移しました。残りの 3 個は設計書の版の日付で、設計書の冒頭の Status が持っているので、消すだけにしました。入口の文書から決定の記録を指すときは、日付つきのファイル名で指しました。共通の決まりの文書と同じ指し方です。Verification の grep は、`decisions/2026-` を含む行を数えない形に直しました。claude-dotfiles の PR #106 と #107 は、この時点でどちらも OPEN です。Step 2〜4 と 6 は、待たずに進めました。Git の節から、共通の `git-workflow` スキルと同じ内容の 5 行を消しました。禁止コマンドの節は、共通のスキルを指す文と、このリポジトリだけが `git branch -D` も止める点に書き換えました。README の Development History の決まりを外しました。入口の文書は 345 行から 342 行になりました。
- **Step 6 の検査の前後。** エラーは 233 件から 230 件に、警告は 3 件から 1 件に減りました。入口の文書への書き写しは、エラー 2 件・警告 3 件から、警告 1 件になりました。残った 1 件は hook の表の `merge-gate.mjs` の行で、開いている PR #957 と同じ行なので、Step 5 で表ごと外します。内輪の語は 154 件から 153 件に減りました（消した Git の節の 1 行の分）。ほかの種類の件数は変わっていません。

### 2026-11-15

- **Step 7 の計画書の比べた結果。** `.claude/feature_plans/` の 4 本は、どれも `.claude/docs/vision/plans/` の側が新しい中身でした。`pixijs_phase3_asset_pipeline.md` は中身が同じでした。残りの 3 本は、2026-11-02 の注記（#731）と、`pixijs_phase4_polish.md` の計画書へのリンクの直しが、`vision/plans/` の側にだけありました。`feature_plans/` の側にしか無い中身は無かったので、フォルダを消しました。
- **日付の無い 8 本の判断。** 2 本が終わっていたので、archive に入れました。`pixijs_phase1_foundation.md` は、archive にあった同じ計画の古い写し（`.claude/archive/pixijs_phase1_foundation_2026-02-22.md`）に上書きしました。差は 2026-11-02 の注記だけです。コードの段階の計画（`.claude/archive/2026-05-17-pixijs-phase1-code-level.md`）は COMPLETED で、`src/ui/pixi/` に基盤があります。`quest_system.md` は、c7bfaf63 でデイリーとウィークリーのクエストが入っていて、archive に同じ中身の写しがあったので、`vision/plans/` の側を消しました。残りの 6 本（`dark_market`・`npc_conversation`・`title_system`・PixiJS の演出・素材・仕上げの 3 本）は、終わっていません。Web 版 `src/` が旧ループのまま凍結しているので、冒頭に「ON HOLD」の Status と理由を足しました。ファイル名は変えていません。
- **案内だけのファイル 3 本。** `.claude/MEMORY.md`・`.claude/HISTORY.md`・`TODO.md` を消しました。hook・スクリプト・`package.json`・設定は、3 本を読んでいませんでした。共通の task-tracker は、`.claude/memory/` の有無で動き方を決めるので、3 本が無くても変わりません。現行の文書の言及は、入口の文書・`session-loader`・`visual-production-pipeline` の 4 か所を直しました。過去の記録の言及は残しました。
- **索引。** `.claude/docs/INDEX.md` と入口の文書の参照の表で、計画書の置き場の説明を直し、`.claude/archive/` と `.claude/decisions/` を足しました。消えていた戦闘 v4 の計画書へのリンクは、今の計画書と archive の写しを指すように直しました。
- **Step 7 の検査の前後。** エラーは 230 件から 226 件に減りました。警告は 1 件のままです。減った 4 件は、計画書の置き場の 1 件と、案内だけの進捗のファイルの 3 件です。決定の記録の置き場の 1 件（`.claude/comm/decisions/`）は残っています。ほかの種類の件数は変わっていません。
