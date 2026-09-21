# 素材の取り込み規約 — 置き場・命名・Unity の設定・台帳

> **Status**: DRAFT v1（2026-09-21）。Issue #93。親は #81。最初の 1 体（#85）を入れる前に要る決めごとです。
> **役割**: 作った絵を Unity に入れるまでの規約を決めます。**立ち絵（キャラクター）だけを対象にします。** 背景・カードの絵・UI の部材は不透明度も圧縮形式も違うので、別の節が要ります（→ §9）。
> **正本**: `.claude/docs/vision/world-v1.md`（世界の正典 v4）／ `.claude/skills/visual-production-pipeline/references/tools-and-prerequisites.md`（道具と前提）
> **読む用**: `docs/reports/2026-09-21-asset-intake-rules.html`
> **隣**: `style-guide.md`（#37。原本の大きさはあちらが正本）／ `card-art-policy.md`（#91）
> **実機**: Unity プロジェクトは `C:/Users/user/Unity/RPG-by-card`、版は 6000.5.5f1、Universal 2D。照合に使った Unity のマニュアルは 6000.0〜6000.6 系です。

---

## 0. 決めたことの一覧

| #   | 決めたこと                                                                                    |
| --- | --------------------------------------------------------------------------------------------- |
| 1   | 原本（`.kra`）は OneDrive に置き、どちらの git にも入れません                                 |
| 2   | 書き出しは **640 × 1536 の PNG**。縮小しません                                                |
| 3   | 絵の正本は **Unity リポ**（`RPG-by-card`）です。このリポには置きません                        |
| 4   | 命名は `chr_<subject>_<category>_<label>.png`、asset id は `chr.<subject>.<category>.<label>` |
| 5   | 取り込み設定は 1 枚の Preset にまとめ、Preset Manager にフォルダ絞りで登録します              |
| 6   | 台帳は Unity リポの `Docs/art/asset-ledger.csv`。**列の正本は本書 §7** です                   |

---

## 1. 原本と書き出し

### 1.1 原本（`.kra`）

