# 錆槍の竜兵（polearm_warped）— 仕様カード

- 作成日: 2026-09-20 / 更新: 2026-09-23（#146。寸法・生成時間・指示文を直し、題材を #128 の竜人へ寄せた）/ 2026-10-03（#85。向きを左向きに変え、待機の絵を Unity リポへ入れた）/ 2026-10-04（#85。行動をロースター v4.6 の 5 つに揃え、間合いをマスの語に直し、差分の指示文を足した）/ 2026-10-10（#85。取り込み設定と画面での確認が済んだので、進捗を差分の作成待ちに直した）/ 工程: C0（`visual-production-pipeline character polearm_warped`）
- 正本: `.claude/docs/enemy_document/enemy_roster_v4.md`（錆槍の竜兵の節）/ `.claude/docs/vision/world-v1.md`（竜の系譜）/ `.claude/docs/battle_document/battle_core_v4.md` §7（マスと間合い）/ `.claude/docs/battle_document/battle_ui_ux_v2.md`（床のマス・届くマス・狙うマス）/ `.claude/docs/art_document/style-guide.md`（原本の大きさと指示文）/ `.claude/docs/art_document/asset-intake.md`（書き出しと取り込み）/ `unity-port/unity-project-kit/Assets/View/Depiction/Editor/DepictionPrefabBuilder.cs`（枠と配置）/ `unity-port/unity-project-kit/Assets/View/Depiction/Script/FigureMotion.cs`（差分の絵の名前と動き）
- 手順書: `docs/reports/2026-09-19-krita-ai-setup-guide.html` 4 章（待機）/ `docs/reports/2026-10-03-enemy-motion.html`（差分）
- 内部 ID は `polearm_warped` のままです。ロースターが ID を替えないと決めているので、ファイル名と asset id もこの ID を使います（`enemy_roster_v4.md` §1.8）。

## 1. この敵が何者か

階層 1〜2 の通常敵です。竜の系譜では眷属の段にいる **竜人** で、二足で立って武器を持ちます（`world-v1.md` §2.1）。ロースターの記述は「土色の鱗を持ち、槍の穂先が瘴気に侵されて赤く錆びている」です。名前の「錆」は、瘴気の帯び方がいちばん浅い段階を指します（`world-v1.md` §7.1）。

相手が間合い 1〜2 にいると薙ぎ払います。隣（間合い 0）へ入られたら、石突きで 2 マス押し返します。離れた相手（間合い 3 以上）には、踏み込んで 1 マス寄ります。この押し引きを最初に教える役です。HP 60 / 最大スタミナ 10 / 回復 2 / 大きさ 1 / 開始の間合い 3 / 得意な間合い 1〜2 で、状態（出血・鈍足など）は使いません（`enemy_roster_v4.md` §2.1）。

絵では、長い柄の武器を持っていることと、その柄の長さで相手を槍の届くマスに留める構えでいることを伝えます。どの敵も床のマスに立ち、マスの移動は View が人型ごと動かして見せます（`battle_core_v4.md` §7.1、`battle_ui_ux_v2.md` §1.1 A2・§5.7）。絵では立ち位置を描き分けず、性格は構えと武器の長さで見せます。眷属は系譜の最下段なので、竜らしさは鱗・角・尻尾・爪に留め、翼は描きません。ロースターの記述に翼が無いためです。

## 2. 画面での大きさと基準点

| 項目 | 値 | 出典 |
| --- | --- | --- |
| 表示枠 | 210 × 450（Canvas の参照単位で、比は 0.467）。実画素は 1080p で 450、1440p で 600、4K で 900 です | `DepictionPrefabBuilder.cs:219`、`:473-475`（`ScaleWithScreenSize`、参照 1920 × 1080、`matchWidthOrHeight = 0.5`） |
| 絵の収まり | `preserveAspect = true` です。640 × 1536（比 0.417）の絵は高さいっぱいに収まり、幅は約 188 単位になります | `DepictionPrefabBuilder.cs:223` |
| 基準点 | 足元の中央（pivot は (0.5, 0)） | `DepictionPrefabBuilder.cs:219` |
| 左右の向き | 左向きで描きます。Unity は反転せず、画面右（x = +400）から左のプレイヤーをそのまま向きます | `DepictionPrefabBuilder.cs:499`、`:503-504`。向きは `FigureFacing`（`FigureMotion.cs:59-64`）が決めます（#288、PR #293） |
| 頭上の空き | 予兆の札（270 × 68）は、枠の上端から約 44 単位上に出ます。枠の中は上端まで描いてかまいません | `DepictionPrefabBuilder.cs:507`（札の中心が y = 468）、`:490`（足元が y = −60） |
| 数字と文字の位置 | 数字は足元から 310 単位（胸の 270 + 40）、ラベルは 440 単位（頭の 470 − 30）に出ます | `DepictionPrefabBuilder.cs:224-225`、`DepictionPlayer.cs:367-368` |
| 原本の大きさ | **640 × 1536** の文書です。書き出しも 640 × 1536 のままにして、縮小しません | `style-guide.md` §2、`asset-intake.md` §1.2 |

