# 錆槍の竜兵（polearm_warped）— 仕様カード

- 作成日: 2026-09-20 / 更新: 2026-09-23（#146。寸法・生成時間・指示文を直し、題材を #128 の竜人へ寄せた）/ 2026-10-03（#85。向きを左向きに変え、待機の絵を Unity リポへ入れた）/ 工程: C0（`visual-production-pipeline character polearm_warped`）
- 正本: `.claude/docs/enemy_document/enemy_roster_v4.md`（錆槍の竜兵の節）/ `.claude/docs/vision/world-v1.md`（竜の系譜）/ `.claude/docs/art_document/style-guide.md`（原本の大きさと指示文）/ `.claude/docs/art_document/asset-intake.md`（書き出しと取り込み）/ `unity-port/unity-project-kit/Assets/View/Depiction/Editor/DepictionPrefabBuilder.cs`（枠と配置）
- 手順書: `docs/reports/2026-09-19-krita-ai-setup-guide.html` 4 章
- 内部 ID は `polearm_warped` のままです。ロースターが ID を替えないと決めているので、ファイル名と asset id もこの ID を使います（`enemy_roster_v4.md` §1.8）。

## 1. この敵が何者か

階層 1〜2 の通常敵です。竜の系譜では眷属の段にいる **竜人** で、二足で立って武器を持ちます（`world-v1.md` §2.1）。ロースターの記述は「土色の鱗を持ち、槍の穂先が瘴気に侵されて赤く錆びている」です。名前の「錆」は、瘴気の帯び方がいちばん浅い段階を指します（`world-v1.md` §7.1）。

柄の長さで相手を寄せつけず、踏み込まれたら石突きで突き放す押し引きを最初に教える役です。HP 60 / 最大スタミナ 10 / 回復 2 で、状態（出血・鈍足など）は使いません。位置を持たない通常敵です。

絵では、長い柄の武器を持っていることと、その柄の長さで相手を遠ざける構えでいることを伝えます。位置を持たないので、立ち位置では描き分けません。性格は構えと武器の長さで見せます。眷属は系譜の最下段なので、竜らしさは鱗・角・尻尾・爪に留め、翼は描きません。ロースターの記述に翼が無いためです。

## 2. 画面での大きさと基準点

| 項目             | 値                                                                                                        | 出典                                                                                                               |
| ---------------- | --------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| 表示枠           | 210 × 450（Canvas の参照単位で、比は 0.467）。実画素は 1080p で 450、1440p で 600、4K で 900 です         | `DepictionPrefabBuilder.cs:218`、`:471-473`（`ScaleWithScreenSize`、参照 1920 × 1080、`matchWidthOrHeight = 0.5`） |
| 絵の収まり       | `preserveAspect = true` です。640 × 1536（比 0.417）の絵は高さいっぱいに収まり、幅は約 188 単位になります | `DepictionPrefabBuilder.cs:222`                                                                                    |
| 基準点           | 足元の中央（pivot は (0.5, 0)）                                                                           | `DepictionPrefabBuilder.cs:218`                                                                                    |
| 左右の向き       | 左向きで描きます。Unity は反転せず、画面右（x = +400）から左のプレイヤーをそのまま向きます                | `DepictionPrefabBuilder.cs:497`。`:501` の反転は #288 で外します                                                   |
| 頭上の空き       | 予兆の札（270 × 68）は、枠の上端から約 44 単位上に出ます。枠の中は上端まで描いてかまいません              | `DepictionPrefabBuilder.cs:505`（札の中心が y = 468、足元が y = −60）                                              |
| 数字と文字の位置 | 数字は足元から 310 単位（胸の 270 + 40）、ラベルは 440 単位（頭の 470 − 30）に出ます                      | `DepictionPrefabBuilder.cs:223-224`、`DepictionPlayer.cs:218-219`                                                  |
| 原本の大きさ     | **640 × 1536** の文書です。書き出しも 640 × 1536 のままにして、縮小しません                               | `style-guide.md` §2、`asset-intake.md` §1.2                                                                        |

**利き手**: 武器が画面左（プレイヤー側）へ来るように描きます。原本と画面で向きは同じです。

**穂先の長さの制約（要判断）**: 原本の幅は 640 px で、高さの 0.42 倍しかありません。長柄を身長の 1.4 倍（身長 1.7 m に 2.4 m）で描くと、縦に立てても原本の高さを超え、横に構えると幅を大きく超えます。推奨は、柄を 70〜75°（ほぼ縦）に立てて穂先を頭の斜め後ろに置き、柄の下端を体の後ろで切って全体を 640 × 1536 に収めることです。穂先を枠の上端まで伸ばしても、予兆の札とは重なりません。穂先の見せ方は、C1 の影絵で形を見てから決めます。

## 3. 行動と、絵が要る場面

`enemy_roster_v4.md` の錆槍の竜兵の節にある 4 行動です。差分は「待機 / 行動 / 被弾 / 崩れ / 倒れ」で、体勢（構え）の差分は作りません（skill 命名節、2026-09-14 決定）。

| 行動 id        | 名前             | 絵で見せること                                                | asset_id                              |
| -------------- | ---------------- | ------------------------------------------------------------- | ------------------------------------- |
| `sweep`        | 薙ぎ払い         | 柄を寝かせて水平に振り抜く。主力（威力 8、相手が遠間なら +3） | `chr.polearm_warped.act.sweep`        |
| `shove`        | 石突きの押し込み | 柄の尻で突き放す。相手の位置を反転する（威力 5）              | `chr.polearm_warped.act.shove`        |
| `reach_thrust` | 穂先の突き       | 腰だめから前へ伸ばす。削り（威力 4）                          | `chr.polearm_warped.act.reach_thrust` |
| `guard_up`     | 柄で受ける       | 柄を斜めに立てて受ける（Guard 3）                             | `chr.polearm_warped.act.guard_up`     |

