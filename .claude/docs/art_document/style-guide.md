# 絵柄の規約 — AI っぽさを消す指示文と加筆

> **Status**: DRAFT v1（2026-09-21）。Issue #37。**指示文の実測はまだです**（→ §10）。生成サーバーが止まっていて、このセッションで画像を 1 枚も出していません。本書の数値は一次資料（モデルカード・プラグインのソース・Danbooru のタグ API・Unity のマニュアル）とリポジトリの実ファイルから取ったもので、出力を見て決めたものではありません。
> **役割**: Animagine XL 4.0 opt で敵の立ち絵を出すときの、原本の大きさ・指示文・使わない語・加筆の工程を決めます。絵柄そのもの（顔つき・塗り・意匠）は決めません。それは #83 の担当です。
> **正本**: `.claude/docs/vision/world-v1.md`（世界の正典 v4。持ち主は main）／ `.claude/skills/visual-production-pipeline/references/tools-and-prerequisites.md`（道具と前提）／ `docs/reports/2026-09-19-krita-ai-setup-guide.html`（手順書）
> **読む用**: `docs/reports/2026-09-21-ai-look-and-prompt-template.html`
> **隣**: `.claude/docs/art_document/asset-intake.md`（#93 取り込み規約）／ `card-art-policy.md`（#91）／ `briefs/`（体ごとの仕様カード）

---

## 0. この文書が決めること

3 つだけです。**原本の大きさ**（§2）、**貼るだけの指示文**（§4・§5）、**加筆で直す箇所**（§6）。

決めないことが 3 つあります。絵柄（#83）、体ごとの姿（`briefs/`）、Unity の取り込み設定（#93 = `asset-intake.md`）です。

---

## 1. 先に直す 3 つ（実機で 5 分）

この 3 つを直さないと、以降の指示文も計測も意味を持ちません。

| #   | いまの状態                                                | 直す先                                        | 理由                                                                                                                                                                |
| --- | --------------------------------------------------------- | --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | style の `negative_prompt` が `..., blood, nswf` で終わる | `..., blood, nsfw`                            | `nswf` は綴り誤りです。Animagine の rating タグは `safe` / `sensitive` / `nsfw` / `explicit` の 4 つで、`nswf` は学習語彙に無く、意図した抑制が丸ごと効いていません |
| 2   | style ファイルの名前が `anime-illustrious.json`           | 中身は `card-battle Animagine` なので改名する | Illustrious 系は商用制限で禁止です。ファイル名が禁止モデルを示唆したままだと、いつか取り違えます                                                                    |
| 3   | 原本が 1024 × 1536                                        | **640 × 1536**（→ §2）                        | 1024 × 1536 は上限に掛かって内部で縮小され、さらに学習済みの超解像モデルで戻されます                                                                                |

**Prompt Translation は確認だけで足ります。** `settings.json` の `prompt_translation` は 2026-09-20 16:41 時点で空でした。空なら `ETN_Translate` は挿さりません。手順書 `2026-09-19-krita-ai-setup-guide.html` 2 章が「`ja` にする」と書いていますが、これは古い記述です。

---

## 2. 原本は 640 × 1536 にする

**確定**（本書の決定。#93 の書き出し規約と対です）

いまの 1024 × 1536 は、次の 4 つの条件のうち 3 つを落とします。

| 条件            | 中身                                                                                          | 1024×1536           | 832×1216          | **640×1536**             |
| --------------- | --------------------------------------------------------------------------------------------- | ------------------- | ----------------- | ------------------------ |
| A. 推奨バケット | Animagine XL 4.0 が縦長として挙げる 4 つ（896×1152 / 832×1216 / 768×1344 / 640×1536）のどれか | 該当なし            | 該当              | **該当**                 |
| B. 画素の上限   | Krita の上限 1 MP。判定は `1,000,000 × 1.05 = 1,050,000` 画素を超えるか                       | 1,572,864。超える   | 1,011,712。通る   | **983,040。通る**        |
| C. 16 の倍数    | 拡散前の丸め（`multiple_of(16)`）で寸法が動かないこと                                         | 動く（816×1232 に） | 動かない          | **動かない**             |
| D. 枠の縦横比   | 戦闘画面の Figure 枠は 210 × 450（比 0.4667）で `preserveAspect = true`。これ以下でないと余る | 0.667。高さの 70%   | 0.684。同じく余る | **0.4167。高さいっぱい** |

