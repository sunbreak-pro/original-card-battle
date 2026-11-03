# 素材の取り込み規約 — 置き場・命名・Unity の設定・台帳

> **Status**: DRAFT v1（2026-09-21）。Issue #93。親は #81。最初の 1 体（#85）を入れる前に要る決めごとです。2026-10-06 に、絵のファイル名の id をロースターの内部 ID に決めました（#327）。#128 を待つ記述は外しました。2026-10-10 に、Preset Manager のフィルタが `Assets/` から始まる形で当たると確かめた結果を §5.2 と §10 に入れました（#85。登録は RPG-by-card#10）。同じ日に、上級の槍の竜兵 2 枚の幅の例外（800 と 900）を §0・§1.2・§3.3 に足しました（#287）。2026-10-17 に、UI の部材と書体の取り込みと、書体のライセンスの記録を §12 に足しました（#92）。2026-10-19 に、数字の 600 に使う `IBMPlexMono-SemiBold.ttf` を §12.2 の表に足しました（#610）。2026-11-03 に、画面での高さが 400 px を超える大型の敵の書き出しを §0・§1.2 に足し、立ち絵の Max Size を 2048 から 4096 に上げました（#840）。同じ日に、背景の絵の取り込みの決まりを §13 に足しました。置き場は旧い戦闘画面と同じ `Resources/Backdrops/layer-<n>` です（#859）。
> **役割**: 作った絵を Unity に入れるまでの規約を決めます。§1〜§11 は**立ち絵（キャラクター）だけを対象にします。** 背景・カードの絵・UI の部材は不透明度も圧縮形式も違うので、節を分けます。UI の部材と書体は §12、背景は §13 です。カードの絵は `card-art-policy.md` が元になる文書です。
> **正本**: `.claude/docs/vision/world-v1.md`（世界の正典 v4）／ `.claude/skills/visual-production-pipeline/references/tools-and-prerequisites.md`（道具と前提）
> **読む用**: `docs/reports/2026-09-21-asset-intake-rules.html`
> **隣**: `style-guide.md`（#37。原本の大きさはあちらが正本）／ `card-art-policy.md`（#91）
> **実機**: Unity プロジェクトは `C:/Users/user/Unity/RPG-by-card`、版は 6000.5.5f1、Universal 2D。照合に使った Unity のマニュアルは 6000.0〜6000.6 系です。

---

## 0. 決めたことの一覧

| # | 決めたこと |
| --- | --- |
| 1 | 原本（`.kra`）は OneDrive に置き、どちらの git にも入れません |
| 2 | 書き出しは **640 × 1536 の PNG**。縮小しません。例外は上級の槍の竜兵 2 枚（800 × 1536 と 900 × 1536、→ §1.2）と、画面での高さが 400 px を超える大型の敵（高さも幅も 4096 まで、→ §1.2） |
| 3 | 絵の正本は **Unity リポ**（`RPG-by-card`）です。このリポには置きません |
| 4 | 命名は `chr_<subject>_<category>_<label>.png`、asset id は `chr.<subject>.<category>.<label>` |
| 5 | 取り込み設定は 1 枚の Preset にまとめ、Preset Manager にフォルダ絞りで登録します |
| 6 | 台帳は Unity リポの `Docs/art/asset-ledger.csv`。**列の正本は本書 §7** です |
| 7 | 背景の絵は 1 層に 1 枚、**1920 × 1080 の不透明な PNG** です。Unity の `Resources/Backdrops/layer-<n>`（n は 1〜7）に置きます（→ §13） |
| 8 | 背景は **RGB Compressed DXT1（BC1、4 bpp）**、ミップマップなし、Max Size 2048 で取り込みます（→ §13.3） |

---

## 1. 原本と書き出し

### 1.1 原本（`.kra`）