**利き手**: 武器が画面左（プレイヤー側）へ来るように描きます。原本と画面で向きは同じです。

**穂先の長さの制約（2026-10-03 に決着）**: 原本の幅は 640 px で、高さの 0.42 倍しかありません。長柄を身長の 1.4 倍（身長 1.7 m に 2.4 m）で描くと、縦に立てても原本の高さを超え、横に構えると幅を大きく超えます。採用した待機の絵を高さ 450 px の影絵で確かめたところ、穂先は枠の上端から 4 px、下の刃は左端から 6 px の内側に収まっていました（#85 の 2026-10-03 のコメント）。そこで原本は 640 × 1536 のままにし、表示枠を広げる実装は入れません。差分の絵も同じ大きさで描き、柄が原本の端で切れない構えにします（§4.2）。

## 3. 行動と、絵が要る場面

`enemy_roster_v4.md` §2.1（v4.6）にある 5 行動です。「届く」は届く間合いで、敵のマスから数えて相手との間合い N がこの範囲にあるときに当たります（`battle_core_v4.md` §7.2）。「—」は自分にだけ効く行動です。

| 行動 id | 名前 | 届く | 数値（属性・列） | 絵で見せること | asset_id |
| --- | --- | --- | --- | --- | --- |
| `sweep` | 薙ぎ払い | 1〜2 | 攻撃・列 2。威力 8。間合い 2 以上なら威力 +3 | 柄を寝かせて振り抜く。主力 | `chr.polearm_warped.act.sweep` |
| `shove` | 石突きの押し込み | 0 | 攻撃・列 2。威力 5、相手を 2 マス押す。無防備（相手の Guard が 0）なら威力 +3 | 柄の尻で隣の相手を突き放す | `chr.polearm_warped.act.shove` |
| `reach_thrust` | 穂先の突き | 0〜2 | 攻撃・列 1。威力 4 | 腰だめから前へ伸ばす。削り | `chr.polearm_warped.act.reach_thrust` |
| `guard_up` | 柄で受ける | — | 防御・列 1。Guard 3。温存: 残 4 以上なら次の回復 +1 | 柄を斜めに立てて受ける | `chr.polearm_warped.act.guard_up` |
| `step_forward` | 踏み込み | — | 防御・列 1。前へ 1 マス、Guard 2。予兆は「移動」 | 絵は作らない。View が体を前へ運ぶ | — |

決定木は、間合い 0 で shove / guard_up / reach_thrust、1〜2 で sweep / reach_thrust / guard_up、3 以上で step_forward / guard_up です（`enemy_roster_v4.md` §2.1）。間合い 1〜2 の枝で最初に挙がるのは薙ぎ払いで、2 以上に留まる相手には威力 +3 で当たります（`enemy_roster_v4.md:232`、`:234`）。

2026-10-04 に `step_forward`（踏み込み）を足し、`sweep` と `shove` の条件をマスの語に直しました。前の版にあった 2 値の距離の条件は「間合い 2 以上なら +3」に、位置を反転する押しは「相手を 2 マス押す」に、ロースター v4.3 で替わっています（`enemy_roster_v4.md` §1.1 の `:104` と、§11 の 2026-09-23 v4.3 の `:887`）。旧版にあった `reposition`（間合い取り直し）はロースターから消えたので外しました。

**技名はロースターのままにしています。** `world-v1.md` §7.2 は「薙ぎ払い」と「石突き」を和の武術語として置き換えると決めていますが、ロースターはまだ置き換えていません。食い違いは #172 に起票しました。行動 id は変わらないので、asset id とファイル名には影響しません。

### 3.1 差分の一覧と作る順