置き場は `%USERPROFILE%\OneDrive\art\characters\<subject>\<subject>.kra` です（2026-09-16 の決定どおり OneDrive）。候補の生成物は同じ階層の `_candidates\` に置きます。

**`.kra` はどちらの git にも入れません。** 理由が 2 つあります。Unity リポの `.gitattributes` は PNG から WebP まで 13 種の画像を LFS に載せていますが `.kra` は入っておらず、素のバイナリとして毎回まるごとコミットされます。このリポは LFS を使っていません（PNG が 33 枚、素で入っています）。OneDrive の版管理がバックアップの役をします。

**レイヤーを結合せずに保存します。** 加筆のレイヤーが、人の創作的寄与の一次証拠です（→ §8）。

**`.kra` は生成条件の容れ物でもあります。** プラグインは設定に関係なく、生成のたびに履歴を `.kra` のアノテーション（`ui.json`）へ書きます。種・指示文・sampler・steps・cfg・checkpoint がそこに入ります。ただし履歴の上限は既定 20 MB で、超えると古いものから捨てられます。**採用が決まった時点で、`workflow.json` を台帳の隣へ写します。**

### 1.2 書き出し（PNG）

| 項目         | 値                                         | 理由                                                                                                       |
| ------------ | ------------------------------------------ | ---------------------------------------------------------------------------------------------------------- |
| 大きさ       | **640 × 1536**（原本のまま。縮小しない）   | `style-guide.md` §2 が正本です。4 の倍数なので BC7 が効きます（→ §3.3）                                    |
| 足の位置     | 足の裏をキャンバスの最下行に接地させる     | 基準点を `(0.5, 0)` と書ける条件です                                                                       |
| 左右         | 足の中心をキャンバス幅のちょうど中央に置く | 同上                                                                                                       |
| 向き         | 右向き                                     | 敵は Unity 側で反転します                                                                                  |
| 背景         | 透明（切り抜き済み）                       | —                                                                                                          |
| 書き出しかた | **Split Alpha ▸ Save Merged…**             | 通常の Export は完全透明画素の色を黒で潰します。潰れた PNG は Unity の補間とミップマップで黒い縁を出します |

**「取り込み長辺 1024」は使いません。** `tools-and-prerequisites.md` §9 #8 の決定ですが、640 × 1536 を長辺 1024 に縮めると 427 × 1024 になり、427 が 4 の倍数を割ります。1024 × 1536 から縮めた場合も 683 × 1024 で同じです。**4 の倍数を割ると BC7 と DXT5 が使えません。** 書き出しは原本と同寸にして、縮小は Unity の Max Size に任せず、そもそも起こさないのが一番安全です。

---

## 2. 絵の正本は Unity リポ

**確定。**

| もの                                               | 正本                                                      | 理由                                                                                                                                                                                 |
| -------------------------------------------------- | --------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `.kra` の原本                                      | OneDrive                                                  | どちらの git にも入れません（→ §1.1）                                                                                                                                                |
| PNG の立ち絵                                       | **Unity リポ `RPG-by-card`**                              | PNG が LFS に載っていて、`.meta` の正本も向こうです                                                                                                                                  |
| `.meta`（取り込み設定の実体）                      | **Unity リポ**                                            | `npm run unity:sync` は `.cs` と `.asmdef` しか写しません。同期スクリプトの中にも「Prefabs / scenes / .meta stay in the Unity repo and are never copied from here.」と書いてあります |
| `.preset` と `ProjectSettings/PresetManager.asset` | **Unity リポ**                                            | 両者は GUID で結ばれていて、GUID は `.preset` の `.meta` にあります。こちらから写すと新しい GUID が振られて結び目が切れます                                                          |
| 取り込み設定の**値**                               | **本書 §3**                                               | 人が読む形の正本はここに置きます。Unity 側の `.preset` はその写しです                                                                                                                |
| 取り込みを自動化する C#                            | このリポ（`unity-port/unity-project-kit/Assets/Editor/`） | ただし同期の一覧に `Assets/Editor` の行を足す必要があります（battle レーンの作業）                                                                                                   |

置き場は `Assets/Art/Characters/<subject>/` です。

---

## 3. Unity の取り込み設定

Unity 6 の項目名で書きます。括弧内は `.meta` のキーです。**`.meta` は手で書きません。** Inspector で 1 回設定し、Unity が書いたものをコミットします。

### 3.1 Sprite のパネル

| 項目                   | 値                                                                          | 理由                                                                                                                                                                                                                                                            |
| ---------------------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Texture Type           | Sprite (2D and UI)（`textureType: 8`）                                      | —                                                                                                                                                                                                                                                               |
| Texture Shape          | 2D（`textureShape: 1`）                                                     | —                                                                                                                                                                                                                                                               |
| Sprite Mode            | Single（`spriteMode: 1`）                                                   | 1 ファイル 1 体です。Single のときだけ Pivot が出ます                                                                                                                                                                                                           |
| Pixels Per Unit        | 100（`spritePixelsToUnits: 100`）                                           | **画面での大きさには影響しません。** 戦闘画面は UGUI の `Image` で描くので、大きさを決めるのは RectTransform です。100 にするのは `Canvas.referencePixelsPerUnit` と揃えて `SetNativeSize()` が 1 単位 = 1 画素になるようにするためだけです                     |
| Mesh Type              | **Full Rect**（`spriteMeshType: 0`）                                        | 既定は Tight（1）なので、明示的に変えます。Tight のメッシュはアルファから作られるので、瘴気のにじみや低アルファの縁が切られます。加えて 1 体は Rim / Fill / Flash の 3 枚の `Image` が同じ sprite を共有するので、メッシュが 3 枚で完全に一致する必要があります |
| Pivot                  | **Custom、X = 0.5、Y = 0**（`alignment: 9`、`spritePivot: {x: 0.5, y: 0}`） | Bottom（7）は「テクスチャ矩形の下辺中央」であって「足の中心」ではありません。書き出し規約（§1.2）を守れば一致しますが、破れたときに Custom なら実測値を書けます                                                                                                 |
| Generate Physics Shape | off（`spriteGenerateFallbackPhysicsShape: 0`）                              | 2D の当たり判定を使いません。既定は on で、1 枚ごとに輪郭が保存されます                                                                                                                                                                                         |
| Generate Mipmap        | **on**（`enableMipMap: 1`）                                                 | 既定は off です。1536 の絵を 450〜900 の実画素で出すので 1.7〜3.4 倍の縮小になり、バイリニアだけでは鱗と結晶の縁がちらつきます。容量は +33%（1 枚 0.94 MiB → 1.25 MiB、20 枚で 18.8 → 25 MiB）                                                                  |
| Mipmap Filtering       | Kaiser                                                                      | Box より輪郭が残ります。**要実測**                                                                                                                                                                                                                              |
| Mipmap Limit           | off（全レベルを使う）                                                       | 既定のままだと Quality 設定の Mipmap Limit に引きずられ、全立ち絵が半分の解像度で出る経路が開きます                                                                                                                                                             |
| Filter Mode            | **Trilinear**（`filterMode: 2`）                                            | Bilinear だとミップの段が切り替わる瞬間が見えます。姿勢の `scaleY` と解像度変更で動くので、混ぜる側にします                                                                                                                                                     |
| Wrap Mode              | Clamp（`wrapU: 1`, `wrapV: 1`）                                             | Sprite の既定です。変更ではなく確認です                                                                                                                                                                                                                         |
| Aniso Level            | 1（`aniso: 1`）                                                             | Sprite の既定です。Canvas の板は斜めから見ないので上げる意味がありません                                                                                                                                                                                        |
| sRGB (Color Texture)   | on（`sRGBTexture: 1`）                                                      | Sprite の既定です。色空間の宣言であって品質の摘みではありません                                                                                                                                                                                                 |
| Alpha Source           | Input Texture Alpha（`alphaUsage: 1`）                                      | Sprite の既定です                                                                                                                                                                                                                                               |
| Alpha Is Transparency  | on（`alphaIsTransparency: 1`）                                              | **Sprite の既定は on です。** 見える画素の色を透明側へ広げて、縁の黒ずみを防ぎます。ただし守りの本体は書き出し側（§1.2 の Save Merged…）です                                                                                                                    |
| Read/Write             | off（`isReadable: 0`）                                                      | 既定 off。on にすると CPU 側の複製でメモリが倍になります                                                                                                                                                                                                        |

**Non-Power of Two は Sprite の Inspector に出ません。** 事故で変わる心配はありません（既に `nPOTScale: 0` です）。

### 3.2 Default タブ

| 項目             | 値                                           | 理由                                                                                                                                                                                           |
| ---------------- | -------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Max Size         | 2048（`maxTextureSize: 2048`。既定のまま）   | **1024 にしません。** 1024 にすると、誰かが大きく書き出した 1024 × 1536 が 683 × 1024 に落とされ、4 の倍数を割って圧縮が効かなくなります。保険が事故を作ります。守りは書き出し規約（§1.2）です |
| Resize Algorithm | Mitchell（`resizeAlgorithm: 0`。既定のまま） | 640 × 1536 に Max Size 2048 なら縮小自体が起きないので、この設定は働きません                                                                                                                   |

### 3.3 Windows / standalone の上書き

| 項目                   | 値                                             | 理由                                                                                                                                                                                                                                               |
| ---------------------- | ---------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Override               | **on**（`overridden: 1`）                      | **これを忘れると上書きが丸ごと無効です。** 最も静かに失敗する箇所なので、`.meta` の diff で必ずここを見ます                                                                                                                                        |
| Max Size               | 2048                                           | Default と揃えます                                                                                                                                                                                                                                 |
| Format                 | **RGBA Compressed BC7**（`textureFormat: 25`） | Unity の desktop 向けの案内が RGBA に「BC7 (higher quality, slower to compress) or DXT5 (faster to compress), both at eight bits/pixel」と書いています。同じ 8 bpp なら BC7 のほうがブロックあたりの表現が多く、瘴気の階調と切り抜きの縁に強いです |
| Use Crunch Compression | off（`crunchedCompression: 0`）                | Crunch はディスク容量だけを縮め、実行時のメモリは変わりません。20 枚では意味がなく、取り込みが遅くなります                                                                                                                                         |

**BC7 は両辺が 4 の倍数のときだけ効きます。** 640 = 4 × 160、1536 = 4 × 384 なので通ります。割ったときに Unity が何と表示するかは未確認なので、取り込み後に Inspector 下端の情報行で実際の形式を見ます。

**容量**: 640 × 1536 = 983,040 texel、BC7 は 8 bpp なので 1 枚 960 KiB。ミップマップ込みで 1.25 MiB。20 枚で 25 MiB です。非圧縮 RGBA32 なら 1 枚 3.75 MiB、20 枚で 75 MiB になります。**1 枚だけ縁にブロックが見えたら、その 1 枚を RGBA 32 bit に上げて、理由を台帳の `notes` に書きます。**

### 3.4 Sprite Atlas は使いません

**確定。** 1 枚 0.98 Mpx が 20 枚あっても page は埋まりません。得られるのは draw call の併合だけです。1 体が Rim / Fill / Flash の 3 枚を使うので、プレイヤー 1 + 敵 3 なら 12 枚が 1 枚に減る計算になりますが、20 枚の立ち絵のためにこの複雑さは合いません。

将来 UI の部材で atlas を作るなら、立ち絵は入れないでください。入れる場合の条件は Allow Rotation = off（Canvas の要素が回ります）、Tight Packing = off（トリミングで `DataUtility.GetPadding` が非ゼロになり、体ごとに枠の中の位置がずれます）、Padding = 4、Alpha Dilation = on です。

---

## 4. 命名

```
asset id : chr.<subject>.<category>.<label>
ファイル : Assets/Art/Characters/<subject>/chr_<subject>_<category>_<label>.png
```

`<category>` は `idle` / `act` / `react` の 3 つです。例は `chr.polearm_warped.idle.stand`、`chr.polearm_warped.act.sweep`、`chr.polearm_warped.react.down`。

`<subject>` は英字の小文字とアンダースコアだけにします。Preset Manager のフィルタがパスで絞るので、フォルダ名にも同じ語を使います。

**`<subject>` の付け替えを先に決める必要があります。** いまのロースターの id（`polearm_warped` `shadow_hound` `rusted_revenant` など）は人型の兵と犬を前提にした語で、#128 が竜の系譜へ付け替えます。**絵のファイル名は #128 の結果を待ってから決めます。** 先に描き始める 1 体（#85）は、id を仮に置いて、#128 が決まった時点で `git mv` と `.meta` ごと改名します。`.meta` は一緒に動かせば GUID が保たれるので、参照は切れません。

---

## 5. 設定を自動で当てる

3 つの手があります。**採るのは A です。**

| 手                             | 中身                                                                                                       | 既存のファイルにも効くか          | `.preset` で値を見られるか | 手で直した値を守るか                       |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------- | --------------------------------- | -------------------------- | ------------------------------------------ |
| **A. Preset + Preset Manager** | `.preset` を作り、Project Settings ▸ Preset Manager に Texture Importer の既定として登録し、フィルタで絞る | いいえ（初回取り込みだけ）        | はい                       | はい                                       |
| B. AssetPostprocessor          | `OnPreprocessTexture()` の C# でパスを見て設定する                                                         | 版番号を上げれば効く              | いいえ                     | ガード次第                                 |
| C. フォルダ単位の preset       | Unity のマニュアルにある約 190 行の見本                                                                    | はい（preset を直すと再取り込み） | はい                       | **いいえ**（手で直した値が黙って戻ります） |

**A を採る理由**は、値が `.preset` というレビューできるテキストに残り、手で直した 1 枚を潰さないからです。C は preset を直すと既存にも効きますが、Inspector で直した値が次の再取り込みで戻ります。立ち絵は Pivot を 1 枚ずつ詰める可能性があるので、そこを潰す手は採りません。

### 5.1 手順

1. 正しく設定した PNG を 1 枚選び、Inspector 右上の preset 選択（スライダのアイコン）を押します。
2. Select Preset の窓で **Create New** を押し、`Assets/Art/Characters/CharacterSprite.preset` として保存します。
3. 保存した `.preset` を選び、`m_SpriteSheet.*` / `m_UserData` / `m_AssetBundleName` / `m_AssetBundleVariant` を **Exclude Property** で外します。外さないと全キャラが同じ sprite ID を持ちます。Pivot を 1 枚ずつ詰めるなら `m_Alignment` と `m_SpritePivot.x` / `.y` も外します。
4. Edit ▸ Project Settings ▸ Preset Manager で **Add Default Preset** ▸ Importer ▸ Texture Importer を選び、作った `.preset` を指します。
5. Filter に `glob:"Assets/Art/Characters/**.png"` と書きます。**`glob:` から始めて、模様を二重引用符で囲みます。** 大文字小文字を区別します。
6. 一般の Sprite 用の行があるなら、その **下** に置きます。既定 preset は上から順に当たるので、狭いほうが後に来ないと上書きされます。

### 5.2 落とし穴

- **登録は PNG を入れる前に済ませます。** 既定 preset は初回の取り込みにしか当たりません。後から登録すると、先に入れた分だけ設定が違い、警告も出ません。
- **既存のファイルに当て直せるのは 2 つだけです。** Inspector の importer のコンテキストメニューから **Reset** を選ぶか、C# で `preset.ApplyTo(importer)` してから `AssetDatabase.WriteImportSettingsIfDirty(path)` を呼ぶかです。**Reimport も `ImportAssetOptions.ForceUpdate` も効きません。** 既定 preset を当てる内部の処理が「`.meta` が無いとき」しか動かないためです。
- **`.preset` を消して作り直さないでください。** GUID が変わって Preset Manager の行が黙って外れます。直すときは中身を編集します。
- **3 つのファイルを一緒にコミットします。** `ProjectSettings/PresetManager.asset`、`Assets/Art/Characters/CharacterSprite.preset`、その `.preset.meta` です。
- **フィルタに `Assets/` を付けるかは未確認です。** Unity のマニュアルの例は `glob:"foldername/*.fbx"` の形しか書いていません。PNG を 1 枚落として Inspector を見れば決まります。

---

## 6. `.meta` の見かた

**取り込み設定の実体は `.meta` です。** 規約が守られたかどうかは、PR の `.meta` の diff でしか分かりません。見る行は次の 15 です。

```
textureType: 8            spriteMode: 1             spriteMeshType: 0
alignment: 9              spritePivot: {x: 0.5, y: 0}
spritePixelsToUnits: 100  spriteGenerateFallbackPhysicsShape: 0
enableMipMap: 1           sRGBTexture: 1            alphaIsTransparency: 1
alphaUsage: 1             isReadable: 0
textureSettings: filterMode: 2, aniso: 1, wrapU: 1, wrapV: 1
platformSettings で buildTarget: Standalone の行:
  overridden: 1           textureFormat: 25         crunchedCompression: 0