置き場は `%USERPROFILE%\OneDrive\art\characters\<subject>\<subject>.kra` です（2026-09-16 の決定どおり OneDrive）。候補の生成物は同じ階層の `_candidates\` に置きます。

**`.kra` はどちらの git にも入れません。** 理由が 2 つあります。Unity リポの `.gitattributes` は PNG から WebP まで 13 種の画像を LFS に載せていますが `.kra` は入っておらず、素のバイナリとして毎回まるごとコミットされます。このリポは LFS を使っていません（PNG が 33 枚、素で入っています）。OneDrive の版管理がバックアップの役をします。

**レイヤーを結合せずに保存します。** 加筆のレイヤーが、人の創作的寄与の一次証拠です（→ §8）。

**`.kra` は生成条件の容れ物でもあります。** プラグインは設定に関係なく、生成のたびに履歴を `.kra` のアノテーション（`ui.json`）へ書きます。種・指示文・sampler・steps・cfg・checkpoint がそこに入ります。ただし履歴の上限は既定 20 MB で、超えると古いものから捨てられます。**採用が決まった時点で、`workflow.json` を台帳の隣へ写します。**

### 1.2 書き出し（PNG）

| 項目 | 値 | 理由 |
| --- | --- | --- |
| 大きさ | **640 × 1536**（原本のまま。縮小しない）。晶槍の竜兵は 800 × 1536、不退の槍竜は 900 × 1536 のまま書き出す。大型の敵は、原本の寸法（高さも幅も 4096 まで）のまま書き出す | `style-guide.md` §2 が正本で、幅の例外と高さの例外もそこにあります。どの幅も 4 の倍数なので BC7 が効きます（→ §3.3） |
| 足の位置 | 足の裏をキャンバスの最下行に接地させる | 基準点を `(0.5, 0)` と書ける条件です |
| 左右 | 足の中心をキャンバス幅のちょうど中央に置く | 同上 |
| 向き | 画面で向く向き（敵は左向き、プレイヤーは右向き） | Unity では反転しません（2026-10-03 こうだいさん決定、#85。反転をやめる作業は #288） |
| 背景 | 透明（切り抜き済み） | — |
| 書き出しかた | **Split Alpha ▸ Save Merged…** | 通常の Export は完全透明画素の色を黒で潰します。潰れた PNG は Unity の補間とミップマップで黒い縁を出します |

**「取り込み長辺 1024」は使いません。** `tools-and-prerequisites.md` §9 #8 の決定ですが、640 × 1536 を長辺 1024 に縮めると 427 × 1024 になり、427 が 4 の倍数を割ります。1024 × 1536 から縮めた場合も 683 × 1024 で同じです。**4 の倍数を割ると BC7 と DXT5 が使えません。** 書き出しは原本と同寸にして、縮小は Unity の Max Size に任せず、そもそも起こさないのが一番安全です。

**大型の敵**（2026-11-03、#840）は、原本の高さが 1536 を超え、4096 までになります。幅も 4096 までです。どちらかが 4096 を超えると、Max Size で縮められて 4 の倍数を割るおそれがあります。大きさの決め方と作り方は `style-guide.md` §2 の「高さの例外」にあります。書き出しは、これまでと同じく原本のまま、縮小しません。両辺とも 4 の倍数にします。台帳の `notes` に、画面での高さと原本の寸法を書きます。

---

## 2. 絵の正本は Unity リポ

**確定。**

| もの | 正本 | 理由 |
| --- | --- | --- |
| `.kra` の原本 | OneDrive | どちらの git にも入れません（→ §1.1） |
| PNG の立ち絵 | **Unity リポ `RPG-by-card`** | PNG が LFS に載っていて、`.meta` の正本も向こうです |
| `.meta`（取り込み設定の実体） | **Unity リポ** | `npm run unity:sync` は `.cs` と `.asmdef` しか写しません。同期スクリプトの中にも「Prefabs / scenes / .meta stay in the Unity repo and are never copied from here.」と書いてあります |
| `.preset` と `ProjectSettings/PresetManager.asset` | **Unity リポ** | 両者は GUID で結ばれていて、GUID は `.preset` の `.meta` にあります。こちらから写すと新しい GUID が振られて結び目が切れます |
| 取り込み設定の**値** | **本書 §3** | 人が読む形の正本はここに置きます。Unity 側の `.preset` はその写しです |
| 取り込みを自動化する C# | このリポ（`unity-port/unity-project-kit/Assets/Editor/`） | ただし同期の一覧に `Assets/Editor` の行を足す必要があります（battle レーンの作業） |

置き場は `Assets/Art/Characters/<subject>/` です。

---

## 3. Unity の取り込み設定

Unity 6 の項目名で書きます。括弧内は `.meta` のキーです。**`.meta` は手で書きません。** Inspector で 1 回設定し、Unity が書いたものをコミットします。

### 3.1 Sprite のパネル

| 項目 | 値 | 理由 |
| --- | --- | --- |
| Texture Type | Sprite (2D and UI)（`textureType: 8`） | — |
| Texture Shape | 2D（`textureShape: 1`） | — |
| Sprite Mode | Single（`spriteMode: 1`） | 1 ファイル 1 体です。Single のときだけ Pivot が出ます |
| Pixels Per Unit | 100（`spritePixelsToUnits: 100`） | **画面での大きさには影響しません。** 戦闘画面は UGUI の `Image` で描くので、大きさを決めるのは RectTransform です。100 にするのは `Canvas.referencePixelsPerUnit` と揃えて `SetNativeSize()` が 1 単位 = 1 画素になるようにするためだけです |
| Mesh Type | **Full Rect**（`spriteMeshType: 0`） | 既定は Tight（1）なので、明示的に変えます。Tight のメッシュはアルファから作られるので、瘴気のにじみや低アルファの縁が切られます。加えて 1 体は Rim / Fill / Flash の 3 枚の `Image` が同じ sprite を共有するので、メッシュが 3 枚で完全に一致する必要があります |
| Pivot | **Custom、X = 0.5、Y = 0**（`alignment: 9`、`spritePivot: {x: 0.5, y: 0}`） | Bottom（7）は「テクスチャ矩形の下辺中央」であって「足の中心」ではありません。書き出し規約（§1.2）を守れば一致しますが、破れたときに Custom なら実測値を書けます |
| Generate Physics Shape | off（`spriteGenerateFallbackPhysicsShape: 0`） | 2D の当たり判定を使いません。既定は on で、1 枚ごとに輪郭が保存されます |
| Generate Mipmap | **on**（`enableMipMap: 1`） | 既定は off です。1536 の絵を 450〜900 の実画素で出すので 1.7〜3.4 倍の縮小になり、バイリニアだけでは鱗と結晶の縁がちらつきます。容量は +33%（1 枚 0.94 MiB → 1.25 MiB、20 枚で 18.8 → 25 MiB） |
| Mipmap Filtering | Kaiser | Box より輪郭が残ります。**要実測** |
| Mipmap Limit | off（全レベルを使う） | 既定のままだと Quality 設定の Mipmap Limit に引きずられ、全立ち絵が半分の解像度で出る経路が開きます |
| Filter Mode | **Trilinear**（`filterMode: 2`） | Bilinear だとミップの段が切り替わる瞬間が見えます。姿勢の `scaleY` と解像度変更で動くので、混ぜる側にします |
| Wrap Mode | Clamp（`wrapU: 1`, `wrapV: 1`） | Sprite の既定です。変更ではなく確認です |
| Aniso Level | 1（`aniso: 1`） | Sprite の既定です。Canvas の板は斜めから見ないので上げる意味がありません |
| sRGB (Color Texture) | on（`sRGBTexture: 1`） | Sprite の既定です。色空間の宣言であって品質の摘みではありません |
| Alpha Source | Input Texture Alpha（`alphaUsage: 1`） | Sprite の既定です |
| Alpha Is Transparency | on（`alphaIsTransparency: 1`） | **Sprite の既定は on です。** 見える画素の色を透明側へ広げて、縁の黒ずみを防ぎます。ただし守りの本体は書き出し側（§1.2 の Save Merged…）です |
| Read/Write | off（`isReadable: 0`） | 既定 off。on にすると CPU 側の複製でメモリが倍になります |

**Non-Power of Two は Sprite の Inspector に出ません。** 事故で変わる心配はありません（既に `nPOTScale: 0` です）。

### 3.2 Default タブ

| 項目 | 値 | 理由 |
| --- | --- | --- |
| Max Size | **4096**（`maxTextureSize: 4096`）。2026-11-03 に 2048 から上げました（#840） | 大型の敵の原本は高さが 4096 まであり、2048 のままだと縮められて 4 の倍数を割り、圧縮が効かなくなります。640 × 1536 の絵は 2048 でも 4096 でも縮まないので、立ち絵の Preset を 1 つのまま 4096 にします。大型の敵だけ別の Preset にすると、当て忘れた 1 枚が黙って縮みます。**1024 にもしません。** 1024 にすると、誰かが大きく書き出した 1024 × 1536 が 683 × 1024 に落とされ、同じく圧縮が効かなくなります。保険が事故を作ります。守りは書き出し規約（§1.2）です |
| Resize Algorithm | Mitchell（`resizeAlgorithm: 0`。既定のまま） | 書き出し規約どおりの絵なら縮小自体が起きないので、この設定は働きません |

### 3.3 Windows / standalone の上書き

| 項目 | 値 | 理由 |
| --- | --- | --- |
| Override | **on**（`overridden: 1`） | **これを忘れると上書きが丸ごと無効です。** 最も静かに失敗する箇所なので、`.meta` の diff で必ずここを見ます |
| Max Size | 4096 | Default と揃えます |
| Format | **RGBA Compressed BC7**（`textureFormat: 25`） | Unity の desktop 向けの案内が RGBA に「BC7 (higher quality, slower to compress) or DXT5 (faster to compress), both at eight bits/pixel」と書いています。同じ 8 bpp なら BC7 のほうがブロックあたりの表現が多く、瘴気の階調と切り抜きの縁に強いです |
| Use Crunch Compression | off（`crunchedCompression: 0`） | Crunch はディスク容量だけを縮め、実行時のメモリは変わりません。20 枚では意味がなく、取り込みが遅くなります |

**BC7 は両辺が 4 の倍数のときだけ効きます。** 640 = 4 × 160、1536 = 4 × 384 なので通ります。割ったときに Unity が何と表示するかは未確認なので、取り込み後に Inspector 下端の情報行で実際の形式を見ます。

**容量**: 640 × 1536 = 983,040 texel、BC7 は 8 bpp なので 1 枚 960 KiB。ミップマップ込みで 1.25 MiB。20 枚で 25 MiB です。幅の例外の 2 枚は、800 × 1536 が 1 枚 1,200 KiB、900 × 1536 が 1 枚 1,350 KiB です（ミップマップ抜き）。非圧縮 RGBA32 なら 1 枚 3.75 MiB、20 枚で 75 MiB になります。大型の敵は、高さ 2720 × 幅 1280 の絵で 1 枚約 3.3 MiB（ミップマップ込みで約 4.4 MiB）、上限の 4096 × 4096 で 1 枚 16 MiB（同じく約 21.3 MiB）です。**1 枚だけ縁にブロックが見えたら、その 1 枚を RGBA 32 bit に上げて、理由を台帳の `notes` に書きます。**

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

**`<subject>` はロースターの内部 ID をそのまま使い、改名しません。** `enemy_roster_v4.md` §1.8 が、竜の系譜への付け替えで名前だけを替え、内部 ID は替えないと決めたためです（#128 の決定）。内部 ID を保てば、実装・台本・パリティ生成はそのまま動きます。仕様カードの `briefs/polearm_warped.md` `polearm_crystal.md` `polearm_unyielding.md` と、それに沿って描く絵は、この id のまま置きます。人型の兵や犬を思わせる ID（`shadow_hound` `rusted_revenant` など）も、ファイル名はそのままです。竜の系譜の名前になるのは表示名だけです。ロースターが ID をまとめて付け替えると決めたとき（同 §1.8 の「#70 / #51 の後」）に、絵の改名もあわせて決めます。

---

## 5. 設定を自動で当てる

3 つの手があります。**採るのは A です。**

| 手 | 中身 | 既存のファイルにも効くか | `.preset` で値を見られるか | 手で直した値を守るか |
| --- | --- | --- | --- | --- |
| **A. Preset + Preset Manager** | `.preset` を作り、Project Settings ▸ Preset Manager に Texture Importer の既定として登録し、フィルタで絞る | いいえ（初回取り込みだけ） | はい | はい |
| B. AssetPostprocessor | `OnPreprocessTexture()` の C# でパスを見て設定する | 版番号を上げれば効く | いいえ | ガード次第 |
| C. フォルダ単位の preset | Unity のマニュアルにある約 190 行の見本 | はい（preset を直すと再取り込み） | はい | **いいえ**（手で直した値が黙って戻ります） |

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
- **フィルタは `Assets/` から書きます。** Unity のマニュアルの例は `glob:"foldername/*.fbx"` の形しか書いていませんが、`glob:"Assets/Art/Characters/**.png"` で当たることを確かめました（2026-10-05、RPG-by-card#10）。試しの PNG を 1 枚落とし、Inspector で §3 の値が入るのを見ています。

---

## 6. `.meta` の見かた

**取り込み設定の実体は `.meta` です。** 規約が守られたかどうかは、PR の `.meta` の diff でしか分かりません。見る行は次のとおりです。

```
textureType: 8            spriteMode: 1             spriteMeshType: 0
alignment: 9              spritePivot: {x: 0.5, y: 0}
spritePixelsToUnits: 100  spriteGenerateFallbackPhysicsShape: 0
enableMipMap: 1           sRGBTexture: 1            alphaIsTransparency: 1
alphaUsage: 1             isReadable: 0
textureSettings: filterMode: 2, aniso: 1, wrapU: 1, wrapV: 1
platformSettings で buildTarget: DefaultTexturePlatform の行:
  maxTextureSize: 4096
