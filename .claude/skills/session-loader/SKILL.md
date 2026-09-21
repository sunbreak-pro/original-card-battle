---
name: session-loader
description: セッション開始時に original-card-battle のコンテキストを読み込む。Use at the start of a new session, after /clear, or when you need to reload project context. Triggers include session start, context load, project understanding, warm up, セッション開始, コンテキスト読み込み.
---

「session-loaderを起動します」と表示する。

# Session Loader — original-card-battle

セッション開始時に、`.claude/` の標準構造とこのプロジェクト固有のコンテキストを読み込みます。

**前のセッションの続きを引き継ぐ場合は `session-successor` が先です。** あちらは引き継ぎ書の前提を実測で確かめ直す手順で、こちらは素の起動時の読み込みです。

## Step 0: main を取り込む

設計書は worktree ごとにコピーを持つので、取り込む前に読むと古い正本を読みます。メインは `git pull --ff-only`、レーンは `git fetch origin && git merge origin/main --no-edit`（`worktree-policy`）。

## Step 1: タスクの状態

- **per-chat モード**: `.claude/memory/INDEX.md`（全チャット集約ビュー）を Read。SSOT は各 `.claude/memory/chat-*.md`
- `INDEX.md` は SessionStart hook と task-tracker が再生成する git 非追跡の派生ビュー。鮮度に不安があれば `.claude/memory/chat-*.md` を個別に Read
- 自分のチャット名は `.claude/comm/.session-name`。未宣言なら `echo main > .claude/comm/.session-name` を促す
- `.claude/MEMORY.md` / `.claude/HISTORY.md` は移転前のポインタ。読まない

## Step 2: 規約

`.claude/CLAUDE.md`（auto-load 済み前提で確認のみ）。とくに「Development Workflows」節と凍結範囲。

## Step 3: 動いている課題

- **プロダクトの課題の正は GitHub Issues**:

  ```bash
  gh issue list -R sunbreak-pro/original-card-battle --state open --limit 30
  ```

- **次にやることは `npm run issues:next`**（`prio:1`〜`4` → `sev:` → 番号の順。`--lane <slug>` でレーンに絞る）
- 過去の知見は `gh issue list --state closed --search <keyword>` と `.claude/docs/known-issues/INDEX.md` の grep の両輪
- 判断待ちがあれば `.claude/comm/decisions/` と `ANSWERS.md`

## Step 4: 進行中タスクの関連ファイル

`memory/chat-<self>.md` の「進行中」が指す対象ファイルと計画書を読みます。

## Step 5: このプロジェクト固有の追加読み込み（タスクの性質に応じて）

**どの主題はどのファイルが正本かは `.claude/docs/SOURCES.md` が持ちます。** ここに表を写しません。

```bash
npm run sources -- --lane <自分の slug>   # 書く正本と読む正本。メインは npm run sources で全件
```

状態の欄に但し書き（「§N 未反映」「§N が本文より優先」）がある行は、その但し書きごと読みます。絵の制作は `visual-production-pipeline` スキルです。

設計書と実装の差分は、設計書側か実装側へ寄せて解消します（`design-research` スキル）。正本どうしの食い違いは直さず Issue にします（台帳 §5）。

## Step 6: 要約表示

```
**現在地**
- 進行中: {memory の「進行中」を 1〜2 行}
- open Issue: {件数}件（着手可 {n} / 人手待ち {n} / 依存待ち {n}）
- ブランチ: {git branch --show-current}（.session-branch と一致するか）
- 判断待ち: {decisions の未回答件数}
```

## 注意事項

- CLAUDE.md は auto-load 済み前提。Step 1〜4 は確認と差分読み込みに留め、重複を避ける
- **戦闘コアの正は `unity-port/BattleCore/`（C#）**。TS の `src/ui/battle-lab/core/` は凍結済みなので、実装の現状を掴むときに読まない
- **Web 版（`src/`）は旧ループのまま**（キャンプ 5 施設 / ライフ制 / AP）。新ループは Unity 側で実装中。`src/` を読んで「いまの仕様」と誤解しない
- 凍結範囲（アーマー / 装備・Gold・ソウル経済 / ショップ・鍛冶屋・サンクチュアリ）の文書を現行仕様として読まない
- ADR は作らない。設計原則は `docs/vision/core.md` に集約