```

**`overridden: 1` を最優先で見ます。** ここが 0 だと Standalone の上書きが丸ごと効きません。

**キーの並び順と有無は Unity の版で変わります。** 実物 2 件を読んだところ、一方は Unity の宣言順で `serializedVersion: 11`、もう一方はアルファベット順で `mipmapLimitGroupName` などを持っていました。**grep で「無い」を探す形のチェックリストは偽の欠落を出します。** 行の値を見る形にしてください。

6000.x で書かれた Sprite の `.meta` の `serializedVersion` は確認できていません。最初の 1 枚を入れたら、実物を 1 枚この文書に貼ってください。

---

## 7. 台帳

**置き場は Unity リポの `Docs/art/asset-ledger.csv`** です（`tools-and-prerequisites.md` §8 の決定どおり）。**列の正本は本書のこの節です。** 同スキルのテンプレート（30 列）は本書で置き換わるので、別 Issue で差し替えます。

### 7.1 1 枚ごとの行（14 列）

| #   | 列                 | 中身                                                                                           |
| --- | ------------------ | ---------------------------------------------------------------------------------------------- |
| 1   | `asset_id`         | `chr.<subject>.<category>.<label>`                                                             |
| 2   | `file`             | `Assets/Art/Characters/<subject>/chr_....png`                                                  |
| 3   | `sha256`           | その PNG のハッシュ。**空の行は出荷できません**                                                |
| 4   | `date`             | 採用した日                                                                                     |
| 5   | `status`           | `shipped` / `cut` / `superseded:<asset_id>`                                                    |
| 6   | `model`            | `animagine-xl-4.0-opt` / `flux2-klein-4b` / `none`。§7.2 の表と結びます                        |
| 7   | `ai_role`          | `generated` / `edited-ai` / `edited-hand` / `hand`（→ §7.3）                                   |
| 8   | `seed`             | `RandomNoise.noise_seed` の値。手描きなら空                                                    |
| 9   | `recipe`           | `Docs/art/recipes/<asset_id>.workflow.json`。**正本は `.kra` の `ui.json` で、これは写しです** |
| 10  | `source_file`      | `.kra` のパス（OneDrive 内）                                                                   |
| 11  | `export`           | `save-merged` か `export`。透明画素の色が潰れているかの記録です                                |
| 12  | `ref_source`       | `none` か `own:<asset_id>`。これ以外は止まる合図です。購入素材と他作品は参照に入れません       |
| 13  | `human_work`       | 直した箇所と分。例「角と顎を描き直し、9 レイヤーに分割。95 分」                                |
| 14  | `similarity_check` | 日付 + 使った検索 + 結果。例「2026-09-21 lens+tineye none」                                    |

`notes` は 15 列目として置いてもかまいません。候補を選んだ理由と、圧縮を上げた 1 枚の理由を書きます。

**1〜7 は PNG を `Assets/` に入れるときに埋めます。8〜14 は 1 セッション遅れてもかまいません。**

### 7.2 モデルの表（1 回埋めて、モデルを変えたときだけ直す）

```
model_id, weights_file, sha256, license, terms_url, terms_checked_on, notes
```

いま埋まる 2 行です。

| model_id               | weights_file                                                                                                    | license                  | notes                                                                                         |
| ---------------------- | --------------------------------------------------------------------------------------------------------------- | ------------------------ | --------------------------------------------------------------------------------------------- |
| `animagine-xl-4.0-opt` | `animagine-xl-4.0-opt.safetensors`（SHA256 `6327eca98bfb6538dd7a4edce22484a1bbc57a8cff6b11d075d40da1afb847ac`） | CreativeML Open RAIL++-M | opt は量子化版ではなく追加学習版です。base と同じ 6.94 GB の fp16 SDXL で、推奨設定も同じです |
| `flux2-klein-4b`       | —                                                                                                               | Apache-2.0               | **9B 版は商用不可です。入れないでください**                                                   |

### 7.3 `ai_role` と IPTC の対応

| `ai_role`     | IPTC の語                              | どんな絵か                                                   |
| ------------- | -------------------------------------- | ------------------------------------------------------------ |
| `generated`   | `trainedAlgorithmicMedia`              | 生成しただけ。中間ファイル                                   |
| `edited-ai`   | `compositeWithTrainedAlgorithmicMedia` | 生成モデルで部分修正した（Refine / Fill / Expand）           |
| `edited-hand` | `compositeSynthetic`                   | 生成物に人の手描きを重ねた。**納品する立ち絵はほぼこれです** |
| `hand`        | `digitalCreation`                      | 人が最初から描いた                                           |

語彙は `http://cv.iptc.org/newscodes/digitalsourcetype/` です。**Krita の筆で直したものを `compositeWithTrainedAlgorithmicMedia` にしないでください。** 定義が「using a Generative AI model」なので、非生成の手作業は当たりません。

