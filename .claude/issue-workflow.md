# issue-workflow のプロジェクト設定

グローバルの `issue-workflow` スキルが、このリポジトリで Issue を選んで実装・確認・PR まで回すときに読む設定です。汎用の手順（空きメモリの確認・worktree の作成・確認の直列化・やり直し・commit と PR の末尾の行・共通の除外規則・モデルの判定）はグローバルのスキルが持ち、ここにはこのリポジトリだけの決まりを書きます。

## 設定値

```json
{
  "repo": "sunbreak-pro/original-card-battle",
  "mainCheckout": "C:/Users/user/orca/original-card-battle",
  "worktreeRoot": "C:/Users/user/orca/workspaces/original-card-battle",
  "worktreeName": "wf-{{n}}",
  "branchPattern": "{{prefix}}/{{lane}}-{{n}}",
  "maxImplementers": 3,
  "maxIssuesPerRun": 8,
  "minFreeGb": 6,
  "baselineFailures": "allow",
  "mergeMethod": "merge",
  "ciCommand": null,
  "ciWorkflow": null,
  "harnessOnlyPaths": [".claude/**", "!.claude/docs/**"],
  "mechanicalFiles": ["**/*.md", "**/package-lock.json", "**/yarn.lock", "**/pnpm-lock.yaml", "**/packages.lock.json"],
  "areaWorkspaces": {
    "lane:*": "*",
    "area:cards": "cards",
    "area:enemy": "cards",
    "area:ui": "design",
    "area:art": "design",
    "area:battle": "battle",
    "area:unity": "battle",
    "area:dungeon": "dungeon",
    "area:docs": "audit",
    "area:tooling": "audit"
  },
  "issueCreation": "main-only",
  "mergePolicy": "merge するのはこうだいさんが chat-main で merge-round を起動した回だけです。hooks/merge-gate.mjs が回の外の gh pr merge に確認を出します。実装役と確認役は merge しません。"
}
```

`mergeMethod` から `issueCreation` までの 7 項目は、claude-dotfiles の共通の `merge-round`・`issue-dispatch` と、共通の hook が読む値です。値は、このリポジトリの `.claude/scripts/merge-round.mjs` と `.claude/hooks/` が定数として持っている値と同じにしてあります。共通の道具に切り替えるまでは、このリポジトリの道具は自分の定数を使い、この 7 項目を読みません。`ciCommand` と `ciWorkflow` が `null` なのは、PR に走る CI が無いためです。

`branchPattern` の `{{prefix}}` は、このリポジトリのブランチ名の接頭辞です（`worktree-policy` の「ブランチ切替」）。下の「選び方」で Issue ごとに決めます。

## 選び方

共通の除外規則に加えて、次の決まりを上から順に当てはめます。

**`chore/tracker-*` ブランチの PR は数えません。** tracker の PR 本文は、その日に触った Issue を並べます。open PR や着地済み PR として数えると、未着手の Issue まで外れます。

**revert の PR は `merge-round` の手順で作ります。** ブランチは `revert/pr-<元の PR>`、題は `revert: <元の題> (#<元の PR>)`、本文は `Reverts #<元の PR>. Refs #<Issue>` です。戻された元の PR は、merge 済みの revert PR の本文にある `Reverts #<元の PR>` で見分けます。open のままの revert PR と、merge せずに閉じた revert PR は数えません。番号の後ろに数字が続くものは当たりにしません（正規表現なら `#<n>(?!\d)`）。

**`type:human` の Issue は回しません。** 報告では「人手待ち」として番号を並べます。

**親 Issue は回しません。** 本文に `## 子 Issue`（または `### 子 Issue`）の節がある Issue で、`npm run issues:next -- --all` が「親 Issue」に並べるものです。実装は子 Issue の側でします。

**`prio:` の無い Issue は回しません。** 報告では「優先順位なし」として並べます。

**並べる順は `prio:` の小さい順です。** 同じなら `sev:`（blocking → important → minor）、次に番号の順にします。下書きは `npm run issues:next -- --all` で出せます。このコマンドは PR の除外と本文の読み込みをしないので、ほかの決まりは省きません。

**同じレーンに `prio:1` が残っているときは、そのレーンの `prio:3` 以下を同じ回に入れません。**

**依存は本文を読んで決めます。** このリポジトリの書き方は、`依存` 節・`Parent: #<n>`・「〜が終わらないと」の 3 つです。ラベルでは代わりになりません。たとえば #39（竜）は #36 / #37 / #38 に依存していて、無視して回すと台本のまま「戦ってみる」に着手して空振りします。

