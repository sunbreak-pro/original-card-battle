# Krita の指示文 — 戦闘の背景の候補を作る

> 使い方: Krita AI Diffusion の指示文の欄に、下の「層ごとの指示文」の 1 行をそのまま貼ります。負の指示文の欄には「負の指示文」を貼ります。まず基本の 1 行で 8〜16 枚出し、気に入った向きが無ければ「差し替えて試す語」を足して、もう一度出してください。細かい所は書き込んでいません。形と空気は、出てきた候補からこうだいさんが選んでください。
> 背景の絵そのものの Issue は #89（戦闘の背景を作る）です。この文書は、指示文の控えです。
> 2026-11-03 の手引き（`docs/reports/2026-11-03-krita-prompt-guide.html`）の背景の指示文を、いまの色と決まりに合わせて直したものです。直した点は末尾にまとめました。どの指示文も、実機ではまだ試していません。

## 設定

- モデルは Animagine XL 4.0 opt、Euler A・28 段・CFG 5 のままです。どれも触りません。
- 大きさは 1344×768 で候補を出します。採った 1 枚を Upscale で拡大し、1920×1080 に切ります。
- 枚数は、Batch を 1 のまま、Generate の右隣の Batches を 2〜4 にして増やします。
- 指示文は Danbooru のタグを並べる形です。`safe` は必ず入れます。画質の語と、色の 16 進は書きません。

## 絵で守ること

- **見せたいものは上半分に置きます。** 戦闘の画面は、上から 556 px（51%）より下を暗い地面が覆います。下半分はほとんど見えないので、作り込みません。
- **真ん中の帯は静かにします。** 人型が立つ帯（横は画面の 15〜85%、縦は 25〜52%）の後ろは、明るさの差を小さくします。濃い形は、画面の端か上に寄せます。
- **琥珀・オレンジ・金・火は入れません。** 琥珀はプレイヤーの色です。淡い黄色は、レモン寄りにします。
- **光の筋と粒は描きません。** ゲームが絵の上に重ねて動かすので、絵に入れると二重になります。
- **左上と、敵の頭の上には、強い光の点を置きません。** 情報の札と、予兆の札が出る場所です。

## 負の指示文（全層で共通）

```
lowres, worst quality, low quality, low score, bad score, average score, signature, watermark, username, text, blurry, blood, nsfw, 1girl, 1boy, solo, people, crowd, simple background, white background, orange theme, sunset, fire, lantern, torch, sunbeam, light rays, light particles
```

1 層だけ、末尾に `, crystal` を足します。結晶は 2 層から出るためです。

## 層ごとの指示文

絵を置く場所は、いま 7 枚ぶんあります（`Assets/Resources/Backdrops/layer-1.png`〜`layer-7.png`）。

### 1. 燦光の樹海（沈んだ森。天井の大穴から陽が落ちる。若草と淡いレモン色）

```
scenery, no humans, safe, wide shot, horizon, forest, tree, moss, overgrown, ruins, sunlight, dappled sunlight, day, green theme, yellow theme, limited palette
```

差し替えて試す語は 3 組です。

- 大穴を見上げる形: `cave, hole, ceiling, vines`
- 根と崩れた石組み: `stairs, arch, stone wall, fern`
- 水の残る林床: `pond, shallow water, reflection`

陽がオレンジに寄ったら、`yellow theme` を外します。

### 2. ■■の秘跡（龍神を祀った古い聖域。砂と白い石の柱）

```
scenery, no humans, safe, wide shot, horizon, ruins, pillar, column, temple, sand, desert, stone floor, day, yellow theme, white theme, pastel colors, limited palette
```

- 柱の並ぶ回廊: `hallway, arch`
- 天井の崩れた広間: `rubble, debris, ceiling`
- 像と階段: `statue, stairs`

柱の彫りと、柱の根元の菫の結晶の芽は、指示文では場所を決められません。選んだ絵に手で足します。

### 3. 大黒蛇の住処（明るい青緑の洞窟。大蛇の背の菫の結晶が照らす）

```
scenery, no humans, safe, wide shot, cave, rock, crystal, purple gem, glowing, aqua theme, purple theme, pastel colors, limited palette
```

- 地底の水面: `lake, water, reflection`
- 岩壁の連なり: `cliff, stone wall`
- 結晶の天井: `ceiling`

暗くしないよう、`dark` は入れません。大蛇のとぐろは指示文では作れないので、入れるなら先に線画を描いて Line Art に渡します。

### 4. 輝晶の間（結晶の柱が立ち並ぶ大広間。白と淡い菫）

```
scenery, no humans, safe, wide shot, crystal, cave, pillar, backlighting, iridescent, white theme, purple theme, aqua theme, pastel colors, limited palette
```

- 鏡のような床: `reflective floor, reflection`
- 結晶の天井: `ceiling`
- 奥へ続く広間: `hallway, arch`

人型は逆光で黒く抜けるので、真ん中の帯を白く飛ばしすぎないものを選びます。