旧版にあった `reposition`（間合い取り直し）はロースターから消えたので外しました。

**技名はロースターのままにしています。** `world-v1.md` §7.2 は「薙ぎ払い」と「石突き」を和の武術語として置き換えると決めていますが、ロースターはまだ置き換えていません。食い違いは #172 に起票しました。行動 id は変わらないので、asset id とファイル名には影響しません。

待機は `chr.polearm_warped.idle.stand`、被弾・崩れ・倒れは `chr.polearm_warped.react.*` です。ファイル名は `Assets/Art/Characters/polearm_warped/chr_polearm_warped_<category>_<label>.png` です（`asset-intake.md` §4）。

**C3 で作るのは待機 1 枚だけ**です。行動と被弾の差分は C6（人の加筆）以降です。

**絵柄**: 線・塗り・色・光と、瘴気の帯び方（錆）・竜の系譜の段（眷属・竜人）の描き分けは、`art_document/style-guide.md` の第 2 部（§12〜§19）に従います。影絵は同 §17 の関門を通してから C3 へ進みます。

## 4. 生成の指示文（段階 1・案出し用）

`style-guide.md` §4.3 の竜人の例文を、この敵に合わせて鱗の色と持ち物だけ替えたものです。スタイルは `card-battle Animagine`、文書は **640 × 1536**、生成回数 4 を 2〜4 回まわして 8〜16 枚出します。

```
1other, solo, safe, full body, standing, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, polearm held upright, rust, shoulder armor, {torn clothes|torn cape|vambraces}, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

替えた語は 3 つです。例文の `black scales` と `blue theme` を、土色の鱗に合わせて `brown scales`（Danbooru 163 件）と `brown theme`（4,915 件）にしました。`brown scales` は件数が少なく効きが弱いので、`brown theme` で支えます。穂先の赤錆には `rust`（1,279 件）を足しました。件数は 2026-09-23 に Danbooru の公開 API で引いた値です。

負の指示文は `style-guide.md` §4.4 の「全被写体で使う文字列」をそのまま使います。竜人は人型なので、竜・亜竜・小竜用の追加の語は足しません。

**`facing right` は書きません。** Danbooru に 0 件で、条件付けになりません。右向きで出た候補は、採用前に Krita で水平反転して左向きに直します（`style-guide.md` §4.2）。

ワイルドカード（`{a|b|c}`）は生成ごとに 1 つが選ばれます。良かった組み合わせの固定のしかたは `style-guide.md` §4.5 にあります。

禁止事項: 既存の作品名・作者名を入れない。流血を描かない。購入素材や他作品の画像を参照に入れない。`style-guide.md` §5 の使わない語（`soldier` `corrupted soldier` `japanese dark fantasy` など）を入れない。

## 5. 時間の予算

1 枚の生成は約 21 秒と見ます。640 × 1536 は 983,040 画素で、実測した 1024 × 1024（1,048,576 画素、28 ステップで 21.09 秒。`%APPDATA%\krita\ai_diffusion\logs\server.log` 2026-09-20 13:32）とほぼ同じ画素数だからです。16 枚で約 5.5 分です。C6 の加筆時間は 1 体目で実測し、残りの体の計画の基準にします。

## 6. 進捗

| 工程 | 状態   | 備考                                                                                                                                                                             |
| ---- | ------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| C0   | 済み   | このファイル。2026-10-03 に向きを左向きへ変えた（#85）                                                                                                                           |
| C1   | 一部   | SVG の影絵は作っていない。待機の絵を黒で塗り、高さ 450 px で見て、穂先は上端から 4 px、下の刃は左端から 6 px 内に収まると確かめた（2026-10-03）                                  |
| C2   | 済み   | 指示文は §4。台帳 `Docs/art/asset-ledger.csv` を Unity リポに置き、1 行目を足した（RPG-by-card PR #8）                                                                           |
| C3   | 済み   | こうだいさんが Krita で生成した（`竜の槍兵2.kra`、候補 6 枚）                                                                                                                    |
| C4   | 済み   | こうだいさんが一番上の候補（種 2367636945）を採用した（2026-10-03、#85 のコメント）                                                                                              |
| C5   | 一部   | 背景の灰色は抜いた。足元の影は残した。類似確認（台帳の `similarity_check`）はまだ                                                                                                |
| C6   | 未着手 | 加筆はせずに待機の絵を先に入れた。差分（行動・被弾・倒れ）は待機の絵が画面に入ってから                                                                                           |
| C7   | 未着手 | —                                                                                                                                                                                |
| C8   | 一部   | 待機の絵（640 × 1536、左向き、背景透明）を Unity リポに置いた（RPG-by-card PR #8）。`.meta` と Preset の登録（`asset-intake.md` §5）、画面への差し込みと反転の撤去（#288）はまだ |

**人に渡したもの**: Unity リポの PR #8（待機の絵・台帳・生成条件の写し）。**次に人がすること**: Unity で Preset を登録してから PR #8 を取り込み、#288 で画面に差し込む。

**前提**: 絵柄の規約（`style-guide.md`）と取り込み規約（`asset-intake.md`）はあります。Unity リポの台帳 `Docs/art/asset-ledger.csv` は RPG-by-card PR #8 で `.claude/docs/art_document/asset-ledger.template.csv` から置きました。列の定義は `asset-intake.md` §7 です。
