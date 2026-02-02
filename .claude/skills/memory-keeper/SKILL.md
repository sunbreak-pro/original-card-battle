---
name: memory-keeper
description: ユーザーが「覚えておいて」「記録して」「メモして」「remember this」「note this」などと発言した際に、情報を自動的にファイルに記録する。設計判断、バグ情報、開発Tips、TODOなどを永続化したい場合に使用。
---

# Memory Keeper Skill

> **発動確認**: このskillを使用する際は、最初に「📝 memory-keeper skill を使用します」と明示してください。

## Trigger

ユーザーが以下のような発言をした場合にこのスキルを使用する:
- 「覚えておいて」「これ覚えて」
- 「記録して」「記録しておいて」
- 「メモして」「メモしておいて」
- 「remember this」「note this」
- その他、情報の永続的な記録を求める発言

## Workflow

1. **「📝 memory-keeper skill を使用します」と宣言する**
2. ユーザーの発言から記録すべき内容を抽出する
3. 内容を以下のカテゴリに分類する
4. `.claude/memories/` ディレクトリが存在しなければ作成する
5. 該当カテゴリのファイルが存在しなければヘッダー付きで新規作成、存在すれば末尾に追記する
6. 記録完了を日本語でユーザーに報告する（カテゴリとファイルパスを含める）

## Categories

記録先は `.claude/memories/` ディレクトリ配下のカテゴリ別ファイル:

| File | Category | Examples |
|------|----------|----------|
| `decisions.md` | 設計判断・方針決定 | 「この実装方針で行く」「AよりBを採用」 |
| `bugs.md` | バグ情報・回避策 | 「このバグがある」「この回避策を使う」 |
| `tips.md` | 開発Tips・ノウハウ | 「このAPIはこう使う」「この書き方が良い」 |
| `todo.md` | 後でやること・保留事項 | 「後で直す」「次回対応」 |
| `misc.md` | 上記に分類できないもの | その他全般 |

## Record Format

各ファイルはMarkdown形式。新規作成時はヘッダーを付与し、追記時は末尾に箇条書きで追加する。

### New file template
```markdown
# {Category Title}

- [{date}] {content}
```

### Append format
```markdown
- [{date}] {content}
```

日付フォーマット: `YYYY-MM-DD`

## Example

ユーザー: 「CardEffectの型にはhitCountを必ず含めること、覚えておいて」

→ 分類: `tips.md`（開発ノウハウ）

`.claude/memories/tips.md` に追記:
```markdown
- [2026-02-01] CardEffectの型にはhitCountを必ず含めること
```

→ 応答: 「memory-keeper skill を使用します」
→ 応答: 「tips.md に記録しました: CardEffectの型にはhitCountを必ず含めること」

## Notes

- 1つの発言に複数のカテゴリにまたがる内容がある場合、それぞれのファイルに分けて記録する
- 既存の記録と重複する内容でも追記する（日付で区別できるため）
- カテゴリの判断が曖昧な場合は `misc.md` に記録する