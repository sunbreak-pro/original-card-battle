# 戦闘描写の置き場と、C# と Unity の素材の持ち主を分ける

決めた日は 2026-09-20 です。入口の文書は、決めた人を書いていませんでした。
元の文があった場所は、`.claude/CLAUDE.md` の「Game Loop Flow」の「戦闘描写」の項目です。項目の題に、この日付が付いていました。

## 何を決めたか

日付が項目のどの文に掛かるかは、入口の文書に書かれていませんでした。項目の最初の文の中身は、次のとおりです。

- 戦闘描写の画面は、`unity-port/unity-project-kit/Assets/View/Depiction/` に置きます。`Script/` は純 C# の台本と型で、View は名前空間 `Depiction.View` です。
- C# はこのリポジトリが元で、`npm run unity:sync` で Unity リポへ写します。
- プレハブ・シーン・`.meta` は、Unity リポが元です。

項目のそれより後ろの文には、#589・#571・#572・#99 の Issue 番号が付いています。#571 と #572 は、2026-10-18 の決定です（`2026-10-18-view-does-not-compute.md`）。

## なぜか

理由は、入口の文書に書かれていませんでした。同じ日付の計画書が `.claude/docs/vision/plans/2026-09-20-battle-scene-production.md` にあります。

## やめた案

入口の文書に、やめた案の記録はありませんでした。
