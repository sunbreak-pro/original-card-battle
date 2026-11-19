# このリポジトリだけのスキル

このフォルダには、このリポジトリでだけ使うスキルを置きます。どの作業でどれを使うかは、入口の文書（`.claude/CLAUDE.md`）の「Skills Quick Reference」の表が持ちます。ここには置き場の決まりだけを書きます。

| スキル | 何をするか |
| --- | --- |
| `card-creator` | カードを足します。データの定義と、デッキへの登録までを扱います |
| `enemy-creator` | 敵を足します。定義・行動の型・階層ごとのデータを扱います |
| `character-class-creator` | キャラクターのクラスを足します |
| `ui-ux-creator` | 画面の部品を作ります。色・文字・動き・配置の決まりを持ちます |
| `visual-production-pipeline` | 敵とプレイヤーの外見、戦闘の画面と演出を、調べ直しから報告書まで一続きで作ります |
| `session-loader` | セッションの最初に、このリポジトリの状態を読み込みます |
| `session-successor` | 前のセッションの続きを、引き継ぎ書の前提を確かめ直してから再開します |

## 置き場の決まり

- **共通のスキルと同じ名前のスキルは置きません。** commit・PR・merge・Issue の起票と実装・作業の記録・報告書などの手順は、claude-dotfiles の共通のスキル（`~/.claude/skills/`）が持ちます。同じ名前を置くと、共通の検査（claude-dotfiles の `check-project.mjs`）がエラーにします。
- **スキルを足すときは、`.claude/skills/<名前>/SKILL.md` を作り、入口の文書の表と上の表に 1 行ずつ足します。**
- **どの作業でも必ず通る手順は、ここに置かず、claude-dotfiles の側へ移します。**
