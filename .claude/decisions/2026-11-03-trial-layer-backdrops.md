# 試遊版の Unity 版で、階層の背景を層ごとに分ける

決めた日は 2026-11-03 で、こうだいさんの依頼です。Issue は #89 です。
元の文があった場所は、`.claude/CLAUDE.md` の「Game Loop Flow」の「試遊版の Unity 版」の項目です。

## 何を決めたか

階層の背景を、試遊版の JS と変えました。層ごとに色を分け、コードで描く遠景（`Backdrop/Script/BackdropScenery.cs`）と、星などの粒の組を足しました。値は `BackdropSpec.cs` が持ちます。

## なぜか

理由は、入口の文書に書かれていませんでした。JS と変えた点の詳しい記録は、`unity-port/TRIAL-PORT.md` にあります。

## やめた案

入口の文書に、やめた案の記録はありませんでした。
