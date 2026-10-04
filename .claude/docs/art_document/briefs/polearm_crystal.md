# 晶槍の竜兵（polearm_crystal）— 仕様カード

- 作成日: 2026-10-04（#287。こうだいさんが Krita で作った待機の絵を Unity リポへ入れた）/ 工程: C0（`visual-production-pipeline character polearm_crystal`）
- 正本: `.claude/docs/enemy_document/enemy_roster_v4.md`（§2.7 晶槍の竜兵）/ `.claude/docs/vision/world-v1.md`（竜の系譜）/ `.claude/docs/art_document/style-guide.md`（原本の大きさ・指示文・帯び方）/ `.claude/docs/art_document/asset-intake.md`（書き出しと取り込み）/ `unity-port/unity-project-kit/Assets/View/Depiction/Editor/DepictionPrefabBuilder.cs`（枠と配置）
- 手順書: `docs/reports/2026-09-19-krita-ai-setup-guide.html` 4 章（待機の絵）/ `docs/reports/2026-10-03-enemy-motion.html`（攻撃と倒れの差分の作り方）
- 内部 ID は `polearm_crystal` です。v4.6 で足した敵で、錆槍の竜兵の `polearm_warped` にそろえて `polearm_` で始めています（`enemy_roster_v4.md` §1.8）。ファイル名と asset id もこの ID を使います。`asset-intake.md` §4 には #128 の付け替えを待つ段落が残っています。この版はロースターに従いました（食い違いは #327）。

## 1. この敵が何者か

階層 3〜4 の通常敵です。錆槍の竜兵の上位で、竜の系譜では同じ眷属の **竜人** です（`enemy_roster_v4.md` §1.8・§2.7）。ロースターの記述は「錆槍の竜兵と同じ種で、鱗の縁に結晶が育っている。槍の穂先が大きな結晶に育ち、体ごと前のめりに構える」です。名前の「晶」は、結晶が育った段の帯び方を指します（`style-guide.md` §15）。

得意な間合いは 1〜2 です。届く間合い 1〜2 で重い突きを出すところは錆槍と同じで、間合いの動かし方が逆になります。錆槍は相手を押し返しますが、晶槍は相手を動かさず、攻撃のたびに自分が前後へ動きます。間合い 3 以上の相手には突き進みながら当てて 2 マス詰めます。隣のマス（間合い 0）の相手には、突いてから 2 マス退きます。HP 90 / 最大スタミナ 10 / 回復 2 / 大きさ 1 / 開始の間合い 3 で、状態は使いません。

絵では、長柄の武器を持っていることと、体ごと前のめりに構えて自分から間合いを詰めたり空けたりする槍兵であることを伝えます。錆槍と同じ種なので、二足・長柄・尻尾・角という影絵の基本形はそろえます。眷属なので翼は描きません。

**鱗の色と結晶（要判断）**: 待機の絵は鱗が土色で、結晶を描いていません。生成に使った指示文が錆槍の竜兵と同じもの（`rust` と `brown theme` を含む。§4）だったためです。ロースターは、鱗の縁に結晶が育ち、穂先も大きな結晶に育つと書いています。`style-guide.md` §15 の「晶」は、鱗の縁・背・肩から菫の結晶を生やして、体の塗りの 10〜25% を占めさせると決めています。このまま使うか、C6 の加筆で鱗の縁と穂先に結晶を足すかは、こうだいさんが決めます。差分は待機の絵を見本にして作るので、決めるのは差分を作る前です。

**構え**: 前のめりの構えはロースターの記述に従いました。`style-guide.md` §16.2 は、深い層の上位種は持ち物と構えを据え置くと決めています。この食い違いは #326 に書き、ロースターに従ったことを残しました。

## 2. 画面での大きさと基準点

| 項目 | 値 | 出典 |
| --- | --- | --- |
| 表示枠 | 210 × 450（Canvas の参照単位で、比は 0.467）。実画素は 1080p で 450、1440p で 600、4K で 900 です | `DepictionPrefabBuilder.cs:219`、`:473-475`（`ScaleWithScreenSize`、参照 1920 × 1080、`matchWidthOrHeight = 0.5`） |
| 絵の収まり | `preserveAspect = true` です。800 × 1536（比 0.521）の絵は枠より横長なので、幅 210 に合わせて縮み、高さは 403.2 になります。絵の `Image` の pivot が (0.5, 0.5) なので、足元から 23.4 浮きます | `DepictionPrefabBuilder.cs:222-223`、#302 |
| 基準点 | 足元の中央（枠の pivot は (0.5, 0)） | `DepictionPrefabBuilder.cs:219` |
| 左右の向き | 左向きで描きます。Unity は反転せず、画面右（x = +400）から左のプレイヤーをそのまま向きます | `DepictionPrefabBuilder.cs:499`、`:503-504`、`FigureView.cs:58` |
| 頭上の空き | 予兆の札（270 × 68）は、枠の上端から約 44 単位上に出ます。枠の中は上端まで描いてかまいません | `DepictionPrefabBuilder.cs:362`、`:507`（札の中心が y = 468）、`:490`（足元が y = −60） |
| 数字と文字の位置 | 数字は足元から 310 単位（胸の 270 + 40）、ラベルは 440 単位（頭の 470 − 30）に出ます | `DepictionPrefabBuilder.cs:224-225`、`DepictionPlayer.cs:367-368` |
| 原本の大きさ | **800 × 1536** です。決まりの 640 × 1536 より 160 広く、書き出しも 800 × 1536 のままです。800 は 4 の倍数なので BC7 が効きます | `style-guide.md` §2、`asset-intake.md` §1.2・§3 |