**この値は台帳にだけ持ちます。** Krita AI Diffusion が XMP を書くのはクラウド版のときだけで、ローカルのサーバーでは何も書きません。

### 7.4 列から外したもの

| 外した列                                                              | 理由                                                                                                                                                                                                                                                                                                                                                                                                               |
| --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `attribution` / `commercial_ok`                                       | OpenRAIL++-M は生成物に権利を主張しません（Section III「Licensor claims no rights in the Output You generate using the Model」）。義務が掛かるのは**重み**の再配布で、PNG を同梱するのは再配布ではありません。ただし同じ節の 3 文目「No use of the output can contravene any provision as stated in the License」により、Attachment A の使用制限は生成物の利用にも及びます。竜と亜竜の絵は制限のどれにも触れません |
| `watermark`                                                           | ローカル経路では誰も透かしを書きません                                                                                                                                                                                                                                                                                                                                                                             |
| `made_by` / `tool` / `cost_usd` / `reviewer` / `disclosure`           | 全行で同じ値です。CSV の先頭のコメント行に 1 回書きます                                                                                                                                                                                                                                                                                                                                                            |
| `model_snapshot` / `model_license` / `terms_url` / `terms_checked_on` | 1 枚ごとではなくモデルごとなので、§7.2 の表へ移しました                                                                                                                                                                                                                                                                                                                                                            |
| `kind` / `subject` / `variant`                                        | `asset_id` から読めます                                                                                                                                                                                                                                                                                                                                                                                            |
| `prompt_file`                                                         | `recipe` の `workflow.json` に指示文が入っています                                                                                                                                                                                                                                                                                                                                                                 |

