# hook が、コマンドの中の cd を読む

決めた日は 2026-11-08 です。入口の文書は、決めた人を書いていませんでした。
元の文があった場所は、`.claude/CLAUDE.md` の「hooks（`.claude/settings.json`）」の節の、表のすぐ後ろの段落です。

## 何を決めたか

3 つの hook（`pre-commit-tracker-guard.sh`・`pre-issue-create-guard.sh`・`merge-gate.mjs`）が、コマンドの中の `cd` を読みます。読む部品は `.claude/hooks/lib/hook-context.mjs` で、テストは `node --test ".claude/hooks/__tests__/*.test.mjs"` です。

## なぜか

Workflow のエージェントは、main の checkout から Bash を始め、`cd <worktree> && ...` で作業フォルダに移ります。入口の文書は、これが分かったのは、この日の `issue-workflow` の試験だったと書いていました。

## やめた案

入口の文書に、やめた案の記録はありませんでした。