platformSettings で buildTarget: Standalone の行:
  overridden: 1           maxTextureSize: 4096
  textureFormat: 25       crunchedCompression: 0
```

**`overridden: 1` を最優先で見ます。** ここが 0 だと Standalone の上書きが丸ごと効きません。

**キーの並び順と有無は Unity の版で変わります。** 実物 2 件を読んだところ、一方は Unity の宣言順で `serializedVersion: 11`、もう一方はアルファベット順で `mipmapLimitGroupName` などを持っていました。**grep で「無い」を探す形のチェックリストは偽の欠落を出します。** 行の値を見る形にしてください。

6000.x で書かれた Sprite の `.meta` の `serializedVersion` は確認できていません。最初の 1 枚を入れたら、実物を 1 枚この文書に貼ってください。

---

## 7. 台帳

**置き場は Unity リポの `Docs/art/asset-ledger.csv`** です（`tools-and-prerequisites.md` §8 の決定どおり）。**列の正本は本書のこの節です。** 同スキルのテンプレート（30 列）は本書で置き換わるので、別 Issue で差し替えます。

### 7.1 1 枚ごとの行（14 列）

| # | 列 | 中身 |
| --- | --- | --- |
| 1 | `asset_id` | `chr.<subject>.<category>.<label>` |
| 2 | `file` | `Assets/Art/Characters/<subject>/chr_....png` |
| 3 | `sha256` | その PNG のハッシュ。**空の行は出荷できません** |
| 4 | `date` | 採用した日 |
| 5 | `status` | `shipped` / `cut` / `superseded:<asset_id>` |
| 6 | `model` | `animagine-xl-4.0-opt` / `flux2-klein-4b` / `none`。§7.2 の表と結びます |
| 7 | `ai_role` | `generated` / `edited-ai` / `edited-hand` / `hand`（→ §7.3） |
| 8 | `seed` | `RandomNoise.noise_seed` の値。手描きなら空 |
| 9 | `recipe` | `Docs/art/recipes/<asset_id>.workflow.json`。**正本は `.kra` の `ui.json` で、これは写しです** |
| 10 | `source_file` | `.kra` のパス（OneDrive 内） |
| 11 | `export` | `save-merged` か `export`。透明画素の色が潰れているかの記録です |
| 12 | `ref_source` | `none` か `own:<asset_id>`。これ以外は止まる合図です。購入素材と他作品は参照に入れません |
| 13 | `human_work` | 直した箇所と分。例「角と顎を描き直し、9 レイヤーに分割。95 分」 |
| 14 | `similarity_check` | 日付 + 使った検索 + 結果。例「2026-09-21 lens+tineye none」 |

`notes` は 15 列目として置いてもかまいません。候補を選んだ理由と、圧縮を上げた 1 枚の理由を書きます。

**1〜7 は PNG を `Assets/` に入れるときに埋めます。8〜14 は 1 セッション遅れてもかまいません。**

### 7.2 モデルの表（1 回埋めて、モデルを変えたときだけ直す）

```
model_id, weights_file, sha256, license, terms_url, terms_checked_on, notes
```

いま埋まる 2 行です。

| model_id | weights_file | license | notes |
| --- | --- | --- | --- |
| `animagine-xl-4.0-opt` | `animagine-xl-4.0-opt.safetensors`（SHA256 `6327eca98bfb6538dd7a4edce22484a1bbc57a8cff6b11d075d40da1afb847ac`） | CreativeML Open RAIL++-M | opt は量子化版ではなく追加学習版です。base と同じ 6.94 GB の fp16 SDXL で、推奨設定も同じです |
| `flux2-klein-4b` | — | Apache-2.0 | **9B 版は商用不可です。入れないでください** |

### 7.3 `ai_role` と IPTC の対応

| `ai_role` | IPTC の語 | どんな絵か |
| --- | --- | --- |
| `generated` | `trainedAlgorithmicMedia` | 生成しただけ。中間ファイル |
| `edited-ai` | `compositeWithTrainedAlgorithmicMedia` | 生成モデルで部分修正した（Refine / Fill / Expand） |
| `edited-hand` | `compositeSynthetic` | 生成物に人の手描きを重ねた。**納品する立ち絵はほぼこれです** |
| `hand` | `digitalCreation` | 人が最初から描いた |

語彙は `http://cv.iptc.org/newscodes/digitalsourcetype/` です。**Krita の筆で直したものを `compositeWithTrainedAlgorithmicMedia` にしないでください。** 定義が「using a Generative AI model」なので、非生成の手作業は当たりません。