`sha256` と `status` と `export` の 3 列を新しく足しました。30 列から 15 列になります。**30 行埋めるとして 900 セルが 450 セルです。**

---

## 8. 権利の説明

**法的助言ではありません。** 背景資料として一次資料を要約したものです。

**記録は法の要求ではありません。** 米国著作権局が求めるのは登録を申請するときの開示で、日本は著作権の発生に登録も届出も要りません。**台帳は、後から創作的寄与を自分で説明するために用意する任意の記録です。** そのうえで、次の 3 つが台帳の設計に効きます。

- **文化庁「AIと著作権に関するチェックリスト＆ガイダンス」（令和 6 年 7 月 31 日）** は「AI生成物については、その利用に先立って、まずは既存の著作物と類似していないかを確認することが必要です」と書き、手段としてインターネットの画像検索を名指しします。`similarity_check` は「安い異常検知」ではなく、資料が挙げる確認手段そのものです。
- **同じ資料が依拠性に触れます。** 指示文に既存の作品名や人物名を入れると依拠性が認められやすくなる、とあります。**指示文の全文を台帳から辿れる状態にしておくこと自体が、固有名詞を使っていない証拠になります。** `recipe` 列がその役です。
- **文化庁「AIと著作権に関する考え方について」（令和 6 年 3 月 15 日）** は「人間が、AI 生成物に、創作的表現といえる加筆・修正を加えた部分については、通常、著作物性が認められる」としたうえで、「もっとも、それ以外の部分についての著作物性には影響しない」と続けます。守られるのは加筆した部分だけです。`human_work` と `.kra` のレイヤーがその証拠になります。