**レーンを決めます。** 決め方は下の表で、上の行から順に当てはめます。決まらない Issue は回さず、「chat-main 采配」として並べます。

| 判定 | lane |
| --- | --- |
| `lane:<slug>` ラベルがある | その slug |
| `lane:` が無く、`area:` が下の表で 1 つのレーンに決まる | そのレーン |
| `area:` が複数のレーンにまたがる / どれにも当たらない | 決まらない（chat-main 采配） |
| `area:world` で `lane:` が無い | 決まらない（世界観の正本は main が持つ） |

| area: | lane |
| --- | --- |
| `area:cards` `area:enemy` | `cards` |
| `area:ui` `area:art` | `design` |
| `area:battle` `area:unity` | `battle` |
| `area:dungeon` | `dungeon` |
| `area:docs` `area:tooling` | `audit` |

**`audit` レーンの Issue は回しません。** `audit` は読み取り専用で、終わりは PR ではなく main への申し送りか元の Issue へのコメントです。こうした Issue は chat-main が手で扱い、報告では「chat-main 采配（audit）」として並べます。

**Issue が名指しする設計書の持ち主と、決めたレーンが違えば回しません。** 持ち主は `.claude/docs/SOURCES.md` §2 が決めます（`npm run sources -- --lane <lane>` の「書く正本」）。違うレーンに書かせると、1 つのファイルに書き手が 2 人できます。こうした Issue は「chat-main 采配」に回します。

**重なりは、Issue の Scope とレーンの「書く正本」で比べます。** 同じ設計書を書く Issue は、同じ回に 2 件入れません。設計書は冒頭の版の行と末尾の改訂履歴を毎回書き換えるので、別々の worktree で書くと必ず衝突します。

選んだ Issue には `lane` と `prefix` を付けて実装役へ渡します。`prefix` は次のとおりです。

| Issue の中身 | prefix |
| --- | --- |
| Scope が Markdown の文書だけ | `docs` |
| `type:bug` | `fix` |
| `type:feature` | `feat` |
| それ以外（`type:task` のツールや設定） | `chore` |

## 実装役への指示

このリポジトリ（{{repo}}）の決まりです。

**作業は {{worktree}} の中だけでします。** Bash の呼び出しのたびに、作業ディレクトリが {{main}} に戻ります。毎回 `cd {{worktree}} && ...` か `git -C {{worktree}} ...` で始めます。

**最初に、名前とブランチを書きます。** `echo wf-{{n}} > {{worktree}}/.claude/comm/.session-name` と `echo {{branch}} > {{worktree}}/.claude/comm/.session-branch` です。あとでこの worktree を Orca で開いたときに、hook が正しい名前を読むためです。

**書いてよい設計書を確かめます。** `cd {{worktree}} && npm run sources -- --lane {{lane}}` を実行し、「書く正本」に出たものだけを書きます。表と台帳が違えば台帳が正です。`.claude/docs/vision/` `.claude/docs/Overall_document/` `.claude/docs/journal_document/` は main が持つので書きません。

**正本どうしの食い違いを見つけても、他のレーンの正本は直しません。** `.claude/comm/handoff/_TEMPLATE.md` を `{{worktree}}/.claude/comm/handoff/YYYY-MM-DD-wf-{{n}}-{{slug}}.md` に写し、`from: wf-{{n}}` と `kind: conflict` を書きます。題は「正本の食い違い: 〜」で始めます。どちらに従って進めたかを申し送りと notes に 1 行ずつ書き、作業を続けます。新しい Issue が要る課題も、同じ形の申し送りにします。

次のものは書きません。

- `.claude/memory/` と `.claude/history/`（tracker）。tracker は chat-main が別ブランチでまとめます。
- `.claude/comm/outbox/`。
- `.mcp.json`。

**画面を動かす確認はしません。** `npm run dev`・ブラウザ・Unity Editor は使わず、試運転は chat-main が merge の後にします。`npm run unity:sync` も打ちません。同期先の Unity プロジェクトは全員で 1 つなので、並行して写すと互いの変更を上書きするためです。

**`node_modules` は worktree ごとに要ります。** TypeScript か `vitest` を使う変更なら、最初に `cd {{worktree}} && npm ci` を打ちます。

**変えたものに合わせて、次の確認だけを打ちます。** コマンドはどれも {{worktree}} で打ちます。