**この値は台帳にだけ持ちます。** Krita AI Diffusion が XMP を書くのはクラウド版のときだけで、ローカルのサーバーでは何も書きません。

### 7.4 列から外したもの

| 外した列 | 理由 |
| --- | --- |
| `attribution` / `commercial_ok` | OpenRAIL++-M は生成物に権利を主張しません（Section III「Licensor claims no rights in the Output You generate using the Model」）。義務が掛かるのは**重み**の再配布で、PNG を同梱するのは再配布ではありません。ただし同じ節の 3 文目「No use of the output can contravene any provision as stated in the License」により、Attachment A の使用制限は生成物の利用にも及びます。竜と亜竜の絵は制限のどれにも触れません |
| `watermark` | ローカル経路では誰も透かしを書きません |
| `made_by` / `tool` / `cost_usd` / `reviewer` / `disclosure` | 全行で同じ値です。CSV の先頭のコメント行に 1 回書きます |
| `model_snapshot` / `model_license` / `terms_url` / `terms_checked_on` | 1 枚ごとではなくモデルごとなので、§7.2 の表へ移しました |
| `kind` / `subject` / `variant` | `asset_id` から読めます |
| `prompt_file` | `recipe` の `workflow.json` に指示文が入っています |

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

- **§1〜§11 は立ち絵だけを扱います。** 背景は不透明なので BC7（8 bpp）ではなく DXT1/BC1（4 bpp）が正解になり、答えが変わります。背景は §13、UI の部材は §12 で決めました。カードの絵は別です。
- **背景のミップマップを off にしました**（§13.3）。絵は基準の画面でほぼ 1 対 1 に出るためです。実画面で確かめていません。
- **立ち絵のミップマップを on にしました。** 1536 の絵を最大 900 の実画素で出す縮小に、バイリニアだけでは足りないと見たためです。**1 体目で on / off のスクリーンショットを見比べて確かめます。** 見て違いが無ければ off に戻し、容量を 25 MiB から 18.8 MiB に下げます。
- **Max Size を 4096 にしました**（2026-11-03、#840。それまでは既定の 2048）。大型の敵の原本が高さ 4096 まであるためです。#840 の決定は「大型の絵は Max Size 4096」で、全ての立ち絵を 4096 にするかまでは言っていません。大型の敵だけ別の Preset にすると当て忘れの事故が起きるので、立ち絵の Preset 1 つを 4096 にしました。640 × 1536 の絵の取り込みは変わりません。1024 を保険に置くと、大きく書き出した絵を 4 の倍数でない寸法へ落として圧縮を壊します。
- **台帳を Unity リポに置いたままにしました。** 既存の 4 箇所（スキル 2 箇所、`briefs/` 2 本）が同じパスを指しているためです。こちらへ移すなら 4 箇所を同時に直す必要があり、どれも本 Issue の書き込み範囲の外です。

