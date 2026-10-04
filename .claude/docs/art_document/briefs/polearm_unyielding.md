# 不退の槍竜（polearm_unyielding）— 仕様カード

- 作成日: 2026-10-04（#287。こうだいさんが Krita で作った待機の絵を Unity リポへ入れた）/ 工程: C0（`visual-production-pipeline character polearm_unyielding`）
- 正本: `.claude/docs/enemy_document/enemy_roster_v4.md`（§3.3 不退の槍竜）/ `.claude/docs/vision/world-v1.md`（竜の系譜）/ `.claude/docs/art_document/style-guide.md`（原本の大きさ・指示文・帯び方）/ `.claude/docs/art_document/asset-intake.md`（書き出しと取り込み）/ `unity-port/unity-project-kit/Assets/View/Depiction/Editor/DepictionPrefabBuilder.cs`（枠と配置）
- 手順書: `docs/reports/2026-09-19-krita-ai-setup-guide.html` 4 章（待機の絵）/ `docs/reports/2026-10-03-enemy-motion.html`（攻撃と倒れの差分の作り方）
- 内部 ID は `polearm_unyielding` です。v4.6 で足した敵で、錆槍の竜兵の `polearm_warped` にそろえて `polearm_` で始めています（`enemy_roster_v4.md` §1.8）。ファイル名と asset id もこの ID を使います。`asset-intake.md` §4 には #128 の付け替えを待つ段落が残っています。この版はロースターに従いました（食い違いは #327）。

## 1. この敵が何者か

階層 5〜6 の精鋭です。竜の系譜では眷属の **竜人** で、槍の竜兵の中で最も古い個体です（`enemy_roster_v4.md` §1.8・§3.3）。ロースターの記述は「全身を重い甲で覆い、頭から長い棘の髪が垂れる。穂先を据えたまま一歩も退かない」です。

得意な間合いは 1〜2 で、精鋭で初めてこの間合いを持ちます。自分は一歩も退かず、相手を槍の間合いへ動かしてから突きます。隣のマス（間合い 0）の相手は柄で 2 マス突き放し、間合い 3〜5 の相手は穂で 2 マス掛け寄せます。どちらも 1 手目で、引き直した 2 手目に長穂の突き（届く 1〜3）が来ます。HP 120 / 最大スタミナ 12 / 回復 3 / 大きさ 1 / 開始の間合い 3 / 1 フェーズ 2 行動 / 予兆 2 段です。

絵では、重い甲と長い棘の髪で精鋭の格を見せ、その場に根を張って動かない構えを伝えます。眷属なので翼は描きません。

**帯び方（要判断）**: 精鋭は名前の前に帯び方の語を持たないので、出る層から当てます（`style-guide.md` §15）。階層 5〜6 は「燐」（主に出る層 4〜7）に当たり、結晶を燐光で光らせて体の塗りの 15〜30% を占めさせる段です。待機の絵は甲と鱗が土色で、結晶も発光も描いていません。生成に使った指示文が錆槍の竜兵と同じもの（`rust` と `brown theme` を含む。§4）だったためです。重い甲と棘の髪はロースターの記述に合っています。`style-guide.md` §16.2 の層 5〜7 は鱗を板のように厚くし、背骨の棘を体の外へ出す段で、甲と髪はこの表にない意匠です。燐の発光をこのまま描かずに使うか、C6 の加筆で足すかは、こうだいさんが決めます。差分は待機の絵を見本にして作るので、決めるのは差分を作る前です。

## 2. 画面での大きさと基準点