**Steam の開示はストアページに出ます。** Steamworks の Content Survey は、ゲームに同梱してプレイヤーが目にする AI 生成物を Pre-Generated として申告させ、実装を「describe that implementation in detail」と求め、「your game will be consistent with your marketing materials」と約束させたうえで審査で照合します。**内部メモではなく公開文面なので、日本語版も要ります。** 草案を置きます。

> 本作の敵と主人公のイラストの一部は、ローカルで動作する画像生成モデル（Animagine XL 4.0 opt、FLUX.2 klein 4B）で下地を作り、開発者が加筆・パーツ分け・アニメーションを行ったものです。UI・カードのテキスト・ゲームの仕組みは開発者が設計しています。ゲームの実行中に AI がコンテンツを生成することはありません。

**C2PA（Content Credentials）は入れません**（2026-09-21 判断）。理由は 3 つです。このパイプラインに書き手がいません。PNG の `caBX` チャンクは「not safe to copy」なので、Krita で保存し直すと落ちます。署名に証明書が要ります。

---

## 9. 置いた仮定

- **本書は立ち絵だけを扱います。** 背景は不透明なので BC7（8 bpp）ではなく DXT1/BC1（4 bpp）が正解になり、答えが変わります。カードの絵と UI の部材も別です。
- **ミップマップを on にしました。** 1536 の絵を最大 900 の実画素で出す縮小に、バイリニアだけでは足りないと見たためです。**1 体目で on / off のスクリーンショットを見比べて確かめます。** 見て違いが無ければ off に戻し、容量を 25 MiB から 18.8 MiB に下げます。
- **Max Size を既定の 2048 のままにしました。** 1024 を保険に置くと、大きく書き出した絵を 4 の倍数でない寸法へ落として圧縮を壊します。
- **台帳を Unity リポに置いたままにしました。** 既存の 4 箇所（スキル 2 箇所、`briefs/` 2 本）が同じパスを指しているためです。こちらへ移すなら 4 箇所を同時に直す必要があり、どれも本 Issue の書き込み範囲の外です。