| 変えたもの | 打つコマンド |
| --- | --- |
| `src/` の TypeScript / TSX | `npm run build`、`npm run lint`、`npm run test:run -- --maxWorkers=2 <変えたディレクトリ>` |
| `unity-port/` の C#（`.cs` `.asmdef`） | 変えたアセンブリのテスト `dotnet test unity-port/<名前>.Tests/<名前>.Tests.csproj -p:UseSharedCompilation=false --disable-build-servers`。テストの無いアセンブリ（`Journal.Script` など）は `dotnet build unity-port/<名前>/<名前>.csproj -p:UseSharedCompilation=false --disable-build-servers`。続けて `npm run unity:check -- --allow-leftovers --allow-unbuilt` |
| `scripts/*.mjs` | `npm run issues:test` |
| `.claude/scripts/*.mjs` | `node --check <ファイル>`。テストは無いので、変えた分岐を手で 1 回動かして notes に書きます |
| `.claude/hooks/` | `node --test ".claude/hooks/__tests__/*.test.mjs"` |
| Markdown | `npm run md:tables -- <変えた .md>` で表の空白を詰め、`npm run md:tables -- --check` |
| `.claude/docs/SOURCES.md` か、正本の追加・退役・持ち主の変更 | `npm run sources -- --check` |

**`dotnet` の常駐ビルドサーバーは使いません。** `-p:UseSharedCompilation=false --disable-build-servers` を必ず付け、`dotnet build-server shutdown` も打ちません。`shutdown` は PC 上のすべての常駐ビルドサーバーを止めるので、ほかの実装役・確認役・chat-main・別のセッションのビルドを落とすためです。

**`unity:check` の exit 2 は合格として扱います。** exit 2 は、Unity のビルドの記録が無く確かめられなかったという意味なので、その理由を notes に書きます。exit 1 はコンパイルの失敗なので直します。

**動きを変える前に、古い動きを固定しているテストを grep で探して直します。** テストは隣の `__tests__/` に置き、書き方は `.claude/CLAUDE.md` の「Testing」に合わせます。

**commit の題は英語の conventional commit にし、題の末尾に Issue 番号を付けます。** 例は `feat(battle): drop the rest between chained battles (#{{n}})` です。

**notes は日本語（です・ます）で書きます。** 入れるものは、置いた仮定、Scope の外で気づいたこと、書いた申し送りのファイル名、正本が食い違ったときに従った側です。chat-main に残す試運転（画面・Unity Editor での確認）があれば、それも書きます。

## 全体確認

1 件ずつ、ほかの確認と重ねずに打ちます。製品のコードは直しません。

**準備をします。** `cd {{worktree}}` で、`node_modules` が無ければ `npm ci` を打ちます。

**次のコマンドを順に打ち、それぞれの終了コードを記録します。**

1. `npm run build`
2. `npm run lint`（警告 2 件は origin/main からあるもので、exit 0 なら合格です）
3. `npm run test:run -- --maxWorkers=4`
4. `dotnet test unity-port/UnityCorePort.slnx -p:UseSharedCompilation=false --disable-build-servers`（`dotnet build-server shutdown` は打ちません。理由は実装役と同じです）
5. `npm run sources -- --check`
6. `npm run md:tables -- --check`
7. `npm run issues:test`
8. ブランチが `unity-port/` の `.cs` か `.asmdef` を変えたときだけ、`npm run unity:check -- --allow-leftovers --allow-unbuilt`
9. ブランチが `.claude/hooks/` を変えたときだけ、`node --test ".claude/hooks/__tests__/*.test.mjs"`

8 を足すのは、Unity の NUnit 3.5 と netstandard2.1 にだけ無い API が、`dotnet test` では見つからないためです。

**書き手を確かめます。** `git diff --name-only origin/main...HEAD` に出たファイルを、`npm run sources` の台帳と突き合わせます。台帳は 1 行に 1 つの正本を出し、括弧の中の先頭が持ち主です。持ち主が {{lane}} 以外の正本を変えていれば失敗です。パスが一致するものと、フォルダの正本の下にあるものを数えます。

`npm run sources -- --lane {{lane}}` の「読む正本」は、そのレーンが読む行しか出しません。そのため、この確認には台帳の全体を使います。

`.claude/memory/` か `.claude/history/` の下を変えていても失敗です。

`.claude/docs/SOURCES.md` と `.claude/docs/known-issues/` は、台帳の行に無いので上の突き合わせには引っかかりません。Issue が台帳の行の変更や既知の課題の記録を求めているときだけ、変えてよいものとします。Issue が求めていないのに変えていれば失敗です。

**合格の条件は、上のコマンドがすべて exit 0 で、書き手の確認に引っかかるものが無いことです。** 例外は `unity:check` の exit 2（Unity のビルドの記録が無く確かめられない）で、合格に数えます。そのときは「Unity での確認は chat-main に残す」と理由を記録します。

