---
name: loop-implement
description: Issue 1 件を実装し、検証を通して commit まで持っていくループ。着手する Issue が決まった時に明示起動する。PR 作成と merge はしない。
disable-model-invocation: true
---

# /loop-implement — Issue 1 件を commit まで

## 目標

指定された Issue 1 件を実装し、検証ゲートを通して commit まで持っていく。1 起動 = 1 Issue。またぐなら分割する。

## 完了条件（機械検証可能）

- `session-verifier` の Verdict が PASS（BLOCKING finding ゼロ）
- 対象 Issue の DoD をすべて満たした状態で commit 済み
- PR 本文の下書きが `.claude/comm/outbox/chat-<self>/pr-draft-<issue>.md` に出ている
- `git diff origin/main --name-only` が Issue に紐づく Scope 内に収まっている（宣言外パスの変更ゼロ）

## 予算

- 反復上限: **5 周**（1 周 = 実装 → 検証 → 修正）。超えたら**仮説を出して停止**する
- 時間上限: **90 分**。開始直後に `START_TS=$(date +%s)` を取り、各周の冒頭で経過を確認する
- **仮説には「次に試すこと」を 1 つ具体的に書く**。「もう少しで終わりそう」だけの仮説は成果として認めない（次の自分が拾えない）

## 停止条件（人間に返す）

- 反復上限・時間上限に当たった
- 宣言した Scope の外に手を入れないと進まない（広げず、計画書か Issue の更新を先に依頼する）
- 要件が二義的で、どちらに倒すかで成果物が変わる（`.claude/comm/decisions/chat-<self>.md` に A/B で書く）
- 検証の失敗が環境起因に見える（`/loop-verify` の担当。切り分けをこのループでやらない）
- **Unity Editor でしか確かめられない**と分かった（実機の手触り・プレハブ・シーン）。`type:human` の Issue を起こす依頼を outbox へ append する
- **正本どうしが食い違っている**。止まらず、他レーンの正本も直さない。「正本の食い違い: 〜」の Issue にし、どちらに従ったかを PR 本文に 1 行で書いて進む（`.claude/docs/SOURCES.md` §5）
- **凍結領域に手が伸びた**（CLAUDE.md の凍結範囲）。広げずに止める

## 使ってよい道具

- `lead-pipeline` スキル — 軽 / 中 / 重のティア判定と、どの工程を呼ぶかの采配表
- `role-engineer` エージェント — 重ティアの実装主体（起動はメインが Agent ツールで行う。再帰起動は不可）
- `session-verifier` スキル — 検証ゲートの正本。ゲートの中身をこのループに転記しない
- `git-workflow` / `git-branch-flow` スキル — commit 規約と破壊的操作のガードレール
- ドメイン別スキル — `battle-system` / `card-creator` / `enemy-creator` / `dungeon-system` / `ui-ux-creator` / `character-class-creator`

## 検証コマンド（この repo の実物）

CI はまだ無いので、`.claude/CLAUDE.md` の Development Commands が正本。

```bash
npm run build          # tsc -b + vite build
npm run lint
npm run test:run       # vitest 単発
cd unity-port && dotnet test   # BattleCore の 54 件
npm run parity:check   # TS / C# のパリティ
npm run unity:sync     # C# を Unity リポへ写す（写し漏れ 0 件を確認）
```

Unity の EditMode / PlayMode テストは Editor 経由（`unity-editor-mcp` の `run_tests`）。Editor がつながっていなければ `dotnet test` までで切り、Editor 分は未検証と明記する。

## 環境の事実（推論では埋まらないので明記する）

- **`git push origin main*` 系と force push は `deny`**、**`gh pr merge` は `ask`**（実体 = `.claude/settings.json`）。だから本ループの完了条件は commit までで、PR 作成と merge は人間の手番に残してある
- **tracker（`memory/` + `history/`）を実装コミットに載せない**。`pre-commit-tracker-guard.sh` が exit 2 で止める。記録は別コミットにし、PR 本文側に要約を書く
- **`INDEX.md` を `git add` しない**。生成物で、`pre-commit-index-guard.sh` が自動 unstage する
- **戦闘コアの正は `unity-port/BattleCore/`（C#）**。TS の `src/ui/battle-lab/core/` は凍結。両方直そうとしない
- **戦闘描写の View は計算しない**。値は全て台本から取る（`Assets/View/Depiction/`）。プレハブ / シーン / `.meta` は Unity リポが正で、C# はこのリポが正
- **`deck.ts` と `deckReducer.ts` は変更禁止**（CLAUDE.md の Immutable Code）
- **ブランチは別チャットと共用することがある**。`.claude/comm/.session-branch` を切り替えのたびに書き換え、`git add -A` を使わずパスを明示列挙する

---

- 1 起動 = 1 目標。「ついでにこれも」で Scope を広げない。広げたくなったら停止条件に当たったと見なす
- 実測した周回数・所要時間は、区切りで `history/chat-<self>.md` に 1 行足す