差分は「待機 / 行動 / 被弾 / 崩れ / 倒れ」で、体勢（構え）の差分は作りません（skill 命名節、2026-09-14 決定）。#85 が挙げる 8 枚を、作る順に並べました。ファイル名は `Assets/Art/Characters/polearm_warped/chr_polearm_warped_<category>_<label>.png` です（`asset-intake.md` §4）。

| 差分 | asset_id | View の口 | 順 | 状態 |
| --- | --- | --- | --- | --- |
| 待機 | `chr.polearm_warped.idle.stand` | 待機 | — | 済み（RPG-by-card#8） |
| 薙ぎ払い | `chr.polearm_warped.act.sweep` | 行動 | 1 | 未作成。指示文は §4.2 の 1 |
| 倒れ | `chr.polearm_warped.react.down` | 倒れ | 1 | 未作成。指示文は §4.2 の 2 |
| 被弾 | `chr.polearm_warped.react.hit` | 被弾 | 2 | 未作成。指示文は §4.2 の 3 |
| 突き | `chr.polearm_warped.act.reach_thrust` | 行動 | 3 | 未作成。指示文は §4.2 の 4 |
| 押し込み | `chr.polearm_warped.act.shove` | 行動 | 3 | 未作成。指示文は §4.2 の 5 |
| 受け | `chr.polearm_warped.act.guard_up` | 行動 | 3 | 未作成。指示文は §4.2 の 6 |
| 崩れ | `chr.polearm_warped.react.<未定>` | なし | 4 | 未作成。指示文は §4.2 の 7。名前は未定 |

順 2 以降と崩れの構えは、この版で置いた仮の案です。手触りの確認（#79・#60）の結果で見直します。

- **順 1 は 2 枚です。** 攻撃 1 枚と倒れ 1 枚を先に作り、1 枚で足りるかを手触りの確認で見てから増やします（`docs/reports/2026-10-03-enemy-motion.html`）。作り始めるのは、待機の絵が `Battle.unity` に映ったのを確かめてからです（§6）。
- **View の口は 4 つです。** 待機 / 行動 / 被弾 / 倒れで、崩れの口はありません（`FigureMotion.cs:12-18`、`battle_ui_ux_v2.md:1121`）。崩れの名前（`react.` の後ろ）は、View に崩れの口が入るときに、`asset-intake.md` §4 の付け方で決めます。
- **行動の口は 1 つだけです。** `chr_polearm_warped_act_*.png` のうち、名前順で最初の 1 枚を全ての行動に使います（`FigureArtCollector.cs:72-85`）。敵が予兆を実行する瞬間は、受けと踏み込みも行動の絵に替わります（`DepictionPlayer.cs:436-441`）。
- **順 3 の 3 枚は、View が行動 id ごとに絵を引くようになるまで Unity に置きません。** `act_guard_up` / `act_reach_thrust` / `act_shove` はどれも名前順で `act_sweep` より前なので、1 枚でも置くとその絵が全ての行動に出ます。

**絵柄**: 線・塗り・色・光と、瘴気の帯び方（錆）・竜の系譜の段（眷属・竜人）の描き分けは、`art_document/style-guide.md` の第 2 部（§12〜§19）に従います。影絵は同 §17 の関門を通してから C3 へ進みます。差分も同じ関門を通します。

## 4. 生成の指示文

### 4.1 待機（段階 1・案出し用）

`style-guide.md` §4.3 の竜人の例文を、この敵に合わせて鱗の色と持ち物だけ替えたものです。スタイルは `card-battle Animagine`、文書は **640 × 1536**、生成回数 4 を 2〜4 回まわして 8〜16 枚出します。

