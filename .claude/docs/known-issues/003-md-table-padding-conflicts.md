# 003 — 並行 PR が docs で必ずコンフリクトする

- **Status**: Mitigated（2026-10-04）
- **カテゴリ**: Tooling / Git
- **影響ファイル**: `.claude/docs/SOURCES.md`、各正本の冒頭の Status と末尾の改訂履歴

## 症状

複数のレーンが PR を同時に出すと、1 本をマージした後に残りの PR がほぼ必ず docs でコンフリクトした。2026-09-15〜10-04 のマージ 181 件を `git merge-tree` で合流し直すと 16 件がコンフリクトし、9/24 以降の 9 件のうち 6 件が `SOURCES.md` だった。

## Root Cause

- **表の桁揃え**: グローバルの PostToolUse hook（`~/.claude/hooks/post-edit-prettier.mjs`）が Edit / Write のたびに prettier をかけていた。prettier は Markdown の表の列幅を最長のマスに揃えるので、1 マスが伸びると表の全行（31〜61 行）が空白だけ書き換わった。この PR が先にマージされると、表に触る他の PR がすべてぶつかった。
- **台帳に版を書く決まり**: 「版を上げるコミットで台帳の行を直す」決まりで、普段の PR が `SOURCES.md` に触っていた。別のレーンが隣り合う行を 1 行ずつ直すと、git は隣接した変更を自動で合流しない。
- **同じレーンの並行 PR**: マージ待ちの間に次のブランチを `origin/main` から切るので、2 本の PR が同じ正本の版の行と改訂履歴の末尾を同じ位置で書いた（#257 と #286、#206 と #207）。

## 解決

- `.prettierrc.json` で `*.md` に `requirePragma: true` を付け、Markdown を prettier の対象から外した。`.prettierignore` は prettier の現在地から探されるため、hook がメインのチェックアウトから呼ばれると worktree のファイルに効かない。設定ファイルはファイルの場所から探されるので、どこから呼ばれても効く。
- 全 Markdown の表の空白を `npm run md:tables` で詰めた（111 ファイル、表の行だけが変わる）。
- `SOURCES.md` §2 を持ち主ごとの表に分け、状態の欄から版と経緯を外し、版を上げるだけのコミットでは台帳に触らない決まりにした。
- `worktree-policy` に「続きの PR は積む」を足した。

## 再発防止

- **`proseWrap: "never"` では直らない。** 幅の広い表は詰めた形になるが、複数行の引用ブロック（設計書の冒頭の `> **Status**` など）を 1 行に結合するので、版の行がかえってぶつかりやすくなる。
- 表の桁揃えが戻っていないかは `npm run md:tables -- --check` で確かめる。
- グローバルの hook 側の対処は claude-dotfiles の Issue #36。