| 項目             | 値                                                                                                                                                                                             | 出典                                                                                                                      |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| 表示枠           | 210 × 450（Canvas の参照単位で、比は 0.467）。実画素は 1080p で 450、1440p で 600、4K で 900 です                                                                                              | `DepictionPrefabBuilder.cs:219`、`:473-475`（`ScaleWithScreenSize`、参照 1920 × 1080、`matchWidthOrHeight = 0.5`）        |
| 絵の収まり       | `preserveAspect = true` です。900 × 1536（比 0.586）の絵は枠より横長なので、幅 210 に合わせて縮み、高さは 358.4 になります。絵の `Image` の pivot が (0.5, 0.5) なので、足元から 45.8 浮きます | `DepictionPrefabBuilder.cs:222-223`、#302                                                                                 |
| 基準点           | 足元の中央（枠の pivot は (0.5, 0)）                                                                                                                                                           | `DepictionPrefabBuilder.cs:219`                                                                                           |
| 左右の向き       | 左向きで描きます。Unity は反転せず、画面右（x = +400）から左のプレイヤーをそのまま向きます                                                                                                     | `DepictionPrefabBuilder.cs:499`、`:503-504`、`FigureView.cs:58`                                                           |
| 頭上の空き       | 予兆の札（270 × 68）は、枠の上端から約 44 単位上に出ます。精鋭は札を 2 段積む設計で、2 段目はさらに上に出ます（2 段目は未実装）。枠の中は上端まで描いてかまいません                            | `DepictionPrefabBuilder.cs:362`、`:507`（札の中心が y = 468）、`:490`（足元が y = −60）、`battle_ui_ux_v2.md` §2.1・§3.10 |
| 数字と文字の位置 | 数字は足元から 310 単位（胸の 270 + 40）、ラベルは 440 単位（頭の 470 − 30）に出ます                                                                                                           | `DepictionPrefabBuilder.cs:224-225`、`DepictionPlayer.cs:367-368`                                                         |
| 原本の大きさ     | **900 × 1536** です。決まりの 640 × 1536 より 260 広く、書き出しも 900 × 1536 のままです。900 は 4 の倍数なので BC7 が効きます                                                                 | `style-guide.md` §2、`asset-intake.md` §1.2・§3                                                                           |

`style-guide.md` §2 と `asset-intake.md` §1.2 には、幅の例外がまだありません。2 体の幅（晶槍の竜兵の 800 と不退の槍竜の 900）を例外として書き足す作業は、design レーンで別に行います。

**利き手**: 下の刃と伸ばした爪が画面左（プレイヤー側）に来ています。槍は体の後ろで縦に立ち、穂先は右上にあります。原本と画面で向きは同じです。

**穂先の長さの判断（済み、2026-10-04）**: 描いた幅 900 のまま入れました。640 に切ると、いちばん良い位置（左端 x = 156）でも右上の穂先と左の下の刃の先が欠けるためです。いまの 210 × 450 の枠では、高さ 358.4 で足元から 45.8 浮いて出ます。枠を 263.7 × 450（450 × 900 / 1536）に広げれば、高さいっぱいに立ちます。枠を敵ごとに広げる作業は、battle レーンの #302 です。

影絵を高さ 450 px に縮めて確かめた値は次のとおりです。穂先は上端から 24 px（原本で 82 px）、下の刃は左端から 16 px（原本で 54 px）の内側にあります。右の空きは 9 px（原本で 31 px）です。同じ縮尺（450 / 1536）で影絵の高さは 426 px で、錆槍の竜兵は 446 px です。

**接地**: 絵を 59 px 下、25 px 右へ動かしました。いちばん下の爪の先が最下行（y = 1535）に届いています。爪は描かれた土の盛り上がりに食い込んでいて、盛り上がりの下 23 行は切れています。足の中心は x = 447 前後で、キャンバスの中央（450）とほぼそろっています。

## 3. 行動と、絵が要る場面

`enemy_roster_v4.md` §3.3 の 6 行動です。差分は「待機 / 行動 / 被弾 / 崩れ / 倒れ」で、体勢（構え）の差分は作りません（skill 命名節、2026-09-14 決定）。表の「届く」は届く間合いで、敵のマスから数えます。