```
1other, solo, safe, full body, standing, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, polearm held upright, rust, shoulder armor, {torn clothes|torn cape|vambraces}, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

替えた語は 3 つです。例文の `black scales` と `blue theme` を、土色の鱗に合わせて `brown scales`（Danbooru 163 件）と `brown theme`（4,915 件）にしました。`brown scales` は件数が少なく効きが弱いので、`brown theme` で支えます。穂先の赤錆には `rust`（1,279 件）を足しました。件数は 2026-09-23 に Danbooru の公開 API で引いた値です。

採用した待機の絵では、ワイルドカードは `vambraces` を選んでいました。種は 2367636945 です（RPG-by-card `Docs/art/recipes/chr.polearm_warped.idle.stand.workflow.json` の `prompt_eval` と `seed`）。

負の指示文は `style-guide.md` §4.4 の「全被写体で使う文字列」をそのまま使います。竜人は人型なので、竜・亜竜・小竜用の追加の語は足しません。

**`facing right` は書きません。** Danbooru に 0 件で、条件付けになりません。右向きで出た候補は、採用前に Krita で水平反転して左向きに直します（`style-guide.md` §4.2）。

ワイルドカード（`{a|b|c}`）は生成ごとに 1 つが選ばれます。良かった組み合わせの固定のしかたは `style-guide.md` §4.5 にあります。

禁止事項: 既存の作品名・作者名を入れない。流血を描かない。購入素材や他作品の画像を参照に入れない。`style-guide.md` §5 の使わない語（`soldier` `corrupted soldier` `japanese dark fantasy` など）を入れない。

### 4.2 差分（待機の絵に揃える）

差分は、待機の絵と同じ竜兵に見えることを先に守ります。待機の `.kra` を複製し、棒人間の Scribble と、待機の絵の Reference の 2 つを Control layer に入れて生成します。手順の全体は `docs/reports/2026-10-03-enemy-motion.html` の「2 段目の絵を Krita で作る手順」にあり、要点は次のとおりです。

- **原本は待機と同じ 640 × 1536 にします。** 足の裏は最下行に接地させ、足の中心は待機の絵と同じ x = 327 に置きます。差し替えたときに体が横へ跳ばないためです。待機の絵の足の位置は、RPG-by-card `Docs/art/asset-ledger.csv` の 1 行目の notes にあります。
- **棒人間は左（プレイヤーの側）へ向けて描きます。** 線は黒で、太めでかまいません。逆向きの候補は、`style-guide.md` §4.2 のとおり水平反転して直します。反転したら、肩当てなど片側だけの意匠が待機の絵と同じ側にあるかを確かめ、逆なら描き直します。
- **柄は原本の端で切りません。** 収まらない分は体と尻尾の後ろへ隠します。穂先と刃は、待機の絵と同じく上端と左端の内側に置きます（§2）。
- **Control layer は 2 つです。** 1 つ目は種類を Scribble にして棒人間のレイヤーを選び、「From image」は押しません。2 つ目は種類を Reference にして待機の絵のレイヤーを選びます。名前が noob で始まる Pose と Reference のモデルは使いません。
- **Batches を 4 にして生成し、待機の絵にいちばん近い 1 枚を選びます。** 甲の形・角の本数・槍の飾りがずれていたら手で描き足し、そのレイヤーは結合せずに残します。選択範囲を描き直すときは、Refine を強度 80% 以下で使います（`style-guide.md` §6）。
- **負の指示文は `style-guide.md` §4.4 のままです。**

**指示文は、待機の採用文（§4.1、`vambraces` で固定）のうち姿勢の語だけを替えます。** 替えるのは姿勢と表情の語です。[3] 画角の `standing` と、[5] 持ち物・構えの `holding polearm, polearm held upright` を替え、被弾・倒れ・崩れでは表情と体の向きの語も足します。材質・塗り・背景のスロットは待機と同じにして、顔・鱗・甲を揃えます（スロットは `style-guide.md` §4.1）。

竜兵の動きの作り方の報告（`docs/reports/2026-10-03-enemy-motion.html`）が足す語のうち、`attacking` と `defeated` は 2026-10-04 に Danbooru で 0 件でした。`swinging polearm` と `dropped polearm` はタグではありません。0 件の語は条件付けにならないので（`style-guide.md` §5）、下の文では実在のタグに替えています。括弧の件数は 2026-10-04 に Danbooru の公開 API で引いた値です。素の英語（タグではない語）は各文の下に挙げます。

**1. 薙ぎ払い（`act.sweep`、順 1）**

棒人間: 両足を前後に開き、上体を左へ少し倒します。柄を斜めに寝かせ、穂先を左下（プレイヤーの胸から足の高さ）へ振り抜いた終わりの形にします。柄の尻は右上の肩の後ろに置きます。2.4 m の柄は 640 px の幅に水平には収まらないので、水平に寝かせません。

```
1other, solo, safe, full body, standing, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, fighting stance, swinging weapon, dynamic pose, leaning forward, polearm swung low and forward, rust, shoulder armor, vambraces, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `fighting stance`（34,247）、`swinging weapon`（1,128）、`dynamic pose`（4,093）、`leaning forward`（164,942）です。素の英語は `polearm swung low and forward` です。