`style-guide.md` §2 と `asset-intake.md` §1.2 には、幅の例外がまだありません。2 体の幅（晶槍の竜兵の 800 と不退の槍竜の 900）を例外として書き足す作業は、design レーンで別に行います。

**利き手**: 下の刃と伸ばした爪が画面左（プレイヤー側）に来ています。原本と画面で向きは同じです。

**穂先の長さの判断（済み、2026-10-04）**: 描いた幅 800 のまま入れました。640 に切ると、いちばん良い位置（左端 x = 137）でも下の刃と伸ばした爪が欠け、絵の画素の 4.7% を失うためです。いまの 210 × 450 の枠では、高さ 403.2 で足元から 23.4 浮いて出ます。枠を 234.4 × 450（450 × 800 / 1536）に広げれば、高さいっぱいに立ちます。枠を敵ごとに広げる作業は、battle レーンの #302 です。

影絵を高さ 450 px に縮めて確かめた値は次のとおりです。穂先は上端から 8 px（原本で 27 px）、下の刃は左端から 12 px（原本で 41 px）の内側にあります。尾の棘は原本で x = 797 まで届きます。右端との空きは原本で 2 px、450 px では 1 px 未満で、ほぼ接します。同じ縮尺（450 / 1536）で影絵の高さは 442 px で、錆槍の竜兵は 446 px です。

**接地**: 絵を 13 px 下、18 px 右へ動かしました。下の穂先と、その下に描かれた地面の線が最下行（y = 1535）に乗っています。爪の裏は最下行の約 12 px 上で終わり、足の下の影は下 8 行が切れています。爪を最下行に置かなかったのは、置くと穂先が約 9 px 欠けるためです。足の中心は x = 378 前後で、キャンバスの中央（400）から 22 px 左にあります。`asset-intake.md` §1.2 は足の裏を最下行に接地させ、足の中心をキャンバスの中央に置くと決めていますが、この絵はどちらも満たしません。尾の棘が右端に届いているので、これ以上右へは寄せられません。画面の側で直すかは #302 で決めます。

## 3. 行動と、絵が要る場面

`enemy_roster_v4.md` §2.7 の 5 行動です。差分は「待機 / 行動 / 被弾 / 崩れ / 倒れ」で、体勢（構え）の差分は作りません（skill 命名節、2026-09-14 決定）。表の「届く」は届く間合いで、敵のマスから数えます。

| 行動 id | 名前 | 絵で見せること | asset_id |
| --- | --- | --- | --- |
| `great_thrust` | 大穂の突き | 前のめりのまま、穂先を真っ直ぐ突き出す。主力（列 3、届く 1〜2、威力 13） | `chr.polearm_crystal.act.great_thrust` |
| `crystal_rush` | 晶穂の突進 | 穂先を前に据えて突き進む。当ててから前へ 2（列 2、届く 2〜4、威力 5。間合い 3 以上で +3） | `chr.polearm_crystal.act.crystal_rush` |
| `recoil_thrust` | 引き突き | 突いた直後に後ろへ退く。当ててから後ろへ 2（列 2、届く 0、威力 5。相手の Guard が 0 なら +3） | `chr.polearm_crystal.act.recoil_thrust` |
| `short_jab` | 短い突き | 柄を短く持って小さく突く。削り（列 1、届く 0〜2、威力 4） | `chr.polearm_crystal.act.short_jab` |
| `haft_guard` | 柄を立てる | 柄を縦に立てて受ける（列 1、自分にだけ効く、Guard 3） | `chr.polearm_crystal.act.haft_guard` |

前へ 2 と後ろへ 2 は、画面の側で人型をマスごと動かして見せます（`battle_ui_ux_v2.md` §5.7）。移動だけの絵は作りません。