**B を落とすと、絵が 2 回作り直されます。** 1024 × 1536 は 816 × 1232 へ縮められて生成され、そのあと学習済みの超解像モデル（`OmniSR_X2_DIV2K`）を通してから 1024 × 1536 へ戻されます。超解像モデルは自分の質感を全画素に残すので、これ自体が AI っぽさの一因です。**640 × 1536 にすると、この経路ごと消えます。**

**D の出典**: `unity-port/unity-project-kit/Assets/View/Depiction/Editor/DepictionPrefabBuilder.cs:218`（`Root("Figure", new Vector2(210f, 450f), new Vector2(0.5f, 0f))`）と `:222`（`view.body.preserveAspect = true`）。

**「140 × 300 px」は古い値です。** `briefs/` の 2 本が使っている 140 × 300 は旧 `ArenaView.cs:35-36` の数字で、いまの正本は Depiction の 210 × 450 です。しかも 210 と 450 は画素ではなく **Canvas の参照単位** です。`BattleScreenView.cs:352-354` が `ScaleWithScreenSize` / 参照 1920×1080 / `matchWidthOrHeight = 0.5` を設定しているので、実画素は 1080p で 450、1440p で 600、4K で 900 になります。**線の太さと縁の粗さは 4K を基準に決めます。**

書き出しは **原本のまま 640 × 1536** で出します。縮小しません。長辺 1024 へ縮めると幅が 683 になり、4 の倍数でなくなって BC7 圧縮が効かなくなります（理由は `asset-intake.md` §3）。

---

## 3. AI っぽさの 10 項目

「生成で消す」は指示文か設定で減らせるもの、「加筆で消す」は人が Krita で直すものです。**どの項目も実機で確かめていません**（→ §10）。

| #   | 見えかた                       | 生成で消す                                                                                                                                                                                                                               | 加筆で消す                                                                                                                                                                 |
| --- | ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | 肌と鱗のつるつるした照り       | 負の指示に `shiny skin`（Danbooru 158,131 件）。ただし黒鱗の反射まで消す恐れがあるので、実機で 1 回見る                                                                                                                                  | 乗算レイヤーで影を置き、生成の階調を消す                                                                                                                                   |
| 2   | 影の境目が決まらない           | 正の指示に `anime coloring`（56,889 件。定義が「hard-edged highlights and shadows, as opposed to the soft, smooth shadows used in fully rendered art」）と `high contrast`（4,847 件）                                                   | Levels / Curves で明度の段を作る                                                                                                                                           |
| 3   | 装飾が左右対称                 | **できません。** `symmetry` は 2,339 件しかなく、負に入れても弱いです                                                                                                                                                                    | 片側を壊す（片方の肩当て、折れた角、武器を片側に）。対にしたい角と結晶列は、良い側を複製・反転して変形で合わせる                                                           |
| 4   | 線幅が一定                     | **できません。** `thick_outlines` 655 件 / `thick_lineart` 1,881 件では条件付けになりません                                                                                                                                              | シルエット線と要所 3〜5 本だけを筆圧つきの筆で引き直す                                                                                                                     |
| 5   | 背景の描き込み過多             | 正に `simple background, grey background`、負に `scenery`（48,290 件）                                                                                                                                                                   | 不要です。背景は切り抜きます                                                                                                                                               |
| 6   | 手・指・爪の破綻               | **できません。** モデルカードが限界として「May struggle with complex anatomical details, particularly hand poses and finger counting」と明記しています。負の `missing_finger` は 86 件・`extra_digits` は 277 件で、実体はほぼありません | 手の周りを選択して Refine 0.35。28 段のうち 10 段、開始のノイズ σ = 0.932 で、指を描き直せる強さです                                                                       |
| 7   | 四足・翼・とぐろの構造         | **できません。** `quadruped` は Danbooru に 0 件で、四足を指すタグがありません。翼 2 対も分布の外です                                                                                                                                    | 先に線画を描いて Line Art コントロールに入れ、生成は色と陰影だけにする                                                                                                     |
| 8   | 彩度が高い・暖色が混じる       | 寄せるだけならできます。`limited palette`（26,553）/ `muted colors`（9,226）/ `purple theme`（13,771）/ `blue theme`（39,969）。縛れはしません                                                                                           | Cross-channel Color Adjustment（Saturation を Hue で駆動）で H = 15〜65 と H ≥ 345 の彩度を落とす                                                                          |
| 9   | 切り抜きの縁に灰色が残る       | **できません。**                                                                                                                                                                                                                         | Split Alpha ▸ Alpha into Mask → Select Opaque → Shrink 1 px → Write as Alpha → **Split Alpha ▸ Save Merged…** で書き出す（通常の Export は完全透明画素の色を黒で潰します） |
| 10  | 超解像モデルの質感が全面に乗る | **原本を 640 × 1536 にすれば経路ごと消えます**（→ §2）                                                                                                                                                                                   | —                                                                                                                                                                          |