## 10. 未確認のこと

- 6000.x が書く Sprite の `.meta` の `serializedVersion`。実物を 1 枚貼ってもらう必要があります。
- BC7 が両辺 4 の倍数を要求するときに Unity 6 が何と表示するか。一次情報に辿り着けませんでした。制約そのものはブロック圧縮の性質から確かです。
- ミップマップの on / off の見え方（→ §9）。
- Unity 6 が高さ 4096 の Sprite を BC7 で取り込むときの時間と、実行時のメモリ（#840）。
- Unity リポの `CharacterSprite.preset` の `maxTextureSize` を 4096 に直したか。2048 のままだと、大型の敵の絵だけが黙って縮みます。`.meta` の diff で確かめます（§6）。
- DXT1 が、背景の空のグラデーションに帯を出さないか（§13.3）。1 枚目を入れたら、実画面で空の暗い所を拡大して確かめます。
- 背景の PNG を置く `Resources` のフォルダが `Assets/Resources/Backdrops/` でよいか。Unity リポの既存の `Resources` は `Assets/View/Resources`（書体）だけです。どちらでも、読み込みの名前 `Backdrops/layer-<n>` は変わりません。
- `EditorSettings.asset` の `m_SpritePackerMode: 5` が何を指すか。atlas を使わないので実害はありませんが、未確認です。
- 台帳 1 行を埋める実時間。`card-art-policy.md` の「1 枚 20〜40 分」は未実測です。15 列でも重いなら、`ai_role` と `similarity_check` を後追いの別ファイルへ逃がす余地があります。
- 禁止モデル（Illustrious / NoobAI 系、MAT）が混ざっていないことを、台帳でどう示すか。checkpoint 名は `.kra` と `workflow.json` に残りますが、Krita 側でどのボタンを押したかは残りません。いまは `notes` 頼みです。