**origin/main でも同じく落ちる失敗は、PR を止めません。** 設定値の `baselineFailures` を `allow` にしているので、落ちたコマンドをすべて origin/main の使い捨ての worktree でも打ち、同じテスト名で落ちたものだけなら PR を開きます。本文には「Failing on origin/main too」の節を足します。2026-11-08 の試験で、main の `MasteryProfilesTests` の 1 件のために 3 本の PR がすべて止まったためです。

この PC には dotnet SDK 10.0.302 が入っています（2026-11-08 に `dotnet --version` で確認）。`dotnet` が見つからない環境では、`dotnet test` を失敗に数え、`unity-port/README.md` の導入手順を報告に書きます。C# を変えていないブランチでも同じです。origin/main の C# がこのリポジトリの検証の土台だからです。

## PR の決まり

**push は資格情報の指定を付けて打ちます。** コマンドは `cd {{worktree}} && git -c credential.helper= -c "credential.helper=!gh auth git-credential" push -u origin {{branch}}` です。既定の資格情報マネージャーが止まることがあるためです。

**Issue との結び付けは、Issue の Gate と Definition of Done から決めます。** `gh issue view {{n}} -R {{repo}}` で読み、試運転・実機・Unity Editor・画面での確認が要るなら `Refs #{{n}}`、要らなければ `Closes #{{n}}` にします（`.claude/CLAUDE.md` の「1 Issue = 1 ブランチ = 1 PR」）。どちらにしたかの理由を、本文に 1 行で書きます。

本文には次のものを書きます。

- 変更の要約。
- 全体確認のコマンドと終了コードの表。
- 正本が食い違ったときにどちらに従ったか。実装役の notes と `{{worktree}}/.claude/comm/handoff/` の申し送りから写します。
- 申し送りがあれば「申し送り `<ファイル名>` を main に回した」の 1 行。

**本文のファイルは worktree の外に書きます。** 置き場所は OS の一時フォルダ（Git Bash の `/tmp/pr-body-{{n}}.md` など）です。worktree の中に残すと、追跡されないファイルが残って `git worktree remove` が止まるためです。

**PR は `gh pr create -R {{repo}} --base main --head {{branch}} --title "<commit の題>" --body-file /tmp/pr-body-{{n}}.md` で開きます。** 題は英語です。

## 実行のあと

chat-main が次の順に進めます。

1. **回の前に、全 worktree の申し送りを集めて Issue にしておきます。** 集め方は `.claude/comm/README.md` の「main がすること」です。集める前に回すと、まだ Issue になっていない課題がどの回にも載りません。
2. **回の後にも、`wf-*` を含む全 worktree の申し送りを集めます。** 1 件ずつ `issue-dispatch` で Issue にするか既存の Issue にコメントし、処理したファイルを消します。
3. **回の結果をこうだいさんに報告します。** 並べるのは、開いた PR、失敗した Issue とその理由、回さなかった Issue の内訳（chat-main 采配・audit・人手待ち・優先順位なし・親 Issue と、共通の除外の理由）です。
4. **PR を開いた `wf-*` の worktree は、`merge-round` の `fixes` より前に必ず消します。** `handoff/` が空になったのを確かめてから、`git -C C:/Users/user/orca/original-card-battle worktree remove <パス>` で消します。ブランチは origin に残ります。
5. **失敗した `wf-*` は、直すか捨てるかを決めるまで残します。** 手元のブランチが残っているあいだ、その Issue は次の回で「着手済み（ブランチあり）」として外れます。
6. **merge はこうだいさんが `merge-round` を起動したときに進めます。** `Refs` にした PR の Issue は、残った確認を chat-main が終えてから閉じます。
7. **C# の変更が merge されたら、chat-main が main で `npm run unity:sync` を打ち、Unity Editor での手触りを確かめます。** 試運転はメインだけの決まりです。
8. **回の記録は chat-main の tracker にまとめます。** ブランチは `chore/tracker-main-YYYYMMDD` で、1 commit にします。
9. **merge 済みのブランチと残った worktree は、`finished-work` スキルでまとめて片付けます。**

4 を省けない理由は、`merge-round` が `workspaces/` の下の worktree をすべてレーンとして数えるからです。`wf-<n>` が残っていると、`fixes` はだれもいない worktree 宛ての `/goal` を出します。そのブランチは「レーンが開いている」扱いになり、chat-main が一時 worktree で直す自動の直しからも外れます。

5 で捨てると決めたときは、worktree を消したあと、未 merge のブランチを `git branch -D` で消す必要があります。`git branch -D` は settings で deny しているので、こうだいさんが手で打ちます。