**光の向きは指示文では決まりません。** 方向を指すタグは `backlighting`（45,817）/ `sidelighting`（13,276）/ `underlighting`（1,302）/ `overlighting`（528）の 4 つだけで、左右の別もありません。**敵の立ち絵は中立光で生成し、層ごとの光は Krita で載せます。** 例外は、光源が体そのものにある 2 体（セルクの背の結晶、ガルドの雷の縁）だけです。

**琥珀 `#e8a84c` は敵に使いません。** 正典 §3 規則 4 が「暖色はプレイヤーの側にしか置かない」と決めています。指示文にも絵にも入れず、混ざったら §3-8 の手当てで落とします。

---

## 4. 使う指示文

### 4.1 スロットの並び

Animagine XL 4.0 のモデルカードは並びを `1girl/1boy/1other, character name, from which series, rating, everything else in any order and end with quality enhancement` と定めています。固有の作品名と人物名は使わないので、2 番目と 3 番目は空けます。

```
[1] 数と種  [2] 格付け  [3] 画角  [4] 体  [5] 持ち物・構え  [6] 材質  [7] 塗りと色  [8] 背景
```

- **[1] は段で変わります。** 竜人は `1other, solo`（`1other` は 129,699 件で、モデルカードが 1 番目に名指しする語）。竜・亜竜・小竜は `no humans, animal focus`。
- **[2] は `safe` を必ず書きます。** style の `style_prompt` に rating タグが入っていないので、ここで書かないと空のままです。
- **品質タグは書きません。** `style_prompt` が `{prompt}, masterpiece, high score, great score, absurdres` で自動的に付けます。二重に書くと他の語が薄まります。
- **`facing right` は書きません。** Danbooru に 0 件で、条件付けになりません。画角は `from side, profile` で押さえ、向きは §4.2 のとおり Krita で決めます。

### 4.2 向きと立ち位置

**確定。画面では、敵は右に立って左を向きます。プレイヤーは左に立って右を向きます。** 2 体は必ず向き合います。

|            | 画面での立ち位置 | 画面での向き | Unity の反転                    | **渡す PNG の向き** |
| ---------- | ---------------- | ------------ | ------------------------------- | ------------------- |
| プレイヤー | 左（x = −260）   | 右を向く     | なし（`localScale.x = +1`）     | **右向き**          |
| 敵         | 右（x = +400）   | **左を向く** | **あり**（`localScale.x = −1`） | **右向き**          |

**渡す PNG はどちらも右向きです。** 敵だけ Unity が左右反転して左向きにします。ここを取り違えて敵を左向きで描くと、**二重に反転して画面では右（背中側）を向きます**。

出典は `Assets/View/Depiction/Editor/DepictionPrefabBuilder.cs:492`（プレイヤーを x = −260 に置く）、`:497`（敵を x = +400 に置く）、`:501`（`player.enemyFigure.body.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // face the player`）です。旧 `ArenaView.cs:29` も同じ扱いです。