## 11. 次に判断が要る点

- **自動検査を置くか。** 規約を書いても、`.meta` を人が見なければ崩れます。`-batchmode -quit -executeMethod` で TextureImporter を読んで差分を吐く Editor のスクリプトが要ります。実行は Unity 側なので、このレーンの静的検証には載りません。
- **`tools-and-prerequisites.md` の 2 箇所。** §8 の 30 列と §9 #8 の「取り込み長辺 1024・BC7」は本書で置き換わります。同ファイルは design が書く正本なので、別 Issue で直します。

## 12. UI の部材と書体（#92）

立ち絵の規約（§0〜§11）とは別に、UI の部材と書体だけをここで決めます。何を絵にするかは `battle-visual-v1.md` §11 が元になる文書です。

### 12.1 UI の部材

| 項目 | 値 |
| --- | --- |
| 原本 | 透明の地に白 1 色の形、128×128 の PNG。色はコードが付けるので、色を塗らない |
| 置き場 | Unity リポの `Assets/Resources/UiArt/icon-<id>.png`。id は `UiArtCatalog.cs` の小文字のケバブ（例: `bleed`、`adapt-1`） |
| Texture Type | Sprite (2D and UI) |
| 圧縮 | なし（RGBA32）。23 枚で約 1.5 MiB なので、縁のにじみを避けるほうを取ります |
| Mip Map | off（表示は 18〜34 px で、拡大縮小の幅が小さいため） |
| Filter | Bilinear |

### 12.2 書体のライセンスの記録

3 つの書体はどれも SIL Open Font License 1.1 で、ゲームへの同梱と商用の配布ができます。書体のファイル単体を売ることと、Reserved Font Name を改変版に使うことだけが禁止です。ゲームの配布物には、各書体の `OFL-<family>.txt` を同梱します。

| 書体 | ファイル | 著作権表示 | 元の版 |
| --- | --- | --- | --- |
| Shippori Mincho | `ShipporiMincho-Bold.ttf` | Copyright 2021 The Shippori Mincho Project Authors | fontdasu/ShipporiMincho `63431fe` |
| Zen Kaku Gothic New | `ZenKakuGothicNew-Regular.ttf` / `ZenKakuGothicNew-Bold.ttf` | Copyright 2022 The Zen Kaku Gothic Project Authors | googlefonts/zen-kakugothic `2705757` |
| IBM Plex Mono | `IBMPlexMono-SemiBold.ttf` / `IBMPlexMono-Bold.ttf` | Copyright © 2017 IBM Corp. with Reserved Font Name "Plex" | googlefonts/plex `9ab3b5b` |

