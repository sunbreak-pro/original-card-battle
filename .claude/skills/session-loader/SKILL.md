---
name: session-loader
description: セッション開始時に original-card-battle のコンテキストを読み込む。Use at the start of a new session, after /clear, or when you need to reload project context. Triggers include session start, context load, project understanding, warm up, セッション開始, コンテキスト読み込み.
---

「session-loaderを起動します」と表示する。

# Session Loader — original-card-battle

セッション開始時に、`.claude/` の標準構造とこのプロジェクト固有のコンテキストを読み込みます。

**前のセッションの続きを引き継ぐ場合は `session-successor` が先です。** あちらは引き継ぎ書の前提を実測で確かめ直す手順で、こちらは素の起動時の読み込みです。

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

- 過去の知見は `gh issue list --state closed --search <keyword>` と `.claude/docs/known-issues/INDEX.md` の grep の両輪
- 判断待ちがあれば `.claude/comm/decisions/` と `ANSWERS.md`

## Step 4: 進行中タスクの関連ファイル

`memory/chat-<self>.md` の「進行中」が指す対象ファイルと計画書を読みます。

## Step 5: このプロジェクト固有の追加読み込み（タスクの性質に応じて）

| タスクの性質               | 読むもの                                                                               |
| -------------------------- | -------------------------------------------------------------------------------------- |
| 設計判断・ゲーム全体の構想 | `.claude/docs/vision/concept-v3.md`（正本、2026-09-12「生と継承」）                    |
| 戦闘のルール・数値         | `.claude/docs/battle_document/battle_core_v4.md`                                       |
| カード                     | `.claude/docs/card_document/swordsman_cards_v4.md`                                     |
| 敵                         | `.claude/docs/enemy_document/enemy_roster_v4.md`                                       |
| UI / 演出                  | `.claude/docs/battle_document/battle_ui_ux_v2.md`                                      |
| 絵の制作                   | `visual-production-pipeline` スキルと `.claude/docs/art_document/`                     |
| Unity 側の実装             | `unity-port/BattleCore/`（戦闘コアの正）と `unity-port/unity-project-kit/Assets/View/` |

設計書は `docs/*_document/` が正本で、実装との差分は設計書側か実装側へ寄せて解消します（`design-research` スキル）。

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