**候補が左向きで出たら、採用前に Krita で水平反転して右向きに直します。** `from side, profile` は横顔を作りますが左右は決めないので、候補の半分前後が左向きで出ます。

**左右で意味が変わる意匠は、反転したあとの絵で決めます。** 武器を持つ手、片側だけの肩当て、折れた角がこれに当たります。敵の絵を反転して「武器がプレイヤー側（画面左）に来ているか」を確かめてから、加筆を始めます。

**左向きの PNG を渡す運用に変えたいときは、先に `DepictionPrefabBuilder.cs:501` の反転を外す必要があります。** このファイルは battle レーンの持ち物なので、Issue を立てて回してください。

### 4.3 4 つの例（竜の系譜）

いずれも 1 行で貼れます。**タグではない素の英語**は各例の下に挙げます。素の英語はタグより弱く、無視されることがあります。

**竜人（眷属・二足・長柄）**

```
1other, solo, safe, full body, standing, from side, profile, monster, dragon horns, slit pupils, scales, black scales, claws, dragon tail, holding polearm, polearm held upright, shoulder armor, vambraces, torn clothes, anime coloring, high contrast, limited palette, muted colors, blue theme, simple background, grey background
```

素の英語は `polearm held upright` の 1 つです。`holding_polearm` は薙刀・槍・戟の別名を吸収するタグなので、和の武器名を書かずに長柄を指せます。**`dragon boy` は既定では使いません**。角と尻尾を持つ萌えの人物像を強く引き、眷属の像から外れます（→ §8 計測 E）。

**小竜（眷属・四足・群れ）**

```
no humans, animal focus, safe, full body, from side, profile, small dragon, dragon, monster, creature, taur, scales, black scales, dragon horns, claws, standing on four legs, anime coloring, high contrast, limited palette, muted colors, purple theme, dark, simple background, grey background
```

素の英語は `standing on four legs` です。`small dragon` は 1,216 件と弱いので `dragon`（37,981）と `monster`（43,388）で支えます。`taur`（8,168）は四足の体を指す唯一の実在タグです。**四足の関節の向きは生成では決まりません。** 線画コントロールが要ります（→ §6 工程 1）。

**亜竜 大黒蛇 セルク**

```
no humans, animal focus, safe, giant snake, snake, monster, huge coiled serpent body, raised head, black scales, scales, dragon horns, crystal, glowing crystal, purple gemstone, crystals growing along the spine, glowing, light particles, backlighting, anime coloring, high contrast, limited palette, purple theme, dark, simple background, grey background
```

素の英語は `huge coiled serpent body` / `raised head` / `crystals growing along the spine` の 3 つです。`eastern dragon`（6,782）は髭と鹿角と雲を引くので使いません。正典の竜は欧州型です。

**竜 獄竜 ガルド**

```
no humans, animal focus, solo, safe, full body, from side, western dragon, dragon, monster, black scales, scales, dragon horns, horns swept backwards, long narrow head, heavy jaw, dragon wings, spread wings, claws, electricity, black lightning, backlighting, spot color, anime coloring, high contrast, limited palette, purple theme, dark, simple background, grey background
```

素の英語は `horns swept backwards` / `long narrow head` / `heavy jaw` / `black lightning` の 4 つです。`electricity`（17,804）は `lightning`（6,272）より 2.8 倍強いタグです。**四足に二対の翼は分布の外にあります。** 線画コントロール無しでは、翼竜（前脚が翼）か、二対目がにじんだ絵になります。

### 4.4 負の指示文

75 トークンが天井です。CLIP は 77 トークンで区切り、先頭と末尾を差し引いた 75 が 1 かたまりです。それを超えた分は SDXL の pooled 埋め込みに入りません。いまの文字列は実測 52 トークン（開始・終了を含めて 54）なので、足せるのは 20 トークンほどです。

**全被写体で使う文字列**

