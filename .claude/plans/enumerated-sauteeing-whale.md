# 計画ファイル管理の整理

## 概要

`.claude/todos/`を廃止し、`.claude/plans/`に統一する。また、設計書とコードの差分統合計画をplansに保存する。

---

## 作業内容

### 1. 設計書とコードの差分統合計画を保存

**ファイル:** `.claude/plans/DESIGN_IMPLEMENTATION_PLAN.md` (新規)

Session 12-28の実装計画を保存。現在Session 12-16が完了済み。

### 2. `.claude/todos/`ディレクトリを削除

```bash
rm -rf .claude/todos
```

### 3. ドキュメントのtodos参照をplansに修正

**対象ファイル:**

| ファイル | 現在の参照 | 修正後 |
|---------|-----------|--------|
| `.claude/CLAUDE.md:179` | `.claude/todos/` | `.claude/plans/` |
| `.claude/MEMORY.md:132` | `.claude/todos/MASTER_IMPLEMENTATION_PLAN.md` | `.claude/plans/DESIGN_IMPLEMENTATION_PLAN.md` |
| `.claude/code_overview/README.md:50` | `../todos/code-analysis-sessions.md` | `../plans/code-analysis-sessions.md` |

---

## 対象ファイル

- `.claude/plans/DESIGN_IMPLEMENTATION_PLAN.md` (新規作成)
- `.claude/todos/` (削除)
- `.claude/CLAUDE.md` (修正)
- `.claude/MEMORY.md` (修正)
- `.claude/code_overview/README.md` (修正)

---

## 検証方法

```bash
# todosディレクトリが存在しないことを確認
ls .claude/todos  # should fail

# plansディレクトリに計画ファイルがあることを確認
ls .claude/plans/*.md

# grepでtodosへの参照がないことを確認
grep -r "todos" .claude/*.md .claude/**/*.md
```
