# 試遊版の Unity 版を、ゲームの基本の形にする

決めた日は 2026-10-22 です。こうだいさんの依頼で、main が Unity 版を作りました。
元の文があった場所は、`.claude/CLAUDE.md` の「Game Loop Flow」の「試遊版の Unity 版」の項目です。

## 何を決めたか

- ブラウザの試遊版（Artifact「Original Card Battle 試遊版」。元は `origin/claude/nifty-dirac-a99nv7` の `prototypes/browser-game/`）を、見た目・動き・音ごと `unity-port/unity-project-kit/Assets/View/Trial/` に写しました。
- これを、今後のゲームの基本の形にしました。
- 見た目と手触りは試遊版の JS が元で、規則は C# のコアが元です。

## なぜか

理由は、入口の文書に書かれていませんでした。写し方の決まりは `unity-port/TRIAL-PORT.md` が持ちます。

## やめた案

入口の文書に、やめた案の記録はありませんでした。