```
lowres, bad anatomy, bad hands, text, error, missing finger, extra digits, fewer digits, cropped, worst quality, low quality, low score, bad score, average score, signature, watermark, username, blurry, blood, nsfw, looking at viewer, upper body, out of frame, shiny skin, sparkle, scenery, white background, 1girl
```

**竜・亜竜・小竜だけ、末尾に足す**: `, dragon girl, human`

足す語の理由は 3 つです。`looking at viewer` は語彙で最大の引き（3,038,888 件）で、入れないと顔が正面を向きます。`upper body` と `out of frame` は足が切れるのを止めます（基準点が足元中央なので、足が無いと使えません）。`1girl` と `dragon girl` は人型への流れを止めます。

トークン数は、単語 1 つで 1、読点 1 つで 1 と数えます。厳密に数えるなら、同梱の ComfyUI の `comfy/sd1_tokenizer` を使います。

### 4.5 候補出しと、選んだ組み合わせの固定

ワイルドカード `{a|b|c}` は **生成回数のたびに 1 回** 選び直されます（バッチの中は同じ文面です）。いまの `batch_size` は 1 なので、1 枚 1 組み合わせです。

落とし穴が 3 つあります。**選択肢が 1 つの `{plate armor}` は波括弧ごと文字として CLIP に渡ります**（正規表現が縦棒を必須にしているため）。固定するときは波括弧を消します。**同じ種は同じ組み合わせを選びます** ので、種を固定したままでは探索になりません。**前の群の選択肢を増減させると、後ろの群の選び方も全部変わります**。

選んだ組み合わせの固定は、履歴の画像にカーソルを乗せて展開後の文面をコピーし、ワイルドカードの行に上書きします。そのあと種を自由にして 1 枚出し、その組み合わせが種に依存していないことを確かめます。

---

## 5. 使わない語

| 群    | 語                                                                                                                                                        | 理由                                                                                                                                                                                       |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 権利  | 既存の作品名・人物名・作者名（Danbooru の type 1 / 3 / 4 の語すべて）                                                                                     | 文化庁のチェックリストが、固有名詞を指示文に入れると依拠性が認められやすくなると述べています。プラグインの語彙ファイル 81,030 行のうち 56,243 行（69%）がこの 3 種で、補完候補に出てきます |
| 正典  | `zombie` `undead` `skeleton` `corpse` `ghost` `wraith` `rotten`                                                                                           | 正典 §2 が亡者・亡霊を敵として置かないと決めています                                                                                                                                       |
| 正典  | `soldier` `knight` `samurai` `corrupted soldier` `warped human`                                                                                           | 同じく「人が歪んだ兵」を置きません                                                                                                                                                         |
| 正典  | `naginata` `katana` `yari` `khakkhara` `japanese dark fantasy`                                                                                            | 舞台は欧州型の竜の住処です。武器は `holding polearm` `crossbow` `dual wielding` で書きます                                                                                                 |
| 正典  | `magic` `magic circle` `spell` `rune` `wizard` `witch`                                                                                                    | この世界に魔術はありません（正典 §7.3。cards レーンの判断待ちの提案ですが、絵では守ります）                                                                                                |
| 正典  | `blood` `gore`                                                                                                                                            | 流血を描きません。負の指示にも入れています                                                                                                                                                 |
| 正典  | 暖色を呼ぶ語（`golden hour` `firelight` `torch` `warm light` `sunlight`）を敵の指示文に                                                                   | 琥珀はプレイヤー専用です                                                                                                                                                                   |
| AI 臭 | `intricate` `highly detailed` `ultra detailed` `8k` `octane render` `trending on artstation` `cinematic lighting` `hyperrealistic` `god rays` `rim light` | Danbooru の語彙に無く、条件付けになりません。`god rays` の実在タグは `sunbeam`、`rim light` の実在タグは `backlighting` です                                                               |
| AI 臭 | `desaturated`                                                                                                                                             | 0 件で、別名もありません。`muted colors`（9,226）か `limited palette`（26,553）を使います                                                                                                  |
| AI 臭 | `quadruped` `dragonkin` `dragonewt` `scalie` `anthro` `cel shading` `facing right`                                                                        | いずれも 0 件です                                                                                                                                                                          |
| 機械  | 色の 16 進表記（`#e8a84c` など）                                                                                                                          | プラグインのコメント除去は 6 桁の 16 進を **わざと残す** ので、そのまま CLIP に渡って雑音になります                                                                                        |