待機は `chr.polearm_crystal.idle.stand`、被弾・崩れ・倒れは `chr.polearm_crystal.react.*` です。ファイル名は `Assets/Art/Characters/polearm_crystal/chr_polearm_crystal_<category>_<label>.png` です（`asset-intake.md` §4、`FigureMotion.cs:37-40`）。View の口は待機 / 行動 / 被弾 / 倒れの 4 つで、崩れの口はありません（`FigureMotion.cs:12-18`、食い違いは #309）。崩れの名前は口が入るときに決めます。

**差分は攻撃 1 枚と倒れ 1 枚から作ります**（`docs/reports/2026-10-03-enemy-motion.html` の 2 段目）。行動の絵は、`act_` で始まるファイルのうち名前順で最初の 1 枚が全ての行動に出ます（`FigureArtCollector.cs:72-85`、食い違いは #310。この版は View に従いました）。そこで 1 枚で 5 行動を代表させます。`act_crystal_rush` は名前順で `act_great_thrust` より前なので、View が行動 id ごとに絵を引くようになるまで Unity に置きません。

| 順 | 差分 | asset_id | ファイル名 | 見せること |
| --- | --- | --- | --- | --- |
| 1 | 攻撃 | `chr.polearm_crystal.act.great_thrust` | `chr_polearm_crystal_act_great_thrust.png` | 大穂の突き。前のめりの体から、穂先を画面左へ真っ直ぐ伸ばす |
| 2 | 倒れ | `chr.polearm_crystal.react.down` | `chr_polearm_crystal_react_down.png` | 膝をつき、上体が前へ崩れ、槍が手から離れて床に落ちる |

被弾（`react.hit`）と、残りの行動の差分（`act_crystal_rush` を除く）は、手触りの確認（#79・#60）で 1 枚で足りるかを見てから足します。差分は待機の絵と同じ 800 × 1536 で作り、足の位置も待機の絵にそろえます。絵が切り替わったときに足元が跳ねないようにするためです。突きの穂先が左端からはみ出しそうなら、穂先をやや下へ向けて枠の中に収めます。

**絵柄**: 線・塗り・色・光と、瘴気の帯び方（晶）・竜の系譜の段（眷属・竜人）の描き分けは、`art_document/style-guide.md` の第 2 部（§12〜§19）に従います。影絵は同 §17 の関門を通してから差分の生成へ進みます。

## 4. 生成の条件（待機の絵は作成済み）

待機の絵は、こうだいさんが Krita で生成して選んだものです。この敵のための指示文は新しく作っていません。条件は `.kra` の履歴（`ui.json`）から読み、Unity リポの `Docs/art/recipes/chr.polearm_crystal.idle.stand.workflow.json` に写しました（RPG-by-card PR #9）。

| 項目 | 値 |
| --- | --- |
| 元の `.kra` | `C:\Users\user\OneDrive\Desktop\windev\card-battle-image\竜の上級槍兵.kra`（800 × 1536）。原本には手を付けず、写しを読みました |
| 使ったレイヤー | 生成のレイヤーは 1 枚だけ（`[Generated] ... (662526091)`）で、統合画像（`mergedimage.png`）と全画素で一致し、全面が不透明です |
| 使わなかったもの | 「ペイントレイヤー 1」は空（タイル 1 枚、不透明の画素 0）で、加筆はありません。「背景」（白）は捨てました |
| 履歴 | `ui.json` の履歴は 1 件です |
| モデルとスタイル | `animagine-xl-4.0-opt`、スタイルは `anime-illustrious.json`（`card-battle Animagine` ではありません） |
| sampler / 段数 / cfg | Euler A / 28 / 5 |
| 種 | 662526091 |
| 指示文 | 錆槍の竜兵の §4 と同じ文です。ワイルドカード `{torn clothes\|torn cape\|vambraces}` は `vambraces` が選ばれました |
| 負の指示文 | 入力した文は `style-guide.md` §4.4 の「全被写体で使う文字列」と同じです |

差分も待機の絵と同じスタイル `anime-illustrious.json` で作ります。このスタイルは文の末尾に `masterpiece, high score, great score, absurdres` を足し、負の指示文にも自前の語を足します（`chr.polearm_crystal.idle.stand.workflow.json` の `prompt_final` / `negative_prompt_final`）。

**差分の指示文（攻撃）**: 待機の絵の文から `standing` と `polearm held upright` を抜き、ワイルドカードを `vambraces` に固定して、動きの語を足したものです。