**2. 倒れ（`react.down`、順 1）**

棒人間: 片膝をつき、頭を下げて上体を前へ崩します。槍は手から離れ、穂先を左の床に着けて、柄が右上へ斜めに倒れかけた形にします。床に寝かせた槍は 640 px に収まらないためです。

```
1other, solo, safe, full body, on one knee, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, polearm, head down, exhausted, polearm falling from hand, rust, shoulder armor, vambraces, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

`standing` を `on one knee`（19,054）に替え、`holding polearm` を `polearm`（75,355）にしました。手から離れた槍を描かせるためです。動きの報告の `kneeling` は両膝をつく形も含むので、片膝の `on one knee` にしました。足した語は `head down`（3,833）と `exhausted`（3,815）です。素の英語は `polearm falling from hand` です。

View は倒れの 800 ms で、絵ごと足元を軸に右（後ろ）へ 24° 傾け、40 単位沈めて暗くし、消します（`FigureMotion.cs:111-151`、`Effects.cs:289`）。前へ崩れた絵が後ろへ傾く動きと合うかは、Editor で確かめます（§6）。

**3. 被弾（`react.hit`、順 2）**

棒人間: 足は待機の位置のまま、上体を右（後ろ）へ反らします。柄は待機に近い角度で持ったままにします。白い点滅は View が絵の形で重ねます（#288）。

```
1other, solo, safe, full body, standing, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, polearm held upright, leaning back, wince, clenched teeth, rust, shoulder armor, vambraces, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `leaning back`（26,473）、`wince`（17,303）、`clenched teeth`（107,522）です。`injury`（40,821）は傷と血を呼ぶので使いません。

**4. 突き（`act.reach_thrust`、順 3）**

棒人間: 腰を落として前の足へ体重を乗せ、柄を腰だめから左へ伸ばします。穂先は左端の内側に置き、柄の尻は右の肘の下から体の後ろへ隠します。

```
1other, solo, safe, full body, standing, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, fighting stance, leaning forward, outstretched arm, polearm thrust forward, rust, shoulder armor, vambraces, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `fighting stance`、`leaning forward`、`outstretched arm`（95,582）です。素の英語は `polearm thrust forward` です。`stab`（3,597）は人を刺す絵を呼ぶので使いません。

**5. 押し込み（`act.shove`、順 3）**

棒人間: 前の足を踏み出し、柄を体の前で斜めに持ち替えて、柄の尻（下の端）を左へ突き出します。穂先は右上の肩の後ろに置きます。

```
1other, solo, safe, full body, standing, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, fighting stance, pushing, leaning forward, striking with the butt of the polearm, rust, shoulder armor, vambraces, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `fighting stance`、`pushing`（1,869）、`leaning forward` です。素の英語は `striking with the butt of the polearm` です。

**6. 受け（`act.guard_up`、順 3）**

棒人間: 両足を開いて腰を落とし、柄を体の前で斜めに立てます。穂先は右上、柄の尻は左下に置きます。

