# 試遊版の Unity 版で、戦闘の動きを JS より遅くする

決めた日は 2026-11-02 で、こうだいさんの依頼です。
元の文があった場所は、`.claude/CLAUDE.md` の「Game Loop Flow」の「試遊版の Unity 版」の項目です。

## 何を決めたか

戦闘の型・守り・移動の動きを、Unity では試遊版の JS より 0.2〜0.4 s 遅くします。長さは `View/Trial/Battle/Flow/BattlePacing.cs` だけが持ちます。

## なぜか

理由は、入口の文書に書かれていませんでした。JS と変えた点の詳しい記録は、`unity-port/TRIAL-PORT.md` にあります。

## やめた案

入口の文書に、やめた案の記録はありませんでした。