- 取得元は github.com/google/fonts の `ofl/<family>/` で、コミット `47d54ca1ebb14fd864695f1d39dc734f39017c33`（2026-10-17）に固定しています。ファイルごとの SHA-256 は `unity-port/tools/fetch-fonts.mjs` が持ち、取得のたびに照合します。
- ファイルは約 13 MB あるので git に入れず、`npm run fonts:fetch` で `unity-port/unity-project-kit/Assets/View/Resources/Fonts/` に取得し、`npm run unity:sync` で Unity の `Assets/View/Resources/Fonts/` へ写します。
- Unity の Font の取り込みは既定（Dynamic）のままにします。TextMeshPro の SDF アトラスは使いません（`battle_ui_ux_v2.md` §8 で残り 20% に置いた作業です）。
- 版を上げるときは、`fetch-fonts.mjs` のコミットとハッシュと、この表を同じコミットで直します。

---

## 13. 背景の絵（#859）

背景の絵の取り込みをここで決めます。何を描くかは `battle-visual-v1.md` §2.4 が元になる文書です。絵を読んで画面に出すコード（受け口）は、旧い戦闘画面の `Assets/View/Backdrop/BackdropLayers.cs` にあります。試遊版の Unity 版の受け口は #858（battle）で作ります。この節はそれと対になります。

### 13.1 描く絵

| 項目 | 値 | 理由 |
| --- | --- | --- |
| 枚数 | 1 層に 1 枚、7 層で 7 枚 | 受け口が層の番号（1〜7）ごとに 1 枚を探します |
| 大きさ | **1920 × 1080**。縮小しません | 基準の画面の大きさです。絵は画面いっぱいに引き伸ばして出すので、16:9 でないと歪みます。両辺が 4 の倍数（1920 = 4 × 480、1080 = 4 × 270）なので DXT1 が効きます |
| 不透明度 | **不透明**。アルファチャンネルを持たない PNG | 空のグラデーションを全面で覆って敷く絵だからです。アルファを持たせると、圧縮が 8 bpp に上がります |
| 書き出しかた | 画像を統合し、アルファチャンネルを保存しない PNG で書き出す | 透明の画素が無いので、立ち絵の Split Alpha（§1.2）は要りません |
| 描く範囲 | 遠景と地面だけ描きます。靄の光の筋と粒は描きません | 靄と粒は、コードが絵の上に重ねて動かします。絵に焼き込むと二重になります |

重なる順は、下から 空のグラデーション、絵、靄、粒 です（`BackdropLayers.cs`）。絵を入れた層では、絵がコードで描く遠景（`BackdropScenery.cs`）の代わりになります。遠景は絵が入るまでの代わりなので、絵には遠景に当たる木・柱・岩・結晶の連なりも含めて描きます。

描くときは、次の 3 点に気をつけます。

- **試遊版では、y 556 より下を暗い地面が覆います**（`View/Trial/Battle/BattleBackdrop.cs` の `EarthTop`）。地面は ground の色の 80%（地平線）から 92%（下端）です。探索の画面では、その上に層ごとの暗い幕（`battle-visual-v1.md` §2.5）も重なります。見せたい形は y 556 より上に置きます。
- **人型の立つ帯（x 15〜85%、y 25〜52%）の後ろは、明るさの差を小さくします。** 濃い形は、画面の端か上に置きます。コードの遠景と同じ決まりです（`battle-visual-v1.md` §2.4）。
- **層の色は `battle-visual-v1.md` §2.4 の値に合わせます。** 絵が層の明るさを変えると、床の赤のコントラスト（同 §2.3）が崩れます。

### 13.2 置き場と名前

```
ファイル : Assets/Resources/Backdrops/layer-<n>.png   （n = 1〜7）
asset id : bg.layer-<n>
読み込み : Resources.Load<Sprite>("Backdrops/layer-" + n)
```

名前は、コードの定数 `BackdropLayers.ArtPath`（`"Backdrops/layer-"`）に合わせて固定します。読み込みの名前には拡張子を付けません。立ち絵の `chr_` の接頭辞は使いません。旧い画面と試遊版で読み込みの口を 1 つにするため、試遊版も同じ名前で読みます（#858）。

原本（`.kra`）は `%USERPROFILE%\OneDrive\art\backdrops\layer-<n>\layer-<n>.kra` に置き、どちらの git にも入れません。理由は §1.1 と同じです。PNG と `.meta` の正本は Unity リポ（`RPG-by-card`）です（§2）。

### 13.3 Unity の取り込み設定

Unity 6 の項目名で書きます。括弧内は `.meta` のキーです。立ち絵（§3）と違う項目は太字にしました。