```
1other, solo, safe, full body, standing, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, fighting stance, blocking, polearm held diagonally across the body, rust, shoulder armor, vambraces, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `fighting stance` と `blocking`（979）です。`blocking` は目安の 1,000 件を下回る弱い語なので、構えは棒人間で決めます。素の英語は `polearm held diagonally across the body` です。

**7. 崩れ（`react.<未定>`、順 4）**

棒人間（仮）: 足は待機の位置のまま、上体を前へ丸めます。柄の先を下げ、構えが解けた形にします。

```
1other, solo, safe, full body, standing, from side, profile, monster, dragon horns, slit pupils, scales, brown scales, claws, dragon tail, holding polearm, hunched over, heavy breathing, polearm lowered, rust, shoulder armor, vambraces, anime coloring, high contrast, limited palette, muted colors, brown theme, simple background, grey background
```

足した語は `hunched over`（2,072）と `heavy breathing`（52,396）です。素の英語は `polearm lowered` です。

**未確認のこと**: Scribble と Reference を組み合わせて、待機の絵とどこまで同じ竜兵に見える絵が出るかは、まだ試していません。揃わなければ、Flux 2 Klein 4B の編集で構えを変える形を試します（`style-guide.md` §10）。差分の文は待機より 2〜4 語長いので、正の指示文が 77 トークンの区切りを越えるかも数えていません。

## 5. 時間の予算

1 枚の生成は約 21 秒と見ます。640 × 1536 は 983,040 画素で、実測した 1024 × 1024（1,048,576 画素、28 ステップで 21.09 秒。`%APPDATA%\krita\ai_diffusion\logs\server.log` 2026-09-20 13:32）とほぼ同じ画素数だからです。16 枚で約 5.5 分です。C6 の加筆時間は 1 体目で実測し、残りの体の計画の基準にします。

動きの報告は、攻撃の絵 1 体ぶんを 30〜60 分と見込んでいます（`docs/reports/2026-10-03-enemy-motion.html`）。1 枚ごとの時間は、順 1 の 2 枚で実測します。生成だけなら、4 枚で約 1.4 分です。

## 6. 進捗

| 工程 | 状態 | 備考 |
| --- | --- | --- |
| C0 | 済み | このファイル。2026-10-03 に向きを左向きへ変えた（#85）。2026-10-04 に行動をロースター v4.6 の 5 つに揃え、間合いをマスの語に直した（#85、#167） |
| C1 | 一部 | SVG の影絵は作っていない。待機の絵を黒で塗り、高さ 450 px で見て、穂先は上端から 4 px、下の刃は左端から 6 px 内に収まると確かめた（2026-10-03）。これで穂先の長さの制約（§2）を閉じ、原本は 640 × 1536 のままにした。`style-guide.md` §17 の関門のほかの条件は未確認 |
| C2 | 済み | 待機の指示文は §4.1、差分の指示文は §4.2（2026-10-04）。台帳 `Docs/art/asset-ledger.csv` を Unity リポに置き、1 行目を足した（RPG-by-card#8） |
| C3 | 済み | こうだいさんが Krita で待機を生成した（`竜の槍兵2.kra`、候補 6 枚） |
| C4 | 済み | こうだいさんが一番上の候補（種 2367636945）を採用した（2026-10-03、#85 のコメント） |
| C5 | 一部 | 背景の灰色は抜いた。足元の影は残した。類似確認（台帳の `similarity_check`）はまだ |
| C6 | 未着手 | 加筆はせずに待機の絵を先に入れた。差分は §3.1 の順で作る。待機の絵が `Battle.unity` に映ったので（C8）、順 1（薙ぎ払い・倒れ）に進める。差分の絵は 1 枚もまだ無い |
| C7 | 一部 | #288（PR #293）で、絵 1 枚を画面で動かす段が入った。敵の踏み込み（46 単位、100 ms）、行動の系統ごとの体の伸び、800 ms の倒れが動く（`DepictionPlayer.cs:516`、`FigureMotion.cs:77-151`、`Effects.cs:249`、`:289`） |
| C8 | 一部 | 待機の絵（640 × 1536、左向き、背景透明）は RPG-by-card#8 で Unity リポに入った（2026-10-03）。取り込み設定（`asset-intake.md` §3）は RPG-by-card#10 で当て、同じ PR で Preset を Preset Manager に登録し、Figure の絵の棚に `polearm_warped` を載せた（2026-10-05）。敵の反転は #288 で外れた。こうだいさんの試運転で、`Battle.unity` の錆槍の竜兵が待機の絵のまま左を向いて立つのを確かめた（2026-10-05、#79）。差分が入るまでは一部のまま |

**人に渡したもの**: Unity リポの RPG-by-card#8（待機の絵・台帳・生成条件の写し）と RPG-by-card#10（取り込み設定・Preset・絵の棚。どちらも merge 済み）、§4.2 の差分の指示文です。

**次に人がすること**: こうだいさんが、§4.2 の 1 と 2（薙ぎ払い・倒れ）を Krita で作ります。待機の絵と同じ 640 × 1536 で、足の位置も待機の絵にそろえます。できた差分は Claude が受け取り、背景を抜いて Unity リポの `Assets/Art/Characters/polearm_warped/` に入れます。差分の PNG には、登録済みの Preset が初回の取り込みで自動で当たります（`asset-intake.md` §5）。

**前提**: 絵柄の規約（`style-guide.md`）と取り込み規約（`asset-intake.md`）はあります。Unity リポの台帳 `Docs/art/asset-ledger.csv` は RPG-by-card#8 で `.claude/docs/art_document/asset-ledger.template.csv` から置きました。列の定義は `asset-intake.md` §7 です。
