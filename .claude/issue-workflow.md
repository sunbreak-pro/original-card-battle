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
  "mergePolicy": "merge するのはこうだいさんが chat-main で merge-round を起動した回だけです。hooks/merge-gate.mjs が回の外の gh pr merge に確認を出します。実装役と確認役は merge しません。"
}
```

`branchPattern` の `{{prefix}}` は、このリポジトリのブランチ名の接頭辞です（`worktree-policy` の「ブランチ切替」）。下の「選び方」で Issue ごとに決めます。

## 選び方

共通の除外規則を通った Issue に、上から順に当てはめます。

1. **`chore/tracker-*` ブランチの PR は数えません。** tracker の PR 本文は「その日に触った Issue」を並べるため、open PR や着地済み PR として数えると、未着手の Issue まで外れます。
2. **`revert/pr-*` の PR が戻した PR は、着地済みに数えません。** revert の PR は題が `revert: <元の題> (#<n>)`、本文が `Reverts #<n>` です。戻した PR の merge commit は origin/main に残るので、数えると reopen した Issue が着地済みとして外れます。
3. **`pdca:auto` の付いた Issue は回しません。** 進行役（pdca-harness）が拾う Issue で、回すと同じ Issue を 2 者が実装します。報告では「pdca 担当」として番号を並べます。
4. **`status:frozen` の Issue は回しません。** 着手の判断がこうだいさん待ちです。報告に理由つきで残します。
5. **`type:human` の Issue は回しません。** 報告では「人手待ち」として番号を並べます。
6. **`prio:` の無い Issue は回しません。** 報告では「優先順位なし」として並べます。並べる順は `prio:` の小さい順で、同じなら `sev:`（blocking → important → minor）、次に番号です。下書きは `npm run issues:next -- --all` で出せます。このコマンドは PR の除外と本文の読み込みをしないので、ほかの規則は省きません。
7. **依存は本文を読んで決めます。** `依存` 節・`Parent: #<n>`・「〜が終わらないと」のどれかが open の Issue を指していれば回しません。ラベルでは代わりになりません。たとえば #39（竜）は #36 / #37 / #38 に依存していて、無視して回すと台本のまま「戦ってみる」に着手して空振りします。報告では `#<n> ← #<依存先>` の形で並べます。
8. **レーンを決めます**（下の表）。決まらない Issue は回さず、「chat-main 采配」として並べます。
9. **`audit` レーンの Issue は回しません。** `audit` は読み取り専用で、終わりは PR ではなく main への申し送りか元の Issue へのコメントです。報告では「chat-main 采配（audit）」として並べます。
10. **Issue が名指しする設計書の持ち主と、決めたレーンが違えば回しません。** 持ち主は `.claude/docs/SOURCES.md` §2 が決めます（`npm run sources -- --lane <lane>` の「書く正本」）。違うレーンに書かせると、1 つのファイルに書き手が 2 人できます。「chat-main 采配」に回します。
11. **同じ回の中で、書くファイルが重なる Issue を 2 件選びません。** Issue の Scope と、レーンの書く正本で比べます。重なれば番号の大きいほうを次の回に回します。同じ設計書は冒頭の版の行と末尾の改訂履歴を毎回書き換えるため、別々の worktree で書くと必ず衝突します。
12. **同じレーンに `prio:1` が残っているときは、そのレーンの `prio:3` 以下を同じ回に入れません。**

レーンの決め方は、上の行から順に当てはめます。

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

選んだ Issue には `lane` と `prefix` を付けて実装役へ渡します。`prefix` は次のとおりです。

| Issue の中身 | prefix |
| --- | --- |
| Scope が Markdown の文書だけ | `docs` |
| `type:bug` | `fix` |
| `type:feature` | `feat` |
| それ以外（`type:task` のツールや設定） | `chore` |

## 実装役への指示

このリポジトリ（{{repo}}）の決まりです。手順の土台（空きメモリの確認・worktree の作成・commit の末尾の行・push しないこと）は別に渡されています。

- **作業は {{worktree}} の中だけでします。** Bash の呼び出しのたびに作業ディレクトリが {{main}} に戻るので、毎回 `cd {{worktree}} && ...` か `git -C {{worktree}} ...` で始めます。{{main}} ではブランチを切り替えず、ファイルも書きません。
- 最初に `echo wf-{{n}} > {{worktree}}/.claude/comm/.session-name` と `echo {{branch}} > {{worktree}}/.claude/comm/.session-branch` を書きます。あとでこの worktree を Orca で開いたときに、hook が正しい名前を読むためです。
- `gh issue view {{n}} -R {{repo}}` で Issue を読みます。Summary・Scope・Definition of Done・依存・Gate が仕様です。Scope の外のファイルは変えません。外で直したほうがよいものは notes に書きます。
- `cd {{worktree}} && npm run sources -- --lane {{lane}}` を実行します。書いてよい設計書は「書く正本」に出たものだけです。`.claude/docs/vision/` `.claude/docs/Overall_document/` `.claude/docs/journal_document/` は main が持つので書きません。表と台帳が違えば台帳が正です。
- **正本どうしの食い違いを見つけても、他のレーンの正本は直しません。** `.claude/comm/handoff/_TEMPLATE.md` を `{{worktree}}/.claude/comm/handoff/YYYY-MM-DD-wf-{{n}}-<slug>.md` に写し、`from: wf-{{n}}`、`kind: conflict`、題を「正本の食い違い: 〜」で始めて書きます。どちらに従って進めたかを申し送りと notes に 1 行書き、作業を続けます。
- **`gh issue create` は打ちません。** 新しい Issue が要る課題も、同じ形で `handoff/` に申し送りとして書きます。hook はここでは止めません。hook は {{main}} の `.session-name`（`main`）を読むためです。自分の Issue へのコメントは許されています。
- `.claude/memory/` と `.claude/history/`（tracker）、`.claude/comm/outbox/` と `.claude/comm/decisions/` は書きません。tracker は chat-main が別ブランチでまとめます。`.mcp.json` も触りません。
- **画面を動かす確認はしません。** `npm run dev`・ブラウザ・Unity Editor は使いません。`npm run unity:sync` も打ちません。同期先の Unity プロジェクトは全員で 1 つなので、並行して写すと互いの変更を上書きするためです。試運転は chat-main が merge の後にします。
- `node_modules` は worktree ごとに要ります。TypeScript か `vitest` を使う変更なら、最初に `cd {{worktree}} && npm ci` を打ちます。
- 変えたものに合わせて、次の確認だけを打ちます。全部を通す確認は後の段でします。

| 変えたもの | 打つコマンド（{{worktree}} で） |
| --- | --- |
| `src/` の TypeScript / TSX | `npm run build`、`npm run lint`、`npm run test:run -- --maxWorkers=2 <変えたディレクトリ>` |
| `unity-port/` の C# | 変えたアセンブリのテスト `dotnet test unity-port/<名前>.Tests/<名前>.Tests.csproj`。テストの無いアセンブリ（`Journal.Script` など）は `dotnet build unity-port/<名前>/<名前>.csproj`。終わったら `dotnet build-server shutdown` でビルド用の常駐プロセスを止めます |
| `scripts/*.mjs` | `npm run issues:test` |
| `.claude/scripts/*.mjs` `.claude/hooks/*.mjs` | `node --check <ファイル>`（テストは無いので、変えた分岐を手で 1 回動かして notes に書きます） |
| Markdown | `npm run md:tables -- <変えた .md>` で表の空白を詰め、`npm run md:tables -- --check` |
| `.claude/docs/SOURCES.md` か、正本の追加・退役・持ち主の変更 | `npm run sources -- --check` |

- 動きを変える前に、古い動きを固定しているテストを grep で探して直します。テストは隣の `__tests__/` に置き、書き方は `.claude/CLAUDE.md` の「Testing」に合わせます。
- commit の題は英語の conventional commit にし、末尾に Issue 番号を付けます。例は `feat(battle): drop the rest between chained battles (#{{n}})` です。tracker の変更を混ぜません。
- notes は日本語（です・ます）で書きます。置いた仮定、Scope の外で気づいたこと、書いた申し送りのファイル名、chat-main に残す試運転（画面・Unity Editor での確認）を入れます。

## 全体確認

1 件ずつ、ほかの確認と重ねずに打ちます。製品のコードは直しません。

1. `cd {{worktree}}` で、`node_modules` が無ければ `npm ci` を打ちます。
2. 次の順に打ち、それぞれの終了コードを記録します。途中で失敗しても止めずに最後まで打ちます。終了コードは `cmd | tail` から取らず、出力をファイルに落としてから `$?` を読みます。
   - `npm run build`
   - `npm run lint`（警告 2 件は origin/main からあるもので、exit 0 のままなら合格です）
   - `npm run test:run -- --maxWorkers=4`
   - `dotnet test unity-port/UnityCorePort.slnx`、続けて `dotnet build-server shutdown`
   - `npm run sources -- --check`
   - `npm run md:tables -- --check`
   - `npm run issues:test`
   - ブランチが `unity-port/unity-project-kit/` の `.cs` か `.asmdef` を変えたときだけ、`npm run unity:check -- --allow-leftovers --allow-unbuilt`。Unity の NUnit 3.5 と netstandard2.1 にだけ無い API は、`dotnet test` では見つからないためです。
3. 書き手の確認をします。`git diff --name-only origin/main...HEAD` に出た `.claude/docs/` のファイルが、`npm run sources -- --lane {{lane}}` の「書く正本」にすべて入っているかを見ます。入っていないファイルが 1 つでもあれば失敗です。`.claude/memory/` `.claude/history/` が混ざっていても失敗です。
4. 合格の条件は、2 のコマンドがすべて exit 0 で、3 に引っかかるものが無いことです。`unity:check` の exit 2（Unity のビルドの記録が無く確かめられない）は失敗に数えず、「Unity での確認は chat-main に残す」と記録します。失敗が origin/main でも起きるものに見えても、失敗のまま理由を書きます。直しません。

この PC には dotnet SDK 10.0.302 が入っています（2026-11-08 に `dotnet --version` で確認）。`dotnet` が見つからない環境では、`dotnet test` を失敗に数え、`unity-port/README.md` の導入手順を報告に書きます。C# を変えていないブランチでも同じです。origin/main の C# がこのリポジトリの検証の土台だからです。

## PR の決まり

- push は `cd {{worktree}} && git -c credential.helper= -c "credential.helper=!gh auth git-credential" push -u origin {{branch}}` で打ちます。既定の資格情報マネージャーが止まることがあるためです。`-u` を省くと、upstream が origin/main のまま残り、次の `git push` が失敗します。
- `gh pr create -R {{repo}} --base main --head {{branch}} --title "<commit の題>" --body-file <ファイル>` で開きます。題は英語です。
- 本文には、変更の要約、全体確認のコマンドと終了コードの表、書いた申し送りのファイル名（「申し送り `<ファイル名>` を main に回した」）、正本が食い違ったときにどちらに従ったかを書きます。
- Issue との結び付けは、このリポジトリの決まりどおり `Closes #{{n}}` にします（`.claude/CLAUDE.md` の「1 Issue = 1 ブランチ = 1 PR」）。merge するのは人なので、merge が Issue を閉じる合図になります。ただし Issue の Definition of Done に chat-main の試運転（画面・Unity Editor での確認）が残るときは、`Refs #{{n}}` にして、残った確認を本文に書きます。
- merge はしません。

## 実行のあと

chat-main が次の順に進めます。

1. 全 worktree の申し送りを集めます。`wf-*` も含みます。集め方は `.claude/comm/README.md` の「main がすること」で、1 件ずつ `issue-dispatch` で Issue にするか既存の Issue にコメントし、処理したファイルを消します。
2. 回の結果（開いた PR・失敗した Issue と理由・回さなかった Issue の内訳）をこうだいさんに報告します。回さなかった内訳は「chat-main 采配」「audit」「待ち行列」「人手待ち」「優先順位なし」「pdca 担当」「status:frozen」に分けます。
3. merge はこうだいさんが `merge-round` を起動したときに進めます。`Refs` にした PR の Issue は、残った確認を chat-main が終えてから閉じます。
4. C# の変更が merge されたら、chat-main が main で `npm run unity:sync` を打ち、Unity Editor での手触りを確かめます。試運転はメインだけの決まりです。
5. 回の記録は chat-main の tracker に、`chore/tracker-main-YYYYMMDD` のブランチで 1 commit にまとめます。
6. PR を開いた `wf-*` の worktree は、申し送りを集めた後に消してかまいません。ブランチは origin に残ります。残しておくと、`merge-round` が `wf-<n>` を 1 本のレーンとして数え、衝突の直しを `wf-<n>` 宛ての `/goal` にします。chat-main が一時 worktree で直す自動の直しも、そのブランチが開かれている間は動きません。merge 済みのものは `finished-work` スキルでまとめて片付けます。失敗した `wf-*` は、直すか捨てるかを決めるまで残します。
