---
name: worktree-policy
description: 本リポの multi-chat worktree 運用規約の正本。レーンの一覧と担当範囲、worktree の新規作成・ブランチ切替・main の取り込み・初回 push・マージ済み判定・Windows での削除・試運転の置き場所を扱う。worktree を作る / ブランチを切り替える / main を取り込む / merge 済みか確認する / worktree を消す ときに読む。Triggers include "worktree", "ワークツリー", "レーン", "ブランチ切替", "main を取り込む", "マージ済み", "session-branch", "worktree 削除".
---

# Multi-chat Worktree Policy（**「1 レーン = 1 worktree = 1 チャット、ブランチは Issue ごとに切替」**）

> `.claude/CLAUDE.md` の「Development Workflows」§worktree の本体です。CLAUDE.md には禁止事項の要約だけを残し、理由・手順はここが正本です。life-editor の同名スキルを本リポ向けに移植しました（2026-09-20）。

## 大前提

- **メイン（`C:\Users\user\orca\original-card-battle`）は chat-main 専有・`main` のみ**。メインで `git checkout <feature>` はしません。feature 作業は worktree から行います
- **1 レーン = 1 worktree = 1 チャット。ブランチは Issue ごとに切り替えます**。1 つの worktree が複数 Issue を順に担当するので、Issue ごとにブランチを切り直します。**worktree に 1 ブランチを固定し続けない** — PR merge 後も同じブランチを使い回すと履歴が絡みます
- **試運転はメインだけ**（2026-09-20 こうだいさん決定）。`npm run dev`・実ブラウザ検証・Unity Editor での手触り確認はメインで行います。各レーンは `npm run build` / `npm run lint` / `npm run test:run` / `dotnet test` の静的検証までです。複数 worktree で localhost を重ねるとポートがずれて「どの画面がどの変更か」の確認が壊れます

## レーン一覧

| slug      | 担当                                                                     | 宛先ラベル                                              | 主に触るパス                                                                                                                          |
| --------- | ------------------------------------------------------------------------ | ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| `cards`   | カード設計・デッキ・習熟・敵ロースター                                   | `lane:cards`（既定 `area:cards` `area:enemy`）          | `src/constants/data/cards/`, `src/domain/cards/`, `docs/*_document/` のカード章                                                       |
| `design`  | 世界観と見た目。世界設定・物語・用語と、UI / UX 設計・演出・立ち絵・素材 | `lane:design`（既定 `area:world` `area:ui` `area:art`） | `docs/vision/`, `docs/Overall_document/`, `docs/journal_document/`, `src/ui/`, `unity-port/**/View/`, `docs/art_document/`, `briefs/` |
| `battle`  | 戦闘プログラム（C# の BattleCore が正）                                  | `lane:battle`（既定 `area:battle` `area:unity`）        | `unity-port/BattleCore/`, `unity-port/BattleCore.Tests/`, `src/domain/battles/`                                                       |
| `dungeon` | 探索プログラム。刻限 / 瘴気 / ノード                                     | `lane:dungeon`（既定 `area:dungeon`）                   | `src/domain/dungeon/`, 探索側の C# と設計書                                                                                           |
| `audit`   | 監査。設計書と実装の整合、既知課題の棚卸し                               | `lane:audit`（既定 `area:docs` `area:tooling`）         | **書き込みなし**（下記）                                                                                                              |

**`audit` は読み取り専用です**（2026-09-20 こうだいさん決定）。整合監査の結果は Issue として起票し、修正は担当レーンへ回します。自分でコードを直しません。例外は自分の tracker（`memory/` `history/`）と `comm/outbox/` だけです。

**`design` は世界観の正本も持ちます**（2026-09-20 こうだいさん決定）。世界の設定・物語・用語・固有名詞・階層の雰囲気を決め、`docs/vision/` と `docs/Overall_document/` と `docs/journal_document/`（手記の文章）に落とします。見た目の設計と同じレーンに置くのは、絵と世界の話が同じ判断から出るためです。