## 10. 未確認のこと

- Preset Manager の Filter が `Assets/` から始まるか。PNG を 1 枚落とせば決まります。
- 6000.x が書く Sprite の `.meta` の `serializedVersion`。実物を 1 枚貼ってもらう必要があります。
- BC7 が両辺 4 の倍数を要求するときに Unity 6 が何と表示するか。一次情報に辿り着けませんでした。制約そのものはブロック圧縮の性質から確かです。
- ミップマップの on / off の見え方（→ §9）。
- `EditorSettings.asset` の `m_SpritePackerMode: 5` が何を指すか。atlas を使わないので実害はありませんが、未確認です。
- 台帳 1 行を埋める実時間。`card-art-policy.md` の「1 枚 20〜40 分」は未実測です。15 列でも重いなら、`ai_role` と `similarity_check` を後追いの別ファイルへ逃がす余地があります。
- 禁止モデル（Illustrious / NoobAI 系、MAT）が混ざっていないことを、台帳でどう示すか。checkpoint 名は `.kra` と `workflow.json` に残りますが、Krita 側でどのボタンを押したかは残りません。いまは `notes` 頼みです。

## 11. 次に判断が要る点

- **`<subject>` の付け替え時期。** #128 がロースター 19 体を竜の系譜へ付け替えます。#85 の 1 体目を仮の id で描き始めるか、#128 を待つかを決める必要があります。
- **自動検査を置くか。** 規約を書いても、`.meta` を人が見なければ崩れます。`-batchmode -quit -executeMethod` で TextureImporter を読んで差分を吐く Editor のスクリプトが要ります。実行は Unity 側なので、このレーンの静的検証には載りません。
- **`tools-and-prerequisites.md` の 2 箇所。** §8 の 30 列と §9 #8 の「取り込み長辺 1024・BC7」は本書で置き換わります。同ファイルは design が書く正本なので、別 Issue で直します。