| 行動 id       | 名前             | 絵で見せること                                                                                                            | asset_id                                 |
| ------------- | ---------------- | ------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| `set_spear`   | 穂先を据える     | 腰を落として穂先を相手へ向けて据える。構え（列 2、自分にだけ効く。相手との間合いが 1 以上のとき攻撃の威力 +3）            | `chr.polearm_unyielding.act.set_spear`   |
| `long_thrust` | 長穂の突き       | 足を動かさず、据えた槍を真っ直ぐ伸ばす。ただ 1 つの攻撃（列 2、届く 1〜3、威力 8。同じフェーズにスキルを出した後なら +3） | `chr.polearm_unyielding.act.long_thrust` |
| `haft_shove`  | 柄で突き放す     | 柄の尻で隣の相手を突き放す（列 1、届く 0、相手を 2 マス押す）                                                             | `chr.polearm_unyielding.act.haft_shove`  |
| `hook_in`     | 穂で掛け寄せる   | 遠くの相手に穂を掛けて引き寄せる（列 1、届く 3〜5、相手を 2 マス引く）                                                    | `chr.polearm_unyielding.act.hook_in`     |
| `plate_guard` | 甲で受ける       | 重い甲で受ける（列 1、自分にだけ効く、Guard 3。残 5 以上で +3）                                                           | `chr.polearm_unyielding.act.plate_guard` |
| `clank_on`    | 甲を鳴らして進む | 甲を鳴らして 1 マス前へ出る（列 1、自分にだけ効く、Guard 2、前へ 1）                                                      | `chr.polearm_unyielding.act.clank_on`    |

押す 2・引く 2・前へ 1 は、画面の側で人型をマスごと動かして見せます（`battle_ui_ux_v2.md` §5.7）。移動だけの絵は作りません。

待機は `chr.polearm_unyielding.idle.stand`、被弾・崩れ・倒れは `chr.polearm_unyielding.react.*` です。ファイル名は `Assets/Art/Characters/polearm_unyielding/chr_polearm_unyielding_<category>_<label>.png` です（`asset-intake.md` §4、`FigureMotion.cs:37-40`）。View の口は待機 / 行動 / 被弾 / 倒れの 4 つで、崩れの口はありません（`FigureMotion.cs:12-18`、食い違いは #309）。崩れの名前は口が入るときに決めます。

**差分は攻撃 1 枚と倒れ 1 枚から作ります**（`docs/reports/2026-10-03-enemy-motion.html` の 2 段目）。行動の絵は、`act_` で始まるファイルのうち名前順で最初の 1 枚が全ての行動に出ます（`FigureArtCollector.cs:72-85`、食い違いは #310。この版は View に従いました）。そこで 1 枚で 6 行動を代表させます。`act_clank_on` / `act_haft_shove` / `act_hook_in` は名前順で `act_long_thrust` より前なので、View が行動 id ごとに絵を引くようになるまで Unity に置きません。

| 順  | 差分 | asset_id                                 | ファイル名                                   | 見せること                                                       |
| --- | ---- | ---------------------------------------- | -------------------------------------------- | ---------------------------------------------------------------- |
| 1   | 攻撃 | `chr.polearm_unyielding.act.long_thrust` | `chr_polearm_unyielding_act_long_thrust.png` | 長穂の突き。足は待機の絵の位置のまま、槍を画面左へ真っ直ぐ伸ばす |
| 2   | 倒れ | `chr.polearm_unyielding.react.down`      | `chr_polearm_unyielding_react_down.png`      | 膝をつき、上体が前へ崩れ、槍が手から離れて床に落ちる             |

被弾（`react.hit`）と、突き放しと掛け寄せの差分は、手触りの確認（#79・#60）で 1 枚で足りるかを見てから足します。この 2 つはこの敵の持ち味なので、View が行動 id ごとに絵を引くようになったら先に作ります。差分は待機の絵と同じ 900 × 1536 で作り、足の位置も待機の絵にそろえます。絵が切り替わったときに足元が跳ねないようにするためです。突きの穂先が左端からはみ出しそうなら、穂先をやや下へ向けて枠の中に収めます。

**絵柄**: 線・塗り・色・光と、瘴気の帯び方（燐）・竜の系譜の段（眷属・竜人）の描き分けは、`art_document/style-guide.md` の第 2 部（§12〜§19）に従います。影絵は同 §17 の関門を通してから差分の生成へ進みます。