**タグの確かめかた**: `https://danbooru.donmai.us/tags.json?search[name_comma]=<語>` で `post_count` を見ます。**0 件のときは別名を疑います。** `https://danbooru.donmai.us/tag_aliases.json?search[antecedent_name]=<語>&search[status]=active` で統合先を引き、その件数を見ます（`muted_color` は 0 件ですが、`muted_colors` 9,226 件への別名です）。目安は 1,000 件以上です。

---

## 6. 加筆で直す箇所

生成では消せないもの（§3 の「できません」）を、この順で直します。**時間はすべて未実測です。** 1 体目で測って埋めます（#82）。

| 順  | 工程     | やること                                                                                                                                                                            | 見込み                                 |
| --- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------- |
| 1   | 構造     | 四足・翼・とぐろは、先に線画を描いて Line Art コントロールに入れる。生成は色と陰影だけ                                                                                              | 竜人は不要。小竜・亜竜・竜で 20〜40 分 |
| 2   | 手・爪   | 手の周りを投げ縄で選択し、Refine 0.35 で描き直す。**Custom Generation を使うときは「Seamless」のチェックを外す**（入っているとライセンス未確認の Fooocus Inpaint が読み込まれます） | 10〜20 分                              |
| 3   | 対の装飾 | 良い側を複製 → レイヤー ▸ 変形 ▸ 水平方向に鏡像 → 自由変形で遠近を合わせる                                                                                                          | 5〜10 分                               |
| 4   | 線       | シルエット線と要所 3〜5 本（顎・翼の付け根・武器の柄）を、Size センサーを筆圧にした筆で引き直す                                                                                     | 15〜30 分                              |
| 5   | 色       | Palettize（Colorspace = Lab、ディザ無し）で外れ量を見る。直すのは Cross-channel Color Adjustment（Saturation ← Hue）で H = 15〜65 と H ≥ 345 の彩度を落とす                         | 5〜15 分                               |
| 6   | 縁       | Split Alpha ▸ Alpha into Mask → Levels → Select Opaque → Shrink 1 px → Write as Alpha → **Split Alpha ▸ Save Merged…**                                                              | 5〜10 分                               |
| 7   | 確認     | Window ▸ New View をもう 1 枚開き、出荷する最大解像度での見えに合わせて縮小表示する。黒の塗り潰し + Inherit Alpha で影絵にし、View ▸ Mirror View（M）で形を疑う                     | 5〜15 分                               |

**合計 45〜100 分（未実測）。** `card-art-policy.md` の「1 枚 20〜40 分」はカードの絵の数字で、立ち絵には当てはまりません。

**使ってよい AI の操作**: Refine（強度 100% 未満）、Custom Generation の Fill = None / Neutral / Blur / Border（Seamless を外す）。選択範囲に描く Fill・Expand・Add Content・Replace Background は、**強度 80% 以下** で使います。SDXL では強度が 80% を超えると Fooocus Inpaint を読むためです（`workflow.py` の `detect_inpaint`。2026-09-23 に #145 で確認）。

**使わない操作**: Remove Content と Custom Generation の Fill = Inpaint（どちらも MAT を読み、CC BY-NC 4.0 です）。Custom Generation の **Seamless**（既定で入っています。SDXL では Fooocus Inpaint を読み、ライセンス条文が未確認です）。強度 80% を超えて選択範囲に描く操作（同じく Fooocus Inpaint を読みます）。既定の Upscale は 2026-09-23 に WTFPL と確かめたので、使ってかまいません（`tools-and-prerequisites.md` §13）。