| 項目 | 値 | 理由 |
| --- | --- | --- |
| Texture Type | Sprite (2D and UI)（`textureType: 8`） | 受け口が `Resources.Load<Sprite>` で読むためです |
| Sprite Mode | Single（`spriteMode: 1`） | 1 ファイルに 1 枚です |
| Mesh Type | Full Rect（`spriteMeshType: 0`） | 全面の長方形 1 枚だからです |
| Pivot | **Center**（`alignment: 0`） | 足元の基準点が要りません |
| Generate Physics Shape | off（`spriteGenerateFallbackPhysicsShape: 0`） | 当たり判定を使いません |
| Generate Mipmap | **off**（`enableMipMap: 0`） | 絵は基準の画面でほぼ 1 対 1 に出ます。縮むのは小さい画面だけで、容量 +33% に見合いません。縮んだ画面で縞が出たら on に上げます |
| Filter Mode | **Bilinear**（`filterMode: 1`） | ミップマップが無いので、Trilinear は働きません |
| Wrap Mode | Clamp（`wrapU: 1`, `wrapV: 1`） | 端の画素を繰り返させないためです |
| sRGB (Color Texture) | on（`sRGBTexture: 1`） | 色の絵だからです |
| Alpha Source | **None**（`alphaUsage: 0`） | アルファの無い絵だからです |
| Alpha Is Transparency | **off**（`alphaIsTransparency: 0`） | 透明の縁が無いので、色を広げる処理が要りません |
| Read/Write | off（`isReadable: 0`） | CPU 側の複製でメモリが倍になります |
| Max Size | **2048**（`maxTextureSize: 2048`） | 1920 は 2048 に収まり、縮小が起きません。1024 にすると 1024 × 576 に縮められ、基準の画面で絵が荒れます |

Windows / standalone の上書きは次のとおりです。

| 項目 | 値 | 理由 |
| --- | --- | --- |
| Override | on（`overridden: 1`） | 忘れると上書きが丸ごと無効になります。§3.3 と同じく、`.meta` の diff で最初に見ます |
| Max Size | **2048** | Default と揃えます |
| Format | **RGB Compressed DXT1**（BC1。`textureFormat: 10`） | 不透明なので 4 bpp で足ります。立ち絵の BC7（8 bpp）の半分の容量です |
| Use Crunch Compression | off（`crunchedCompression: 0`） | 7 枚では意味がなく、取り込みが遅くなります |

**容量**: 1920 × 1080 は 2,073,600 texel で、DXT1 は 4 bpp なので 1 枚約 0.99 MiB、7 枚で約 6.9 MiB です。BC7 なら 7 枚で約 13.8 MiB、非圧縮の RGBA32 なら 7 枚で約 55 MiB になります。

**DXT1 は、4 × 4 の画素ごとに 4 色しか持ちません。** 空のなめらかなグラデーションに帯が出ることがあります。1 枚だけ帯が見えたら、その 1 枚を BC7（`textureFormat: 25`）に上げて、理由を台帳の `notes` に書きます。

### 13.4 設定を自動で当てる

§5 と同じく、Preset と Preset Manager で当てます。立ち絵の Preset とは別に、背景用の Preset を 1 つ作ります。

1. §13.3 の値で 1 枚目を設定し、`Assets/Art/Backdrops/BackdropSprite.preset` として保存します。`Resources` の中に置くとビルドに入るので、外に置きます。
2. `m_SpriteSheet.*` / `m_UserData` / `m_AssetBundleName` / `m_AssetBundleVariant` を Exclude Property で外します（§5.1 の 3 と同じです）。
3. Preset Manager に Texture Importer の既定として足し、Filter に `glob:"Assets/Resources/Backdrops/**.png"` と書きます。一般の Sprite 用の行があるなら、その下に置きます（§5.1 の 6）。立ち絵の行とは絞る場所が重ならないので、前後は問いません。
4. 登録は PNG を入れる前に済ませます（§5.2）。`BackdropSprite.preset`、その `.meta`、`ProjectSettings/PresetManager.asset` の 3 つを一緒にコミットします。

### 13.5 台帳

台帳は立ち絵と同じ `Docs/art/asset-ledger.csv` に、同じ列（§7.1）で 1 行ずつ足します。`asset_id` は `bg.layer-<n>`、`file` は `Assets/Resources/Backdrops/layer-<n>.png` です。透明の画素が無いので、`export` の列は `export` と書きます（`save-merged` は要りません）。

### 13.6 絵を入れた後にやること

- 絵を入れた層は、床の赤のコントラストを測り直します（`battle-visual-v1.md` §2.3）。いまの値は、絵が無いときの空と遠景で測った値だからです。
- 探索の画面の幕（`battle-visual-v1.md` §2.5）の上の、本文と琥珀のコントラストも測り直します。
- 試遊版の地面が絵の下半分を覆ってよいかは、1 枚目を画面で見て決めます。