## 4. 生成の条件（待機の絵は作成済み）

待機の絵は、こうだいさんが Krita で生成して選んだものです。この敵のための指示文は新しく作っていません。条件は `.kra` の履歴（`ui.json`）から読み、Unity リポの `Docs/art/recipes/chr.polearm_unyielding.idle.stand.workflow.json` に写しました（RPG-by-card PR #9）。

| 項目                 | 値                                                                                                                                                                                                                                 |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 元の `.kra`          | `C:\Users\user\OneDrive\Desktop\windev\card-battle-image\上級槍竜兵２.kra`（900 × 1536、2026-10-03 19:11 保存）。原本には手を付けず、写しを読みました                                                                              |
| 使ったレイヤー       | 一番上の生成のレイヤー（`[Generated] ... (1195294282)`）です。統合画像（`mergedimage.png`）と全画素で一致しました                                                                                                                  |
| 使わなかったもの     | その下の生成のレイヤー（種 1195294283）は表示されていますが、上のレイヤーに全て隠れています。「ペイントレイヤー 1」はタイルが 0 枚で、加筆はありません。「背景」（白）は捨てました。履歴の 1 件目（種 1195294281）も使っていません |
| 加筆の有無           | 一番上のレイヤーは、履歴 3 件のうち 2 件目（種 1195294282、`result6.webp`）と、圧縮のノイズを除いて一致しました。生成のレイヤー自体にも手は入っていません                                                                          |
| モデルとスタイル     | `animagine-xl-4.0-opt`、スタイルは `anime-illustrious.json`（`card-battle Animagine` ではありません）                                                                                                                              |
| sampler / 段数 / cfg | Euler A / 28 / 5                                                                                                                                                                                                                   |
| 種                   | 1195294282                                                                                                                                                                                                                         |
| 指示文               | 錆槍の竜兵の §4 と同じ文です。ワイルドカード `{torn clothes\|torn cape\|vambraces}` は `vambraces` が選ばれました                                                                                                                  |
| 負の指示文           | 入力した文は `style-guide.md` §4.4 の「全被写体で使う文字列」と同じです                                                                                                                                                            |

差分も待機の絵と同じスタイル `anime-illustrious.json` で作ります。このスタイルは文の末尾に `masterpiece, high score, great score, absurdres` を足し、負の指示文にも自前の語を足します（`chr.polearm_unyielding.idle.stand.workflow.json` の `prompt_final` / `negative_prompt_final`）。

**差分の指示文（攻撃）**: 待機の絵の文から `standing` と `polearm held upright` を抜き、ワイルドカードを `vambraces` に固定して、動きの語を足したものです。

```
1other, solo, safe, full body, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, rust, shoulder armor, vambraces, attack, fighting stance, leaning forward, dynamic pose, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `attack`（Danbooru 5,482 件）、`fighting stance`（34,247 件）、`leaning forward`（164,943 件）、`dynamic pose`（4,093 件）です。この敵は踏み込まないので、晶槍の竜兵の文にある `lunging` は入れず、足を据えた `fighting stance` にしました。動きの作り方の HTML が挙げる `attacking` は 0 件で、`swinging polearm` はタグにありません（`swinging` は廃止）。件数は 2026-10-04 に Danbooru の公開 API で引いた値です。

**差分の指示文（倒れ）**: 同じく待機の文から `standing` `holding polearm` `polearm held upright` を抜き、倒れの語を足したものです。

```
1other, solo, safe, full body, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, polearm, rust, shoulder armor, vambraces, on one knee, head down, exhausted, weapon on floor, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `on one knee`（19,054 件）、`head down`（3,833 件）、`exhausted`（3,815 件）、`weapon on floor`（252 件）、`polearm`（75,355 件）です。HTML の `defeated` は 0 件、`dropped polearm` はタグにないので、`on one knee` と `weapon on floor` に替えました。HTML の `kneeling` の代わりには、片膝の `on one knee` を使います。`weapon on floor` は 252 件と少ないので `polearm` で支えます。

