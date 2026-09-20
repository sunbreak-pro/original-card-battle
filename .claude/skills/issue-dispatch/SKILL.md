---
name: issue-dispatch
description: プロダクトの課題を GitHub Issue として起票し、ラベルで種別・重要度・領域を付ける。重複チェックと DoD の型を持つ。Triggers include "課題を起票して", "Issue にして", "タスクを切って", "dispatch", "起票", "issue-dispatch".
---

# issue-dispatch — GitHub Issue 駆動のタスク起票

課題の台帳 `.md` を作らず、**掲示板 1 枚 = GitHub Issues** に全部貼る方式です。進捗の台帳を別に作ると、必ず Issue と二重管理になって片方が腐ります。

## モデル（誰が何をするか）

- **正本 = GitHub Issues**（`gh -R sunbreak-pro/original-card-battle`）。`docs/vision/plans/` の計画書は大型仕様の詳細だけに使い、**作業分配・進捗追跡の台帳 .md は新規作成しない**
- **消化 = `loop-triage` → `loop-implement`**。本スキルは実装しません
- **並べ直し = `issue-prompter`**。本スキルは着手順を決めません

## ラベル（`gh label list` が正本）

| 系統      | 値                                                                     | 必須             |
| --------- | ---------------------------------------------------------------------- | ---------------- |
| `type:`   | `bug` / `feature` / `task` / `human`                                   | 必須             |
| `sev:`    | `blocking` / `important` / `minor`                                     | 任意             |
| `area:`   | `battle` `cards` `enemy` `dungeon` `ui` `art` `unity` `docs` `tooling` | 任意（複数可）   |
| `lane:`   | `cards` `design` `battle` `dungeon` `audit`                            | 任意（1 つだけ） |
| `status:` | `monitoring` / `workaround` / `frozen`                                 | 任意             |

**`lane:` は worktree レーンの宛先**です。付けなければ `issue-prompter` が `area:` から既定のレーンに振ります（対応表はそちらが正本）。`area:` が複数レーンに跨るときと、既定と違うレーンに任せたいときだけ明示します。**`lane:audit` は読み取り専用のレーン**なので、コードの修正を含む Issue には付けません。

**`type:human` は Claude が代われない作業**に付けます（実機の手触り、絵の選定、課金・認証）。`issue-prompter` はこれを `/goal` に混ぜず、こうだいさん宛の一覧に分けます。

## スコープの境界

**Issue はプロダクトの課題専用**です。判定式は「**このリポジトリのコードを直せば直るか？**」。

- **Yes** → Issue
- **No**（Claude Code の環境 / hook / ツールの挙動） → `.claude/docs/known-issues/` に `NNN-<slug>.md` を作り `INDEX.md` を更新

例外は 1 つだけです。環境起因でも**このリポの設定ファイル（`.claude/settings.json` / `hooks/` / `.gitattributes`）を直せば直る**なら Issue にします（実例 = #40）。

## 手順

### 1. 課題収集

ソース: こうだいさんの指示 / 検証レポート（`docs/reports/`）/ plans の残タスク / 整合監査。

サブエージェントの報告に含まれる file:line・件数・引用は、**起票前に必ず自分で開いて spot check** します。裏取りしていない findings の Issue 化は「矛盾の量産」になります。

### 2. 重複チェック（起票前に必ず）

```bash
gh issue list -R sunbreak-pro/original-card-battle --state open --limit 100
gh issue list -R sunbreak-pro/original-card-battle --state closed --search "<keyword>"
grep -rn "<keyword>" .claude/docs/known-issues/INDEX.md
```

既存の open があれば新規を立てずコメントを追記します。closed 済みの類似は known-issues の grep と両輪で確認し、再発なら reopen ではなく**新 Issue + 旧番号への参照**にします。

### 3. 起票（1 課題 = 1 Issue）

テンプレートは `.github/ISSUE_TEMPLATE/` の 3 種（Known Issue / Roadmap Item / Human Task）。`gh issue create` から立てるときは本文に次を入れます。

- **Summary / 現象** — 1〜3 行。再現手順があれば併記
- **いま何が起きているか** — 原因の本質を `file:line` で示す。推測なら「未確認」と書く
- **Scope** — 触ってよいパス。同じファイルを 2 件に触らせない
- **Definition of Done** — 機械検証可能な形。`npm run build` exit 0 / `dotnet test` 緑 / 実測値
- **依存** — 先に close されている必要がある Issue 番号。`issue-prompter` はここを読んで待ち行列を作る
- **Gate** — 人手の工程（実機の手触り、PR merge、絵の選定）が含まれるなら明記。Issue に書いても実行はこうだいさん

```bash
gh issue create -R sunbreak-pro/original-card-battle \
  --title "<素のタイトル>" \
  --label "type:feature,area:battle,sev:important" \
  --body-file <(cat)
```

### 4. 完了追跡

作業の区切りに open Issue を巡回し、停滞・close 漏れ・「merge 済みなのに open」を検出します。PR merge / Issue close の後は plan の Status と per-chat の `memory/` を追随させます（`task-tracker`）。

## 凍結領域（起票しない）

`.claude/CLAUDE.md` の凍結範囲は Issue にしません。アーマー（AP / 装備耐久 / 修理）、装備・Gold・ソウル経済、ショップ / 鍛冶屋 / サンクチュアリ。防御は Guard のみです。

TS 側の `src/ui/battle-lab/core/` も凍結済みで、戦闘コアの正は `unity-port/BattleCore/` です。

## Gotchas

- **stale `origin/main`**: 判断の前に `git fetch origin main`。古い ref は幻の ahead/behind を報告します
- **`.claude/memory/INDEX.md` は git に遅れます**。「merge された？」は `gh pr list --json state` が正
- **無関係な作業の同梱 merge 禁止**: 別チャットの変更が PR に相乗りしていたら、merge 前にこうだいさんへ提示します