**人の加筆が要る理由は法にもあります。** 米国著作権局の 2023 年の指針（88 FR 16190）は、指示文だけの出力を登録できないとし、人が改変した部分は保護されるとします。文化庁「AIと著作権に関する考え方について」（令和 6 年 3 月 15 日）は創作意図と創作的寄与で判断するとし、**「人間が、AI 生成物に、創作的表現といえる加筆・修正を加えた部分については、通常、著作物性が認められる」** としたうえで **「もっとも、それ以外の部分についての著作物性には影響しない」** と続けます。守られるのは加筆した部分だけです。これは設計書の背景資料で、法的助言ではありません。

---

## 7. 摘みの表

| 摘み               | いまの値              | 何に効くか                                                                                                                              | 触るか                                                                     |
| ------------------ | --------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `cfg_scale`        | 5.0                   | 指示への追随、局所のコントラスト、彩度。**手・対称・線幅には効きません**                                                                | 触りません。モデルカードの推奨は 4〜7（5 推奨）                            |
| `sampler_steps`    | 28                    | ほぼ何も。しかも ancestral なので、段数を変えると同じ種でも別の絵になります                                                             | 触りません。推奨は 25〜28（28 推奨）                                       |
| sampler preset     | Alternative - Euler A | 表面の粒。`euler_ancestral` + `normal` の組で、モデルカードの推奨どおり                                                                 | 触りません。`lora` を持つプリセット（Hyper / Lightning / LCM）は使いません |
| `rescale_cfg`      | 0.7                   | **何も効きません。** Krita は v 予測 / zero-terminal-SNR のときだけ RescaleCFG を挿します。Animagine は ε 予測なので graph に現れません | 触りません。計測の変数にもしません                                         |
| `clip_skip`        | 0                     | 0 は「ノードを挿さない」です。モデルカードは何も言っていません                                                                          | 触りません。2 にする指南は SD1.5 の話です                                  |
| Refine の強度      | —                     | 段数の開始位置です。0.25 → 7 段（σ 0.625）、0.35 → 10 段（σ 0.932）、0.50 → 14 段（σ 1.519）                                            | 手の描き直しは 0.35 から                                                   |
| Performance の上限 | 1 MP                  | 1.05 MP を超えると縮小されます                                                                                                          | 1.0 のまま。原本を 640×1536 にして収めます                                 |

**摘みで直せる AI 臭はほとんどありません。** 効くのは指示文（§4）と加筆（§6）と原本の大きさ（§2）の 3 つです。

UI のどこにあるかも書いておきます。`cfg_scale` と `sampler_steps` と sampler preset はスタイル編集画面にあります。`clip_skip` と VAE と v 予測は「Checkpoint configuration (advanced)」の中です。`rescale_cfg` は **UI に無く**、`%APPDATA%\krita\ai_diffusion\styles\*.json` を直接編集して Krita を再起動するしかありません。種と強度は生成パネル側です。

---

## 8. 実測の手順

**このセクションはまだ空です。** 人が機械の前で回して埋めます。GPU の時間は合計 5 分ほど、見比べを入れて 30 分です。1 枚あたり約 21 秒（うち拡散が約 10.3 秒、残りはモデルの読み込みと復号）。

前提の確認は GPU が要りません。Prompt Translation が空であること、Performance の上限が 1 であること、原本を 640 × 1536 で新規作成すること、種を固定すること、竜人の例文（§4.3）を使うことの 5 つです。

| 計測               | 変える値                                                                                                            | 枚数 | 見る点                                                                            | 結果 |
| ------------------ | ------------------------------------------------------------------------------------------------------------------- | ---- | --------------------------------------------------------------------------------- | ---- |
| A. CFG             | 3.5 / 5.0 / 6.5 / 8.0                                                                                               | 4    | 彩度、暖色の混入、黒鱗が潰れるか、`simple background` が効いているか              | 未   |
| B. 段の入れ替え    | 既定 / `euler_ancestral`+`karras` / `dpmpp_2m`+`normal`（利用者側の `presets/samplers.json` に足して 1 変数にする） | 3    | 平坦部の粒、線幅の差、角の左右差                                                  | 未   |
| C. 再現性          | 同じ種で 2 枚                                                                                                       | 2    | Krita で差分合成して真っ黒になるか                                                | 未   |
| D. Refine          | 0.25 / 0.35 / 0.50                                                                                                  | 3    | 艶が増えるか、手が直るか                                                          | 未   |
| E. 竜人の register | `dragon boy` を入れる / 入れない                                                                                    | 2    | 萌えの人物像に寄るか、眷属に見えるか                                              | 未   |
| F. 書き出しの検算  | —                                                                                                                   | 0    | `logs/workflow.json` の `EmptyLatentImage` が 640×1536 で、超解像ノードが無いこと | 未   |