**燐の発光を足すと決めたとき**: 両方の文で `rust` を `glowing crystal`（186 件）と `crystal`（36,580 件）に替えます。`glowing crystal` は件数が少ないので `crystal` で支えます。待機の絵も同じ見た目に加筆してから差分を作ります。差分は待機の絵を Reference にするので、待機の絵が光っていないと差分の発光もそろいません。

負の指示文は待機の絵と同じです。竜人は人型なので、竜・亜竜・小竜用の追加の語は足しません。

**生成のしかた**は `docs/reports/2026-10-03-enemy-motion.html` の「2 段目の絵を Krita で作る手順」に従います。棒人間の Scribble と、待機の絵の Reference を Control layer に置きます。名前が `noob` で始まる Pose と Reference のモデルは使いません。一部を描き直す Refine は強度 80% 以下にします。

禁止事項: 既存の作品名・作者名を入れない。流血を描かない。購入素材や他作品の画像を参照に入れない。`style-guide.md` §5 の使わない語（`soldier` `corrupted soldier` `japanese dark fantasy` など）を入れない。

## 5. 時間の予算

900 × 1536 は 1,382,400 画素で、Krita の上限（1,050,000 画素、`style-guide.md` §2 の B）を超えます。超えた文書は、縮めて生成してから超解像で戻す経路を通ります。待機の絵がこの経路を通ったかは、ログで確かめていません。生成の時間も測っていません。差分の手間は 1 体につき 30〜60 分の見込みです（`docs/reports/2026-10-03-enemy-motion.html`）。

## 6. 進捗

| 工程 | 状態   | 備考                                                                                                                                                                              |
| ---- | ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| C0   | 済み   | このファイル（2026-10-04、#287）                                                                                                                                                  |
| C1   | 一部   | SVG の影絵は作っていない。待機の絵を黒で塗って高さ 450 px で見て、穂先は上端から 24 px、下の刃は左端から 16 px、右の空きは 9 px と確かめた（2026-10-04）                          |
| C2   | 済み   | 錆槍の竜兵の指示文をそのまま使った（§4）。台帳 `Docs/art/asset-ledger.csv` に行を足した（RPG-by-card PR #9）                                                                      |
| C3   | 済み   | こうだいさんが Krita で生成した（`上級槍竜兵２.kra`、生成のレイヤー 2 枚）                                                                                                        |
| C4   | 済み   | こうだいさんが一番上の候補（種 1195294282）をこの敵に当てた（2026-10-03、#287）                                                                                                   |
| C5   | 一部   | 背景の灰色は抜いた。足元の土の盛り上がりは残した。類似確認（台帳の `similarity_check`）はまだ                                                                                     |
| C6   | 未着手 | 加筆はせずに待機の絵を先に入れた。燐の発光を足すかはこうだいさんが決める（§1）。差分（攻撃・倒れ）は待機の絵が画面に入ってから                                                    |
| C7   | 未着手 | —                                                                                                                                                                                 |
| C8   | 一部   | 待機の絵（900 × 1536、左向き、背景透明）を Unity リポに置いた（RPG-by-card PR #9）。`.meta` と Preset の登録（`asset-intake.md` §5）、枠の幅（#302）、Unity Editor での確認はまだ |

**人に渡したもの**: Unity リポの PR #9（https://github.com/sunbreak-pro/RPG-by-card/pull/9。待機の絵・台帳・生成条件の写し）。**次に人がすること**: こうだいさんがメインの checkout で Preset を登録してから PR #9 を取り込み、`Tools > Depiction > Collect Character Art`（`FigureArtCollector.cs:15`）で絵を Figure に載せて、Unity Editor で `.meta` と見え方を確かめる。この敵はまだ BattleCore にない（`unity-port/BattleCore/Enemies.cs` に id がない）ため、`Battle.unity` で戦って見ることはまだできません。

**前提**: 絵柄の規約（`style-guide.md`）と取り込み規約（`asset-intake.md`）はあります。#287 の本文にある「右向きに反転する」は、2026-10-03 の決定（#287 のコメント、#85）で外れました。敵は左向きのまま使います。