```
1other, solo, safe, full body, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, rust, shoulder armor, vambraces, attack, lunging, leaning forward, dynamic pose, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `attack`（Danbooru 5,482 件）、`lunging`（132 件）、`leaning forward`（164,943 件）、`dynamic pose`（4,093 件）です。`lunging` は件数が少なく効きが弱いので、`leaning forward` で前のめりを支えます。動きの作り方の HTML が挙げる `attacking` は 0 件で、`swinging polearm` はタグにありません（`swinging` は廃止）。件数は 2026-10-04 に Danbooru の公開 API で引いた値です。

**差分の指示文（倒れ）**: 同じく待機の文から `standing` `holding polearm` `polearm held upright` を抜き、倒れの語を足したものです。

```
1other, solo, safe, full body, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, polearm, rust, shoulder armor, vambraces, on one knee, head down, exhausted, weapon on floor, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `on one knee`（19,054 件）、`head down`（3,833 件）、`exhausted`（3,815 件）、`weapon on floor`（252 件）、`polearm`（75,355 件）です。HTML の `defeated` は 0 件、`dropped polearm` はタグにないので、`on one knee` と `weapon on floor` に替えました。HTML の `kneeling` の代わりには、片膝の `on one knee` を使います。`weapon on floor` は 252 件と少ないので `polearm` で支えます。

**結晶を足すと決めたとき**: 両方の文で `rust` を `crystal`（36,580 件）と `purple gem`（9,016 件）に替えます。待機の絵も同じ見た目に加筆してから差分を作ります。差分は待機の絵を Reference にするので、待機の絵が土色のままだと結晶はそろいません。

負の指示文は待機の絵と同じです。竜人は人型なので、竜・亜竜・小竜用の追加の語は足しません。

**生成のしかた**は `docs/reports/2026-10-03-enemy-motion.html` の「2 段目の絵を Krita で作る手順」に従います。棒人間の Scribble と、待機の絵の Reference を Control layer に置きます。名前が `noob` で始まる Pose と Reference のモデルは使いません。一部を描き直す Refine は強度 80% 以下にします。

禁止事項: 既存の作品名・作者名を入れない。流血を描かない。購入素材や他作品の画像を参照に入れない。`style-guide.md` §5 の使わない語（`soldier` `corrupted soldier` `japanese dark fantasy` など）を入れない。

## 5. 時間の予算

800 × 1536 は 1,228,800 画素で、Krita の上限（1,050,000 画素、`style-guide.md` §2 の B）を超えます。超えた文書は、縮めて生成してから超解像で戻す経路を通ります。待機の絵がこの経路を通ったかは、ログで確かめていません。生成の時間も測っていません。差分の手間は 1 体につき 30〜60 分の見込みです（`docs/reports/2026-10-03-enemy-motion.html`）。

## 6. 進捗

| 工程 | 状態 | 備考 |
| --- | --- | --- |
| C0 | 済み | このファイル（2026-10-04、#287） |
| C1 | 一部 | SVG の影絵は作っていない。待機の絵を黒で塗って高さ 450 px で見て、穂先は上端から 8 px、下の刃は左端から 12 px の内側にあり、尾の棘は右端にほぼ接する（原本で 2 px）と確かめた（2026-10-04） |
| C2 | 済み | 錆槍の竜兵の指示文をそのまま使った（§4）。台帳 `Docs/art/asset-ledger.csv` に行を足した（RPG-by-card PR #9） |
| C3 | 済み | こうだいさんが Krita で生成した（`竜の上級槍兵.kra`、生成のレイヤー 1 枚） |
| C4 | 済み | こうだいさんがこの絵を #286 の通常の上位に当てた（2026-10-03、#287） |
| C5 | 一部 | 背景の灰色と、右下の角の薄い灰色のにじみを抜いた。類似確認（台帳の `similarity_check`）はまだ |
| C6 | 未着手 | 加筆はせずに待機の絵を先に入れた。結晶を足すかはこうだいさんが決める（§1）。差分（攻撃・倒れ）は待機の絵が画面に入ってから |
| C7 | 未着手 | — |
| C8 | 一部 | 待機の絵（800 × 1536、左向き、背景透明）を Unity リポに置いた（RPG-by-card PR #9）。`.meta` と Preset の登録（`asset-intake.md` §5）、枠の幅（#302）、Unity Editor での確認はまだ |

**人に渡したもの**: Unity リポの PR #9（https://github.com/sunbreak-pro/RPG-by-card/pull/9。待機の絵・台帳・生成条件の写し）。**次に人がすること**: こうだいさんがメインの checkout で Preset を登録してから PR #9 を取り込み、`Tools > Depiction > Collect Character Art`（`FigureArtCollector.cs:15`）で絵を Figure に載せて、Unity Editor で `.meta` と見え方を確かめる。この敵はまだ BattleCore にない（`unity-port/BattleCore/Enemies.cs` に id がない）ため、`Battle.unity` で戦って見ることはまだできません。

**前提**: 絵柄の規約（`style-guide.md`）と取り込み規約（`asset-intake.md`）はあります。#287 の本文にある「右向きに反転する」は、2026-10-03 の決定（#287 のコメント、#85）で外れました。敵は左向きのまま使います。
