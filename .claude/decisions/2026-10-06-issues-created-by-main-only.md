# Issue を作るのは chat-main だけにする

決めた日は 2026-10-06 で、こうだいさんの決定です。Issue は #367 です。
元の文があった場所は、`.claude/CLAUDE.md` の「課題追跡は GitHub Issues が正」の節の、最初の項目です。

## 何を決めたか

- Issue を作るのは、chat-main だけにしました。
- ほかのチャットは `gh issue create` を打ちません。課題を申し送りとして、`.claude/comm/handoff/` に 1 件 1 ファイルで書いて先へ進みます。
- main が、全部の worktree の申し送りを集めます。重複を確かめて `issue-dispatch` で Issue にし、処理したファイルを消します。

## なぜか

life-editor の決まり（「起票は chat-main 一元」）を、このリポジトリへ移したものです。それより詳しい理由は、入口の文書に書かれていませんでした。

## やめた案

入口の文書に、やめた案の記録はありませんでした。