**`cards` との境目はファイルの持ち主で切ります。** 敵の数値とロースター（`docs/enemy_document/`）は `cards` が書きます。敵の由来や呼び名の文章も、置き場がそのファイルなら書き手は `cards` です。`design` は用語と設定の正本側（`docs/vision/`）を書き、`cards` がそれを参照します。逆向きに書きに行きません。

**設計書の持ち主は `.claude/docs/SOURCES.md` §2 が正本です。** 上の表の「主に触るパス」は目安で、設計書 1 本ごとの書き手は台帳が決めます。`npm run sources -- --lane <slug>` が、自分の書く正本と読む正本を出します。表と台帳が食い違えば台帳が正です（2026-09-21 時点で `battle_ui_ux_v2.md` と `View/Depiction/` は縦切りの間だけ `battle` が持ちます）。

**正本どうしの食い違いを見つけても、他レーンの正本は直しません。** 題を「正本の食い違い: 〜」で始めた Issue にし、どちらに従って進めたかを Issue と PR 本文に書いて先へ進みます（台帳 §5）。

**one writer per artifact**: 同じファイルを 2 レーンに触らせません。担当が重なる Issue は片方を chat-main の采配へ落とします。

## 置き場所（リポジトリの外）

**worktree はリポジトリの外、`C:\Users\user\orca\workspaces\original-card-battle\<slug>\` に置きます**。リポジトリと同階層の `workspaces/` で、life-editor と同じ並びです。

理由は Orca ADE が `.gitignore` で無視されたパスの worktree を一覧から除外するためです。リポジトリ内の `.claude/worktrees/` に置くと Orca から見えなくなります。`.gitignore` の `.claude/worktrees/` 行は旧式作成の保険として残します。

**パスは必ず絶対パスで書きます**。`git worktree add workspaces/original-card-battle/<slug>` のように相対パスで打つと cwd 基準で解決され、リポジトリ内に worktree ができます。worktree の中で同じ相対パスを打つとさらにネストします。作成直後に `git worktree list` のフルパスを目で確かめます。

## 新規作成は 5 ステップ 1 セット

```bash
git worktree add C:/Users/user/orca/workspaces/original-card-battle/<slug> -b chore/lane-<slug> origin/main   # 絶対パスで
cd C:/Users/user/orca/workspaces/original-card-battle/<slug>
echo <slug> > .claude/comm/.session-name        # chat- 接頭辞は付けない
echo chore/lane-<slug> > .claude/comm/.session-branch
npm install                                      # node_modules は worktree 間で共有できない
```

省略禁止です。`.session-name` が無いと `task-tracker` が per-chat モードで止まり、`.session-branch` が無いと `session-start-check.sh` が無音でスキップします。

`chore/lane-<slug>` は**待機ブランチ**で、ここに commit は積みません。Issue に着手するたびに下の手順で切り直します。

## ブランチ切替は 2 ステップ 1 セット

```bash
git checkout -b <prefix>/<slug>-<issue> origin/main
echo <prefix>/<slug>-<issue> > .claude/comm/.session-branch   # 省略禁止
```

- **接頭辞は本リポの規約どおり** `feat/` `fix/` `docs/` `chore/` + kebab-case。末尾に Issue 番号を付けます（例: `feat/battle-36`, `docs/cards-38`）。snake_case は使いません
- **`.session-branch` は「今作業中のブランチ名」を都度更新します**。宣言と実態がズレると `session-start-check.sh` が規約違反と誤判定します
- **初回 push は `git push -u origin <branch>` と明示します**。`origin/main` から切ると upstream が `origin/main` のまま残り、引数なしの `git push` が「upstream の名前がブランチ名と一致しない」で失敗します。`tail` へパイプすると本体の失敗が隠れて exit code 0 に見えるので、パイプ時は `${PIPESTATUS[0]}` を見ます

## 作業開始前に main を取り込む

セッション開始時・着手前に 2 段階です。

1. `git pull --ff-only`（自ブランチの origin 追従。履歴が割れていたら停止）
2. `git fetch origin && git merge origin/main --no-edit`（main の差分取り込み）

取り込みを飛ばすと、古い設計書を正本として読みます。worktree は設計書のコピーを 1 組ずつ持つためです（2026-09-21 の実測でレーンは最大 37 コミット遅れ）。取り込んだ後に `npm run sources -- --lane <slug>` を引きます。

feature ブランチでは (2) を `pull --ff-only` で代替できません（fast-forward が成立せず必ず失敗します）。コンフリクトは手動で解消し、判断に迷う衝突は自動解消せず停止して chat-main / こうだいさんに報告します。chat-main（`main` ブランチ）だけは `git pull --ff-only` のみで足ります。

## tracker の更新を実装ブランチに載せない

1 レーンが複数ブランチを並行させると、各ブランチが `memory/chat-<self>.md` と `history/chat-<self>.md` の同じ位置へ別々に追記するため必ず衝突します。

実装 PR では tracker を触らず、**専用ブランチ `chore/tracker-<self>-YYYYMMDD` に分けて 1 commit でまとめます**。`pre-commit-tracker-guard.sh` が同梱コミットを止めます。意図的に混ぜるときだけコマンドに `[tracker-ok]` を含めます。引き換えに「PR 単位で何をしたか」が同時に残らないので、PR 本文に要約を書きます。

実行タイミングは `session-verifier` が緑になった直後です。実装 PR の merge を待ちません。merge は常にこうだいさんの手番なので、待つとセッションの終端が人待ちで止まります。実装 PR の状態は書いた時点の実測（open / merged）で記します。

## マージ済み判定に git の差分を使わない

squash merge されたブランチは `git diff origin/main <branch>` / `git log origin/main..<branch>` / `git cherry` のいずれでも「未マージ」に見えます。内容は main にあるのにコミットと patch-id が一致しないためです。

**判定の正は `gh pr list -R sunbreak-pro/original-card-battle --json number,state,headRefName` の state です**。差分で確認したいときは `git merge-tree --write-tree origin/main <branch>` の結果ツリーを main と比べます（衝突マーカーが差分に混ざるので中身の確認まで必須です。「追加のみ・削除ゼロ」はマーカー分を疑います）。

## Windows での worktree 削除

`git worktree remove` はディレクトリ削除で `Permission denied` になることがあります（`node_modules` をプロセスが掴んでいる）。この場合 git 側の登録だけ外れてディレクトリが残るので、`workspaces/original-card-battle/` に実体だけの残骸が溜まります。残骸は手動削除します（`git worktree list` に出ないものが対象）。掴んでいるのが Orca のターミナルのときは `orca terminal list --json` で該当 handle を探し、`orca terminal close --terminal <handle>` で解放してから削除します。

## Orca ADE 利用時の例外

Orca の GUI から作った worktree は `.session-name` / `.session-branch` を書かないため hook が無音スキップします。Claude を起動する前に手で書くか、Orca 内蔵ターミナルで上の 5 ステップを踏みます。メインリポジトリは Orca から開いてもブランチを切り替えません（`main` 専有を維持）。

## 課題の配り方

chat-main が `issue-dispatch` で Issue を起票し、`lane:` ラベルで宛先を決めます。各レーンは自分宛の open Issue をタスクキューとして実行し、PR を開くまで担います（merge と close はこうだいさんの手番）。貼り付け用の `/goal` 文字列の組み立ては `issue-prompter` です。

## 既知制約

- `node_modules` と `.tsbuildinfo` は worktree 間で共有できません（1 レーンあたり約 355 MB）
- 同じブランチを 2 つの worktree で同時に checkout できません
- `.claude/memory/INDEX.md` と `.claude/history/INDEX.md` は git 非追跡の生成物です。worktree ごとに別物になります