記録する値は `logs/workflow.json` から写します。`RandomNoise.noise_seed`、`BasicScheduler.scheduler` `.steps` `.denoise`、`KSamplerSelect.sampler_name`、`CFGGuider.cfg`、`EmptyLatentImage.width` `.height`、`CheckpointLoaderSimple.ckpt_name`、`CLIPTextEncode` の正負の文面です。強度を下げたときは `SplitSigmas.step` も要ります。**`KSamplerAdvanced` は本経路に現れません。**

貼るだけの graph を `prompts/dragonkin-polearm.workflow.json` に置きました。640 × 1536、28 段、CFG 5.0、`euler_ancestral`、種 12345 で、§4.3 の竜人の例文が入っています。これは **手順の雛形であって、実行の記録ではありません**。

---

## 9. 置いた仮定

- **原本を 640 × 1536 に決めました。** 推奨バケット・上限・16 の倍数・枠の縦横比の 4 条件を同時に満たす唯一の値だからです。幅 640 は翼を広げた竜には狭いので、ガルドだけ別扱いが要る見込みです（→ §11）。
- **敵は中立光で生成し、層の光は後から載せると決めました。** 光の向きを指すタグが 4 つしかなく、左右の別も無いためです。
- **`dragon boy` を既定から外しました。** 件数は多い（10,696）のですが、萌えの人物像を引く語です。計測 E で決め直します。
- **負の指示文に 10 語を足しました。** 75 トークンの天井に収まる見込みですが、数えていません。
- **タグの件数は Danbooru の公開 API（2026-09-21 取得）です。** Animagine の学習データは「various sources」としか公開されていないので、これは語の重みの代理であって、学習分布そのものではありません。日々動きます。

## 10. 未確認のこと

- **指示文を 1 つも実機で試していません。** §4 の 4 例はどれも生成していません。Issue #37 の完了条件「実測した指示文の型が 1 つ以上ある（seed と `workflow.json` 付き）」は、**まだ満たしていません**。
- 加筆の分数（§6）はすべて見積もりです。#82 で 1 体通して測ります。
- 負の指示文の実トークン数。`shiny skin` を負に入れると黒鱗の照りまで消えるかどうか。
- Krita の日本語 UI での項目名。本書は英語のメニュー名で書いています。
- 同じ体の差分 3 枚（待機 / 行動 / 被弾）をどう揃えるか。種でもプロンプトでも揃いません。Reference コントロールか Flux 2 Klein 4B の編集機能が要りますが、どちらも未調査です。
- 自動で落ちてくるモデルのライセンスは、#145 で実機を棚卸ししました（`tools-and-prerequisites.md` §13）。残る未確認は Fooocus Inpaint の条文、PiDiNet の但し書き、Nova Anime XL の元の条項、noob_openpose と noob-ipa の表示の 4 つで、どれも使わない側に置いています。

## 11. 次に判断が要る点

- **セルクとガルドを立ち絵にするか。** 正典 §8 はセルクを「壁からうねり出た」「動かない」とし、ガルドを「翼の先が両側の崖に食い込む構図」と定めています。どちらも 210 × 450 の切り抜きに収まりません。battle レーン（View の持ち主）と詰める必要があります。
- **出荷する最大解像度。** 線の太さと縁の許容はここから決まります。1080p なら原本のシルエット線 12〜16 px でちょうどですが、4K では太すぎます。
- **`briefs/` の 2 本の数字。** 140 × 300、原本 1024 × 1536、1 枚 30〜35 秒はいずれも古い値です。#85 の着手前に直します。