### 5. 暗き死海（干上がった赤い海床。巨大な肋骨と、白い塩。明るい薔薇色）

```
scenery, no humans, safe, wide shot, horizon, desert, sand, bone, giant, fog, red sky, pink sky, red theme, pink theme, limited palette
```

- 浅く水の残る海床: `shallow water, reflection`
- 岩と白い丘: `rock, cliff, white theme`

肋骨の門は、線画で形を決めます。`skeleton` と `skull` は使いません。オレンジに寄せません。

### 6. 獄雷峡（切り立った谷。明るいコバルトと菫）

```
scenery, no humans, safe, wide shot, cliff, mountain, rock, cloudy sky, fog, blue theme, purple theme, limited palette
```

- 谷底から見上げる形: `mountainous horizon, cloud`
- 両側に迫る岩壁: `stone wall, canyon`

雷は描きません。ゲームが画面の光で出します。`canyon` は件数の少ない語なので、効かなければ外します。

### 7. 果てなき世界（床も天井も無い星空。紺と菫）

```
scenery, no humans, safe, wide shot, starry sky, star (sky), space, nebula, floating rock, floating island, glowing, purple theme, blue theme, limited palette
```

- 星雲を主役に: `galaxy, aurora`
- 岩の欠片を主役に: `debris`

この層だけは、下半分も星にします。星はゲームも 124 個重ねるので、星の少ない候補を選びます。

### 寄生の領域（置き場がまだ無い候補）

寄生の領域の 2 つの層は、いま竜の領域の背景を借りています。絵を置く場所が増えたときのために、指示文だけ置きます。

蔓絡みの獣窟（世界樹の枝と根が貫く獣の巣。灰緑と濁った桃、明るさは中くらい）:

```
scenery, no humans, safe, wide shot, cave, tree, vines, branch, moss, thorns, fog, green theme, pink theme, grey theme, limited palette
```

血染めの腐林（世界樹の幹の根元。暗い桃紫と灰緑）:

```
scenery, no humans, safe, wide shot, forest, tree, vines, fog, glowing, pink theme, purple theme, grey theme, limited palette
```

## 選んだ絵を入れるまで

1. 採った 1 枚を Upscale で拡大し、1920×1080 に切ります。地平線が上から 556 px の近くに来るように切ります。
2. アルファの無い PNG で、Unity リポの `Assets/Resources/Backdrops/layer-<層の番号>.png` に置きます。取り込みの設定（Sprite、sRGB を on、Max Size 2048 など）は `.claude/docs/art_document/asset-intake.md` §13.3 のとおりです。Sprite で取り込まないと、警告なしにコードで描いた遠景に戻ります。
3. 置くと、戦闘・探索・拠点の画面が同じ絵を読みます。絵を入れた層は、床の赤の見え方を測り直します（同 §13.6）。
4. 原本の `.kra` は `%USERPROFILE%\OneDrive\art\backdrops\layer-<層の番号>\` に置き、git に入れません。採った絵ごとに `workflow.json` を保存し、台帳（`Docs/art/asset-ledger.csv`）に 1 行足します。

## 2026-11-03 の手引きから直した点

- **色を今の値に合わせました。** 5 層は暗い深紅から明るい薔薇色に、6 層は夜の紺から明るいコバルトに変わっています（`unity-port/unity-project-kit/Assets/View/Backdrop/Script/BackdropSpec.cs:390-474`）。6 層の `night, dark`、`lightning, electricity, storm` を外し、5 層に `pink sky, pink theme` を足しました。5 層の名前も「暗き死海」に直しました。
- **光の筋と粒の語を外しました。** 手引きの指示文には `sunbeam, light rays, light particles` が入っていました。絵には光の筋と粒を描かない決まり（`asset-intake.md` §13.1）なので外し、負の指示文に足しました。光の筋を絵に入れた候補のほうが良ければ、ゲームが重ねる靄を弱める相談になります。
- **件数の少ない語を外しました。** 2026-11-23 に Danbooru の件数を引き直し、1,000 件に届かない語（`giant snake` 529、`salt` 515、`stalactite` 474、`giant tree` 441 など）は基本の 1 行に入れていません。`canyon`（663）だけ、差し替えの語に残しました。
- **差し替えて試す語を足しました。** 構図の向きを変えたいときの語を、層ごとに 2〜3 組置きました。どれも 1,000 件以上の語です。

## 確かめていないこと

- どの指示文も、実機で 1 度も通していません。1344×768 から Upscale する手順も、背景ではまだ試していません。
- 人型の帯の範囲は、文書によって値が少し違います。`battle-visual-v1.md` §2.4 は横 15〜85%・縦 25〜52%、手引きは横 300〜1620 px・縦 300〜556 px です。この文書は前者で書きました。
- 戦闘と地図で、引く背景の番号が食い違う層があります（#1029）。直るまでは、絵を入れた層が戦闘と地図で別の層に出ることがあります。
