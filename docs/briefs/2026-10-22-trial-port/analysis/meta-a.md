# meta-a: タイトル・継承の間・終わりのカード・meta の背景（Unity 移植の分析）

対象は試遊版ブラウザゲームの `src/scenes/meta/` のうち `title.js` `hall.js` `ending.js` `backdrop.js` `util.js` `text.js` `index.js` と、`meta.css` の該当規則、それに 3 画面が読む `src/core/inheritance/Inheritance.js` の view-model です。以下、パスはブラウザゲームのルート `…/scratchpad/proto/prototypes/browser-game/` からの相対で書きます。座標はすべて 1920×1080 のステージ px（左上原点、y は下向き）です。

見た目と動きは JS が正です。規則（継承・痕跡・生存者ボーナス・名前・装束）は C# が正ですが、継承の規則は **C# にまだ存在しません**（`docs/INHERITANCE.md:5`）。そのため、この 3 画面を Unity で作るには `Inheritance.js` の C# 移植が前提になります（7 章の unit 1・2）。

## 0. 実測した正解データ（実装時の照合用）

ブラウザで実際に描いた結果を、要素ごとの矩形と PNG で保存しました。フォントは Google Fonts の Shippori Mincho / Zen Kaku Gothic New / IBM Plex Mono（本番の `build.mjs:15` と同じ指定）で、Chromium headless 1920×1080 です。

- 矩形（JSON、1 要素 1 行、`x y w h` はステージ px、`op` は不透明度、`tf` は transform）: `…/scratchpad/meta-a-measure/out/{title,title-first,title-unreadable,hall,hall-first,hall-many,hall-collected,ending-death,ending-alive,ending-miasma,ending-cleared}.json`
- 画像: 同じフォルダの `<画面>-<ms>.png`。タイトルは 300 / 1000 / 1900 / 2300 / 4000 ms、継承の間は 400 / 1300 / 1900 / 2200 / 5000 ms、終わりは 1200 / 3000 / 9000 ms 時点です。
- 再実行: `node …/meta-a-measure/server.mjs 5199`（読み取り専用の静的サーバ）→ `node …/meta-a-measure/run.mjs [画面名…]`。サンプルの系譜は `dev/meta.js` と同じもの（`page.js`）に、回収済みの痕跡と使い終えた生存者ボーナスを持つ `hall-collected` を足しています。
- 終わりのカードは紙が傾いているので、傾きを外した矩形を 3.3 節に別に書きました（`flat.mjs`）。

実機（Windows の Chrome）では、一覧があふれたときに 8 px のスクロールバーが出てカード幅が 434 → 426 px に縮みます。headless では出ないので、JSON の値はスクロールバーなしの値です。

---

## 1. meta 全体で共有するもの

### 1.1 色（`vendor/ds/tokens.css:2`）と color-mix の計算結果

| トークン | 値 |
| --- | --- |
| `--ground` | `#10141d` |
| `--panel` | `rgba(16,20,30,.88)` |
| `--panel-solid` | `#141925` |
| `--ink` | `#ecebf2` |
| `--rule` | `rgba(157,166,186,.26)` |
| `--steel` | `#6d7787` |
| `--amber` | `#e8a84c` |
| `--wick` | `#ffd89a` |
| `--violet` | `#8b6fd0` |
| `--phosphor` | `#b9a3ff` |
| `--washi` | `#f0e6d0` |
| `--washi-ink` | `#1d1a16` |
| `--figure-self` | `#2b2f3a` |
| `--shadow-lamp` | `0 0 0 2px #ffd89a, 0 0 20px 5px rgba(255,216,154,.5)` |
| `--shadow-reach` | `0 0 22px rgba(232,168,76,.5)` |
| 半径 | panel 6 / tag 4（`--radius-panel` `--radius-tag`） |
| 線 | rule 1 / frame 2 / bold 3 |

`color-mix(in srgb, X p%, transparent)` は「X の色で不透明度 p」です。透明でない 2 色の mix は乗算済みアルファで補間します。使う箇所の結果は次のとおりです。

| 式 | 結果 |
| --- | --- |
| `color-mix(amber 8%, panel)`（生存者の箱の背景） | `rgba(35,33,34,.89)` |
| `color-mix(wick 55%, rule)`（層の帯のホバー枠） | `rgba(238,207,160,.667)` |
| `color-mix(washi 65%, ink 35%)`（和紙の明るみ） | `#efe8dc` |
| hall 背景の空 0%: `BackdropSpec.Mix(ground, amber, .06)` | `rgb(29,29,32)` 付近 |
| hall 背景の空 100%: `Mix(ground, black, .45)` | `rgb(9,11,16)` 付近 |

### 1.2 フォント

| CSS | 使う場所 | Unity の Resources/Fonts（`C:\Users\user\Unity\RPG-by-card\Assets\View\Resources\Fonts`） |
| --- | --- | --- |
| Shippori Mincho 400 | タイトルの縦書き 46px、終わりの一文 34px | **無い**（追加が必要） |
| Shippori Mincho 700 | 見出し、名前、ボタン | `ShipporiMincho-Bold.ttf` あり |
| Shippori Mincho 800 | 「継承の間」56px、襲名する名前 72px、「襲名」印 30px、終わりの題 72px | **無い**（追加が必要） |
| Zen Kaku Gothic New 400 / 700 | 本文・チップ | あり |
| IBM Plex Mono 400 | `Esc` `Enter` の小札 16px | **無い**（追加が必要） |
| IBM Plex Mono 600 | 代の番号・層番号・頁送り | `IBMPlexMono-SemiBold.ttf` あり |

CSS の `letter-spacing: .14em` は TMP の `characterSpacing = 14`（em/100 単位）に当たります。CSS の `font: 700 36px/1.2` は「太さ 700・36px・行の高さ 36×1.2」です。日本語の改行は `word-break: auto-phrase`（文節で折る、`meta.css:438`）と `text-wrap: balance`（2 行を揃える、`meta.css:440`、hall の `hall-line` と `heir-reason`）を使っています。TMP にはどちらも無いので、7.3 節の Kit 要求に入れました。

### 1.3 時計・イージング・styler（`common/stage.js:80-131`、`meta/util.js:30-109`）

- **イージング**（`stage.js:80`）: `out = 1-(1-t)^3`、`in = t^3`、`inout = t<.5 ? 4t^3 : 1-(-2t+2)^3/2`、`linear`、`smooth = t²(3-2t)`。既存の `UiTween.Apply`（`Assets/View/UiTween.cs:23`）の In/Out/InOut と同じ式です。CSS の transition は既定の `ease` = `cubic-bezier(.25,.1,.25,1)`、keyframe の `ease-out` = `cubic-bezier(0,0,.58,1)` で、こちらは UiTween に無いので追加が要ります。
- **tween**（`stage.js:95`）: `tween(obj, to, ms, ease, delay)` は呼んだ時刻 + delay で始まり、**始まった瞬間の値**を from に取ります。同じ obj に同じキーの tween を足すと、古い tween からそのキーを外します（全キーが外れたら古い方を resolve）。進み具合は rAF の実時間（`now`）で計算し、dt は使いません。`wait(ms)` はタイマー、`stop()` は全 tween と wait を即 resolve して止めます。
- **sceneClock**（`util.js:93`）: OS の「視差効果を減らす」が有効なら、全 tween を 1 ms・遅延 0 にします。
- **styler(el, init)**（`util.js:34`）: 要素の `o`（opacity）`x` `y`（px）`s`（scale、縦横同率）`r`（度）をまとめた代理です。作った瞬間に init を当て、`transform: translate(x,y) scale(s) rotate(r)` を書きます。回転と拡大の中心は CSS の `transform-origin` で、既定は要素の中心です（タイトルの人影は `50% 100%`、横線は `0 50%`、終わりの紙は `50% 40%`）。Unity では pivot をこれに合わせます。
- **注意**: styler は呼ぶたびに別の代理を作るので、同じ要素に別の代理で tween を重ねると打ち消し合いません（タイトルの入場中に Enter を押した場合など）。C# では「要素 + プロパティ」ごとに 1 本だけ走るチャネルにして、後から来た tween が前のものを止める形を勧めます。見た目の意図は同じです。
- **riseIn(clock, els, {delay=0, step=70, dy=18, ms=420})**（`util.js:104`）: 各要素を `o0,y+dy → o1,y0`、`out`、遅延 `delay + i*step`。終わったら transform を外します。

### 1.4 キー・ポインタ・音

- **キーの積み重ね**（`stage.js:136` `pushKeys`）: 一番上のハンドラが先に受け、true を返したら飲み込みます。オーバーレイ（設定・手記）が上に積まれると画面は受けません。押しっぱなしの繰り返し（`e.repeat`）は、オーバーレイを開くキー（Esc、J）で無視します。
- **音**（`util.js:13` `soundOf`）: `play(name, delayMs)` は例外を出しません。ブラウザでは最初のユーザー操作まで鳴りません。プリセット（`scenes/audio/Audio.js:58-82`、`docs/AUDIO.md:65-72`）は次のとおりで、合成の実装は audio 担当の範囲です。

| 名前 | timbre（vol / pitch Hz / dur ms） | 鳴る場所 |
| --- | --- | --- |
| `titleEnter` | bow（0.075 / 73.42 / 2800） | タイトルから入る |
| `hallArrive` | kindle（0.12 / 2400 / 1500） | 継承の間に着く（遅延 80 ms） |
| `heirNamed` | nameSeal（0.18 / 98 / 1500） | 襲名の印が押される |
| `lifeEndDeath` | expire（0.15 / 55 / 4400） | 終わりのカード・倒れた（遅延 300 ms） |
| `survived` | relief（0.12 / 440 / 2200） | 終わりのカード・生存ルートと踏破（遅延 300 ms） |
| `uiClick` | wood（0.07 / 760 / 30）gap 40 ms | ボタン、キーでのカーソル移動 |
| `uiConfirm` | seal（0.10 / 170 / 110） | 決定 |
| `uiCancel` | slide（0.08 / 620 / 110） | タイトルへ戻る |
| `nodeHover` | paper（0.035 / 2600 / 22）gap 60 ms | ポインタでのカーソル移動 |

環境音は router が切り替えます（`app/main.js:216` title 1200 ms、`:264` hall 1500 ms、`:497` 終わりのカードの直前に hall 2000 ms）。瘴気死の終わりのカードは無音です（`AUDIO.md:38`）。

### 1.5 共有の CSS 部品（`meta.css`）

| クラス | 見た目 |
| --- | --- |
| `.ocbg-meta`（`:6`） | 1920×1080、overflow hidden、既定の文字は Zen Kaku 400・ink |
| `.ocbg-meta-backdrop`（`:13`） | 背景キャンバス 1920×1080、クリックを通す |
| `.ocbg-meta-fade`（`:14`） | 全面の `--ground` 幕、z 50、クリックを通す |
| `.ocbg-meta-keys`（`:15`） | Zen 400 16px / 行高 22px、`.06em`、ink、折り返さない |
| `.ocbg-meta-dot`（`:23`） | 10×10 の丸、steel |
| `.ocbg-meta-primary`（`:29-32`） | 枠 2px amber・角 6・背景 amber 16%、Shippori 700 28px/1 `.2em` ink、子は中央寄せ gap 16。小札 `small`: Plex 400 16px/1 `.04em`、padding 4 8、枠 1px rule、角 4。ホバーで背景 amber 28%・`--shadow-reach`・上へ 2px。transition: background / box-shadow / transform 140 ms、opacity 200 ms（すべて `ease`） |
| `.ocbg-meta-chip-btn`（`:36-38`, `:417-419`） | 背景 panel、枠 1px rule、角 6、Zen 700 20px/1、子は中央寄せ gap 8、小札 `small` は Plex 400 16px/1。ホバーで枠 ink・上へ 1px（border-color / transform 140 ms）。`is-square` は padding 0 |
| `.ocbg-meta-washi`（`:268-273`） | 和紙。下に `--washi`、上に 3 枚を重ねる: (1) 繊維タイル 360×360（`util.js:127` `applyWashi`、1.6 節）、(2) `radial-gradient(ellipse at 30% 18%, #efe8dc 0%, washi 透明 60%)`、(3) `radial-gradient(ellipse at 78% 92%, washi-ink 7% 0%, 透明 55%)`。楕円の大きさは CSS 既定の farthest-corner（縦横比は farthest-side と同じ） |
| `.ocbg-meta-washi-btn`（`:291-293`） | 枠 2px washi-ink、角 6、padding 16 30、Shippori 700 26px/1 `.16em` washi-ink、gap 16。小札は Plex 400 16px/1、padding 4 8、枠 1px washi-ink 40%、角 4。ホバーで背景 washi-ink 8%・上へ 2px（140 ms） |
| `button:focus-visible`（`:11`） | Tab で来たときだけ outline 2px wick、offset 3。3 画面とも Tab を使わないので、Unity では省いてよい項目です |
| スクロールバー（`:24-26`） | 幅 8、つまみ steel・角 4、溝は透明 |

### 1.6 和紙の繊維（`util.js:127-171` `applyWashi`）

一度だけ 360×360 のタイルを描き、data URL にして全和紙で共有します。乱数は Park-Miller（seed 48271、`seed = seed*48271 % 2147483647`、値は `seed/2147483647`）で、毎回同じ絵になります。色は `--washi-ink`（29,26,22）です。描くものは、ぼかし丸 26 個（中心 `rnd*360` ×2、半径 `30+rnd*70`、中心の不透明度 `.012+rnd*.02` から外周 0 への放射グラデーション）と、繊維 260 本（始点 `rnd*360` ×2、長さ `6+rnd*rnd*46`、角度 `(rnd-.5)*π*.9`、曲がり `(rnd-.5)*len*.6`、二次ベジェで制御点 `((x0+x1)/2+bend, (y0+y1)/2-bend)`、不透明度 `.04+rnd*.07`、線幅 `.5+rnd*.9`、丸い端）です。各図形は 3×3 にずらして描き、継ぎ目なく並べられるようにしています。乱数を引く順番は「丸ごとに cx, cy, rad, a」「線ごとに x0, y0, len の 2 回, ang, bend, a, w」です。

Unity では「焼いた画像を持たない」方針なので、起動時にこのタイルを `Texture2D` に描いて（wrap Repeat）`RawImage` の uvRect で敷くのが一番近い作り方です。実行時に作るのでアセットは増えません。

### 1.7 meta の背景（`backdrop.js`）

`createBackdrop(host, spec, {veil, vignette, moteSpeed, vignetteColor})` は 1920×1080 のキャンバスに毎フレーム描きます。値はすべて橋渡し層の `BackdropSpec` / `BackdropMotion` から取ります。C# の `Backdrop.Script`（`Assets/View/Backdrop/Script/BackdropSpec.cs` `BackdropMotion.cs` `ExplorationBackdrop.cs`）がそのまま同じ値と関数を持っているので、純粋な部分は再利用できます。

描く順番（`backdrop.js:102-136`）と、全体に掛かる `alpha`（継承の間で 0→1 に tween する「灯り」）は次のとおりです。

1. **空**: `spec.Sky` の各 stop（At 0〜1 を縦に）で縦の線形グラデーション、×alpha。
2. **霞**（`spec.Hazes` の順）: 明るさ `lit = BackdropMotion.FlickerAlpha(haze.Flicker, ms)`。
   - `TopGlow` / `Band`: 全面の縦グラデーション。33 点（i/32）で `haze.Color` の不透明度 × `lit` × `BackdropSpec.HazeAlphaAt(色を白・α1 にした haze, y)`。
   - `Radial`: 中心 `(X*1920, Y*1080)`、直径 `Size*1080`、中心 `Color×lit` から外周 0 への線形な放射。
   - `Shafts`: 本数 `Count`、間隔 `1920*0.18`、i 本目の中心 x = `X*1920 + (i-(Count-1)/2)*345.6`、y = 540。幅 `Size*1920`、高さ `1080*1.6`。画面上で時計回りに `Angle` 度回し、横方向に 0→lit（25%）→lit（75%）→0 の台形グラデーション。
3. **舞う粒**: `t = ms * moteSpeed`。位置 `BackdropMotion.MoteAt(m, i, t, 1920, 1080)`（粒の中心）、不透明度 `alpha × m.Color.A × MoteAlpha(m, i, t)`。粒の絵は直径 `max(4, ceil(SizePx*2))` px の丸で、半径の 45% まで不透明、そこから外周で 0 です。**SizePx 4 なら直径 8 px** です（既存の `BackdropLayers.cs:123` は直径 SizePx で `1-d²` の丸なので、ここは JS に合わせます）。
4. **ベール**（opts.veil があれば）: 全面を veil 色で塗る、×alpha。
5. **周辺減光**（opts.vignette があれば）: 中心 (960,540)、内半径 `1080*0.35 = 378` で透明、外半径 `1920*0.62 = 1190.4` で `--ground` の不透明度 vignette、の放射グラデーション、×alpha。

`ms` はページの時刻（rAF のタイムスタンプ）で、場面ごとにリセットしません。Unity では `BackdropLayers` と同じく `Time.unscaledTimeAsDouble*1000` を使えば十分です。`alpha<1` のとき下に透けるのはステージの `--ground` です。

3 画面の指定は次のとおりです。

| 画面 | spec | veil | vignette | moteSpeed | alpha |
| --- | --- | --- | --- | --- | --- |
| タイトル（`title.js:19`） | `BackdropSpec.For(7)` | なし | 0.55 | 0.7 | 1 |
| 継承の間（`hall.js:84`） | `hallBackdropSpec`（下） | なし | 0.5 | 1 | 0 → 1（3.2 節） |
| 終わり（`ending.js:59`） | `BackdropSpec.For(layer)` | `ExplorationBackdropSpec.For(layer).Veil`（`--ground` を層 1〜7 で 0.72 / 0.68 / 0.35 / 0.70 / 0.35 / 0.35 / 0.35） | 0.75 | 死なら 0.35、生なら 0.8 | 1 |

継承の間の部屋（`hall.js:17-30` `hallBackdropSpec(el, strength=1)`）は、層と同じ型で作った暖かい部屋です。

- `Layer 0`、`Name '継承の間'`
- 空: `0 → Mix(ground, amber, .06)`、`.55 → ground`、`1 → Mix(ground, black, .45)`
- 霞 1: `Radial`、amber α `0.16*strength`、X .5、Y .02、Size 1.25（直径 1350 px、中心 (960, 21.6)）、Flicker(4200 ms, 0.28)
- 霞 2: `Radial`、wick α `0.07*strength`、X .86、Y .30、Size 0.7（直径 756、中心 (1651.2, 324)）、Flicker(5300, 0.35)
- 粒: Count 26、wick α `0.22*strength`、SizePx 3、SpeedX 3、SpeedY −5、Twinkle 0.6

C# の `LayerBackdrop` / `Haze` / `Motes` は public フィールドを持つ class なので、オブジェクト初期化子でこのまま組めます。

### 1.8 MetaText（`text.js`）

meta の全画面が使う固定の文言です。このうち 3 画面が使うのは次のものです（`text.js:8-40`）。残り（出立・所持カード・設定・手記の文言）も同じファイルにあるので、C# の `MetaText.cs` は全部を 1 つに移します（他の meta 画面の担当もここを読みます）。

- `GameName 'Original Card Battle'`、`TaglineLines ['死ぬたびに人は変わる。', 'だが、技と記憶は次の生へ。']`
- `NewGame 'はじめから'`、`Continue 'つづきから'`、`Settings '設定'`、`NoSave '保存された生は無い'`、`Unreadable '記録を読み込めない'`、`TitleKeys '↑↓ 選ぶ　Enter 決める'`
- `NameSeal ['襲','名']`、`HallKeysFor(traces, scrolls)`（`←→ 痕跡を選ぶ` は痕跡 2 件以上、`↑↓ 候補を読む` は候補一覧があふれているときだけ、続けて `Enter 出立` `Esc 設定`、区切りは全角スペース）
- `MarkTitle(heirLine, ending, trace) = '${heirLine}　${ending}・${trace}'`、`MarkUntaken '未回収の痕跡'`、`MarkTaken '回収済み'`、`MarkAlive '生きて閉じた'`、`BackToTitle 'タイトルへ'`
- `TraceCollected(g, name) = '痕跡：第 ${g} 代 ${name}が回収した'`
- `TraceWaits(layer, g, name) = '第 ${layer} 層には第 ${g} 代 ${name}の古い痕跡がある。それが回収されると現れる'`、`TraceWaitsMany(layer, count) = '第 ${layer} 層には古い痕跡が ${count} 件ある。それらが回収されると現れる'`

---

## 2. タイトル（`title.js`）

### 2.1 要素と配置

重なり順は下から背景 → 地面 → 人影 → 縦書き → 名前 → 横線 → メニュー → キー案内 → 幕です。

| 要素 | 位置・大きさ | 見た目 |
| --- | --- | --- |
| 背景 | 0,0 1920×1080 | 1.7 節（層 7、減光 .55、粒の速さ .7） |
| 地面 `title-ground` | 0,826 1920×254 | 縦グラデーション ground 0% → ground 45%（80% = y 1029.2）→ 下端まで 45%。`meta.css:408` が `:41` を上書きしているので、こちらが正です |
| 地平線（`::before`、`meta.css:409`） | x 422.4〜1497.6（左右 22%）、y 826、高さ 1 | 横グラデーション violet 0% → violet 55%（中央）→ violet 0% |
| 人影の枠 `title-figure` | 890,600 140×226（足元 y 826 が地平線） | 回転・拡大の中心は足元（`50% 100%`） |
| 足元の灯り（`O.FootLight(200,false)`） | 枠内 −30,196 → ステージ 860,796、200×60 | 楕円（半径 100×30）の放射グラデーション `rgba(232,168,76,.55)` → 透明（closest-side）。不透明度は毎フレーム `0.55 + 0.35k`、`k = 0.5-0.5cos(2π·now/4200)` |
| 人影（`O.Figure('swordsman')`） | 枠と同じ 140×226 | 下の 2.2 |
| 縦書き `title-tagline` | 1440,104 280×880 | 列は右から左（row-reverse）、列の間 40 |
| 1 列目「死ぬたびに人は変わる。」11 字 | x 1674、幅 46、y 104 から | Shippori 400 46px、行高 1、字間 `.22em`、ink、影 `0 0 18px violet 40%` |
| 2 列目「だが、技と記憶は次の生へ。」13 字 | x 1588、y 200 から（margin-top 96） | 同上 |
| 1 字ごとの送り | 46×1.22 = **56.12 px** | 実測: 1 列目は 104, 160.1, 216.3 …、2 列目は 200, 256.1 … |
| 名前 `title-name` | 152,236（幅は中身、実測 504.3×30） | Shippori 700 30px/1、字間 `.34em`、ink、影 `0 2px 12px ground 80%` |
| 横線 `title-rule` | 152,300 360×2 | 横グラデーション amber → amber 0%。中心は左端（`0 50%`） |
| メニュー `title-menu` | 152,560 幅 520 | 縦並び、間 12 |
| 項目（3 つ） | 152, 560 / 635.2 / 710.4、520×63.2 | padding 上下 10・左 48、子は baseline 揃え gap 16。文字 Shippori 700 36px/1.2、字間 `.14em`、ink。文字の左上は (200, 項目 y+10)、高さ 43.2 |
| 選択の線 `title-mark` | 項目の左端、y = 項目 y+30.6、32×2 | amber。中心は左端、`scaleX 0`（選択で 1） |
| 線の先の灯（`::after`） | 線の右端 −4・上 −3、8×8 | wick の丸、影 `0 0 10px wick`、不透明度 0（選択で 1）。中心は (項目 x+32, 項目 y+31.6) |
| 選べない理由 `small` | 文字の右 +16、baseline 揃え（実測 x 421.2、y = 項目 y+32.0） | Zen 400 16px/1、字間 `.04em`、ink、不透明度 .85 |
| キー案内 | 152,1004（実測 189.2×22） | 1.5 節 `.ocbg-meta-keys`、`'↑↓ 選ぶ　Enter 決める'` |
| 幕 | 0,0 1920×1080 | ground、z 50 |

router は読めない記録・保存できない環境のときだけ、メニューの下に通知を出します（`app/main.js:246-256`、位置 152, `840 - max(0, 行数-2)*25`、幅 560、z 40 で幕より下）。通知そのものは app 担当です。

### 2.2 人影（design system の `Figure('swordsman')`）

`artifact/ocb.js:584-597`（`FIGURES.swordsman`）の 12 本の SVG パスで、viewBox 140×226、足が下端です。描き方（`ocb.js:641-654`）は、全パスを縁の色 `rgba(232,168,76,.28)` で塗り、同じ色・線幅 4・角を丸めた線（`stroke-linejoin: round`）でなぞり、その上に全パスを `--figure-self #2b2f3a` で塗ります。結果として外側に 2 px の琥珀の縁が付きます。`poly(x1,y1,x2,y2,w)`（`ocb.js:579`）は太さ w の棒を 4 点の多角形にする関数で、剣と柄に使っています。継承の間も同じ人影を使います。

### 2.3 状態

- **選択中 `is-on`**（`meta.css:56-58`）: 字間 `.14em → .2em`、影 `0 0 22px amber 45%`（どちらも transition 200 ms `ease`）、線 `scaleX 0 → 1`、灯 `0 → 1`（どちらも 220 ms `ease`）。
- **選べない `is-off`**（`:59-61`）: 文字の不透明度 .4、カーソルは矢印。`small` は ink の .85。
- **最初の選択**: 保存があれば「つづきから」、無ければ「はじめから」（`title.js:85`）。
- **選べる条件**: はじめからは常に、つづきからは `ctx.hasSave`、設定は `ctx.onSettings` があるとき。つづきからが選べないときは `small` に `ctx.unreadable ? '記録を読み込めない' : '保存された生は無い'` を出します。

### 2.4 動き（入場、`title.js:122-143`）

時刻はマウントからの ms です。ease は 1.3 節の式です。

| 対象 | 変化 | 長さ | ease | 遅延 |
| --- | --- | --- | --- | --- |
| 幕 | o 1 → 0 | 1600 | inout | 0 |
| 人影の枠 | o 0→1、y +12→0 | 1400 | out | 500 |
| 名前 | o 0→1、y +8→0 | 900 | out | 900 |
| 横線 | o 0→1、s 0.4→1（縦横とも、中心は左端） | 1100 | out | 1000 |
| 縦書きの 1 字ずつ（両列通しで i = 0〜23） | o 0→1、y −10→0 | 700 | out | 900 + i×85（最後は 2855 開始） |
| メニューの項目 i（0〜2） | o 0→1、x −16→0 | 500 | out | 1800 + i×110。3 つとも終わったら transform を外す |
| キー案内 | o 0→1 | 700 | out | 2400 |
| 足元の灯 | 不透明度 0.55〜0.90、周期 4200 ms の余弦 | 常時 | — | — |

### 2.5 動き（退場、`title.js:108-120` `leave(cb, enter)`）

`busy` を立て、`enter` なら `titleEnter`、続けて `uiConfirm` を鳴らします。

| 対象 | 変化 | 長さ | ease | 遅延 |
| --- | --- | --- | --- | --- |
| メニュー | o →0、x →−24 | 360 | in | 0 |
| 名前 | o →0 | 420 | in | 0 |
| 縦書き全体 | o →0 | 700 | in | 0 |
| 人影の枠 | o →0、y →+26、s →0.92（足元中心） | 900 | in | 0 |
| 幕 | o →1 | 900 | inout | 200 |

幕が閉じきった 1100 ms 後に `cb()` を呼びます。

### 2.6 操作

- **ポインタ**: 項目に入ると `focus(i, true)`（選べる項目で、今と違うときだけ。`nodeHover`）。クリックで `choose(i)`。
- **キー**（`title.js:151-160`）: `busy` 中と、router の確認カード（`.ocbg-confirm`）が開いている間は受けません。`↑` / `w` で上、`↓` / `s` で下（端で回り、選べない項目は飛ばす。`uiClick`）。`Enter` / `Space` で決定。`Esc` は押しはじめだけ設定を開きます（`uiClick`）。
- **決定の中身**（`title.js:56-65`）:
  - はじめから: 保存も読めない記録も無ければ `leave(onNew, true)`。あれば `uiClick` だけ鳴らして `onNew()` を呼び、router がタイトルの上に確認カードを出します（`app/main.js:226-241`）。確認で「はじめから」を選ぶと router が直接継承の間へ切り替えるので、このときは退場の動きがありません。
  - つづきから: `leave(onContinue, true)`。
  - 設定: `uiClick` → `onSettings()`。

### 2.7 読むデータとコールバック

`ctx: { stage, audio, settings, hasSave, unreadable?, onNew(), onContinue(), onSettings() }`。規則のデータは読みません。返り値は `{ destroy() }` で、キーを外し、時計を止め、根を消します。

---

## 3. 継承の間（`hall.js`）

### 3.1 要素と配置（実測は `out/hall.json`）

| 要素 | 位置・大きさ | 見た目 |
| --- | --- | --- |
| 背景 | 全面 | 1.7 節の部屋、減光 .5、alpha は灯り（3.4 節） |
| 題「継承の間」 | 72,36（実測 277.8×61.6） | Shippori 800 56px/1.1、字間 `.24em`、ink、影 `0 0 30px amber 30%` |
| リード | 74,110（実測 395.2×28） | Zen 400 20px/1.4、字間 `.04em` |
| 右上のボタン | y 44、高さ 52。右端 1848 から左へ、間 16 | 1.5 節の chip-btn |
| ・設定（`onSettings` があるとき） | x 1728、120×52 | 「設定」Zen 700 20 + 小札「Esc」Plex 400 16、gap 8 |
| ・手記（`onJournal` があるとき） | x 1656、56×52 | journal アイコン 30px ink、`is-square`。router は今これを渡していないので、実際の画面には出ません（`app/main.js:265-271`） |
| ・タイトルへ（`onTitle` があるとき） | 手記ありで x 1472、無しで x 1544、168×52 | back アイコン 20 ink + 「タイトルへ」 |
| 見出し h2 ×3 | 72,184 / 552,184 / 1428,184（高さ 36） | Shippori 700 24px/1、字間 `.14em`、下 padding 12、下端に 56×2 の amber の線（`::after`、左下） |
| これまでの踏破者の一覧 | 72,236 440×486 | 縦並び、間 16、右 padding 6、縦スクロール（3.5 節） |
| 痕跡の規則の箱 | 72,742 440×244 | 背景 panel、枠 1px rule、角 6、padding 14 16、縦中央揃え、間 6 |
| 七層の柱 | 552,236 360×750 | 帯 7 本（3.1.2） |
| 印の凡例 | 552,1000 360×40 | 折り返しあり、間 縦 6・横 18、Zen 400 18px / 行高 24 |
| 選んだ痕跡の箱 | 936,236 452×750 | 背景 panel、枠 1px rule、角 6、padding 20 22 18 |
| 次の踏破者の箱 | 1428,236 420×696 | 縦並び、間 16（3.1.4） |
| 出立ボタン | 1428,948 420×76 | primary（1.5 節）。「出立の支度へ」+ 小札「Enter」 |
| キー案内 | 72,1028 | `HallKeysFor(…)` |
| 幕 | 全面 | ground、z 50 |

#### 3.1.1 これまでの踏破者のカード（新しい順、`hall.js:127-139`）

カードは幅 434（あふれてスクロールバーが出ると 426）、高さは中身で決まります（実測 168.3、一文が 2 行なら 194.4）。

- 箱: 背景 panel、枠 1px rule、**左だけ 3px steel**（生きて閉じた生 `is-alive` は amber）、角 6、padding 上 14・右 16・下 12・左 18、縦並び間 6。
- 1 行目（見出し、baseline 揃え、間 12）: 「第 n 代」Plex 600 18px/1 `.02em` → 名前 Shippori 700 28px/1.1 `.1em` → 終わり方の札（右寄せ `margin-left:auto`、縦中央、inline-flex 間 6、padding 3 10 3 6、枠 1px steel・生なら amber、角 4、Zen 700 18px/1.2、アイコン 22px）。
- アイコン（`hall.js:32-39` `endingIcon`）: 瘴気死 = miasma（phosphor）、倒れた = person（steel）、踏破 = star（amber）、生存ルート = person（amber）。
- 2 行目: 終わりの一文 `EndingLine`、Zen 400 18px/1.45、`text-wrap: balance`。
- 3 行目: 場所 `PlaceLine`、stairs アイコン 18 ink + 間 8、Zen 400 18px/1.4。
- 4 行目（footer）: 上 margin 6、上 padding 8、上に 1px rule の線、左右に振り分け（間 12）、Zen 400 18px/1.4。左「装束　＜段の名＞」（折り返さない）、右は痕跡の行（`pastTraceLine`、3.6 節）。
- 実測の縦位置（カード上端 y0 から）: 見出し y0+15、一文 y0+51.8、場所 y0+83.9、footer y0+121.1（線）→ 文字 y0+130.1、カード高さ 168.3。

生が 1 つも無いときは `p.hall-none`（Zen 400 18px/1.6、padding 16 0）で「最初の踏破者。継ぐものはまだ無い。」を出します。

#### 3.1.2 七層の柱（`hall.js:156-211`）

- 帯 i（層 1〜7）: x 552、y `236 + (i-1)×108`、360×102。枠 1px rule、角 6、はみ出しは切る。背景は層の空（`BackdropSpec.For(i).Sky` の各 stop を At×100% の位置に置いた縦グラデーション、枠の下まで塗る）。transition: box-shadow / border-color 200 ms `ease`。
- ベール（帯の内側全面）: 横グラデーション ground 86%（0%）→ ground 18%（70%）→ ground 40%（100%）。
- 層の名札: 帯の左上 +12,+12、背景 panel、角 4、padding 5 12 5 6、間 10、Shippori 700 20px/1 `.08em`。中に番号の丸（28×28、枠 1px steel、Plex 600 16px/1）と層の名前 `SevenLayers.Of(i).DisplayName(lineage.Player.CarvingTaken)`。
- 印の列: 帯の右下 −12,−12 から左へ、間 8。
- **印**（`hall.js:177-199`、生 1 つにつき 1 つ、その生が終わった層の帯に置く）: 44×44 の丸、背景 panel-solid、枠 2px steel、中に代の番号 Plex 600 18px。状態の組み合わせ: `is-alive`（生きて閉じた）枠 amber、`is-taken`（痕跡が回収済み、`life.CollectedBy !== null`）枠を破線、`is-trace`（未回収の痕跡がある）枠 wick・`--shadow-lamp`・カーソル指・ホバーで上へ 2px（transition なし）。
- **炎**（`is-trace` の印だけ）: 印の右上 −5,−5 に 14×14、放射グラデーション wick → 透明（closest-side）。毎フレーム、炎の番号 i（生の順）で `k = 0.5-0.5cos(2π(now + i·700)/1900)`、不透明度 `0.55 + 0.45k`、拡大 `0.85 + 0.25k`（`hall.js:381-387`）。
- **帯が選ばれている `is-sel`**: 枠 wick、影 `0 0 0 1px wick, 0 0 22px wick 30%`。
- **帯で痕跡を選べる `is-live`**（未回収の痕跡がある帯）: カーソル指。選ばれていなければ、ホバーで枠 `rgba(238,207,160,.667)`。
- 同じ帯に印が 2 つ以上あるときは、列ごと右下に寄せたまま生の古い順に左から並べます。古い生が左、新しい生が右端です（実測 hall-many: 第 1 代が x 803、第 2 代が x 855）。
- 印のツールチップ（HTML の `title`）: 生の記録があれば `MarkTitle(past.Line, past.EndingLabel, pastTraceLine(...))`、無ければ `InheritanceText.HeirLine(gen, name)`。
- **凡例**（`hall.js:203-211`）: 柱が実際に使っている見た目だけを、未回収 → 回収済み → 生きて閉じた の順に並べます。小さい印は 22×22・影なし、未回収のものだけ影 `0 0 8px wick 60%`。3 つ並ぶと 2 行に折り返します（実測 hall-collected: 1 行目 y 1001、2 行目 y 1031）。

#### 3.1.3 選んだ痕跡の箱（`hall.js:214-288` `drawTrace`）

箱の中身は `body` 1 つで、縦並び間 12、縦スクロール（3.5 節）です。中身の左上は (959, 257)、幅 406、高さは最大 710 です。

- 上段: 終わり方の札（3.1.1 と同じ。生なら `t.Cleared` で amber、アイコンは `lineage.Traces` から引いた Ending）と、痕跡が 2 件以上なら右端に頁送り「n / 総数」（Plex 600 18px/1）。
- `h3` 痕跡の主 `HeirLine`: Shippori 700 26px/1.25 `.06em`。
- 場所 `PlaceLine`（3.1.1 と同じ形）。
- 現れ方 `AppearsLine`: 丸 10×10（`Visible` なら wick + 影 `0 0 8px wick`、待ちなら steel・影なし）+ 間 10、Zen 400 18px/1.4。
- 注記 `Note`（踏破した生の痕跡だけ）: Zen 400 16px/1.45、padding 8 10、左 2px amber、背景 amber 8%。
- 数 `CountLine`: Zen 700 18px/1.4、上 padding 8、上に 1px rule。
- 候補の一覧（各行は grid `136px | 残り | 中身幅`、縦中央、間 12、最小高さ 42、下に 1px rule）: 種類（アイコン 18 + 間 6 + 「技術」等、Zen 400 16px/1）、名前（Shippori 700 20px/1.2 `.04em`、はみ出しは …）、詳細（Zen 400 16px/1.2、右端）。種類のアイコン（`hall.js:41-48`）: 技術 = star amber、敵の頁 = journal ink、ダンジョンの頁 = stairs ink、走り書き = journal ink。
- 走り書きの行（`is-long`）: grid `136px | 残り`、行の間 2、padding 6 0。名前は折り返し可・18px・字間 0・行高 1.3・上 padding 4、詳細は 2 列目の 2 行目で折り返し可。
- 浅い層で残らなかった札（`Withheld` があるとき）: 見出し h4 Zen 700 18px/1.3（上 margin 8）、各行は左 2px の steel 破線、padding 6 0 6 12、左右振り分け、名前 Shippori 700 18px/1.3、理由 Zen 400 16px/1.4。
- 痕跡が無いとき（`is-empty`）: 中身が箱いっぱいに広がり、中央に 18×18 の丸（枠 2px steel）と「未回収の痕跡は無い。」（Zen 400 18px/1.6）を間 16 で縦に並べます（実測: 丸 1153,564.6、文字 1072,598.6）。
- 実測の縦位置（hall.json）: 上段 257、h3 295、場所 335.5、現れ方 368.7、数 405.9（線）、候補 1 行目 452.1、以後 42 ずつ。

#### 3.1.4 次の踏破者（`hall.js:290-341`）

| 要素 | 位置（実測） | 見た目 |
| --- | --- | --- |
| 代 `heir-gen` | 1428,236 高さ 26 | 「第 n 代」Shippori 700 26px/1 `.1em` + 間 12 +「踏破者」Zen 400 18px/1 `.1em`（baseline 揃え） |
| 名前の行 | 1428,278 高さ 96 | 横並び、縦中央、間 24 |
| 名前 | 1428,290、1 字 82.08 幅 | Shippori 800 72px/1、字間 `.14em`、影 `0 0 34px amber 32%`、1 字ずつ別の箱 |
| 襲名の印 | 名前の右 +24（3 字なら x 1698.3）、y 280、62×92、最後は −4° | 縦に「襲」「名」、Shippori 800 30px/1.08 amber、枠 3px amber、角 4、背景 amber 10% |
| 装束 | 1428,390 幅 262 | 縦並び間 6。h4「装束」Zen 700 18px/1 `.2em` → 段の名 Shippori 700 26px/1.2 `.08em` → 理由 Zen 400 18px/1.4（`balance`）→ 3 行（上 margin 6、間 6） |
| 装束の 1 行 | 高さ 24、間 10 | 色見本 24×24（角 4、枠 1px steel、色が無ければ破線で透明）→ 部位名（幅 96）→ 説明、Zen 400 16px/1.3 |
| 人影 | 箱の右 16・上 166 → 1692,402、140×226 | 2.2 と同じ。足元の灯は 180 幅で枠内 −20,196 → 1672,598 |
| 前回の生存者 | 箱の下端に寄せる（`margin-top:auto`）。実測 1428,737.3 420×194.8 | padding 14 16、枠 1px amber、角 6、背景 `rgba(35,33,34,.89)`、縦並び間 6。h4 Zen 700 18px/1.2 `.12em` → 主 Shippori 700 24px/1.2 `.06em` → 行ごとに plus アイコン 18 amber + 間 8、Zen 400 18px/1.4 → 期間 Zen 400 16px/1.3 |
| ボーナスを使い終えた（`SurvivorSpent`） | 同じ位置（実測 1428,789.2、高さ 142.8） | 枠 ink 30%、背景 panel、行にアイコンなし、期間なし |

### 3.2 動き（到着と襲名、`hall.js:343-379`）

`n` は名前の字数、`sealAt = 1250 + n×110 + 240`（3 字なら 1820）です。

| 対象 | 変化 | 長さ | ease | 遅延 |
| --- | --- | --- | --- | --- |
| 音 `hallArrive` | — | — | — | 80 |
| 幕 | o 1→0 | 700 | out | 0 |
| 灯り（背景の alpha） | 0→1 | 1600 | out | 150 |
| 題、リード | o 0→1、y +10→0 | 600 | out | 250 |
| 左列（見出し、各カード、規則の箱） | riseIn: o 0→1、y +18→0 | 420 | out | 380 + i×90 |
| 中列（見出し、帯 7 本） | riseIn: y +10→0 | 420 | out | 480 + i×60 |
| 痕跡の箱 | o 0→1、x +16→0 | 500 | out | 900 |
| 右列（見出し、代） | riseIn: y +18→0 | 420 | out | 1000 + i×80 |
| 装束・人影・生存者の箱 | 最初は o 0 で隠す | — | — | — |
| 名前の 1 字ずつ | o 0→1、y +14→0 | 420 | out | 1250 + i×110 |
| 襲名の印 | 最初 o 0・s 1.9・r −14° → wait(sealAt) のあと o 1・s 1・r −4° | 170 | **in** | sealAt |
| 押された瞬間 | 音 `heirNamed`、印の周りに輪（`box-shadow 0 0 0 0 amber 70%` → `0 0 0 22px amber 0%`） | 700 | CSS `ease-out` | sealAt+170 |
| 装束・人影・生存者の箱（順に i） | o 0→1、y +10→0 | 520 | out | 押された瞬間 + 120 + i×120 |
| 出立ボタン | o 0→1、y +12→0 | 520 | out | sealAt+500 |
| キー案内 | o 0→1 | 600 | out | sealAt+700 |
| 右上のボタン | o 0→1 | 500 | out | 600 |
| 炎 | 3.1.2 | 常時 | — | — |

画面を作り直した直後（3.2 の入場中）は、どのボタンも押せます（`busy` は退場でだけ立ちます）。

### 3.3 退場（`hall.js:389-406`）

- **タイトルへ** `leaveToTitle()`: `uiCancel`、灯り → 0.35（600 ms inout）、幕 → 1（500 ms in、遅延 100）、閉じたら `onTitle()`。
- **出立** `depart()`: `uiConfirm`、`lineage.Current` が無ければここで `startLife(lineage, ctx.seed ?? null)` を作る（あれば null）、灯り → 0.35（700 ms inout）、幕 → 1（650 ms in、遅延 150）、閉じたら `onDepart(lifeStart)`。router は null のとき自分で `startLife` します（`app/main.js:267`）。

### 3.4 選択と操作

- **痕跡を選ぶ** `select(i, byPointer)`（`hall.js:280-287`）: 同じ番号なら何もしません。音はポインタなら `uiClick`、キーなら `nodeHover`（タイトルと逆です）。中身を作り直し、`body` を o 0・y +8 → 1・0（220 ms out）で出します。選んだ痕跡の層の帯に `is-sel` を付けます。
- **ポインタ**: 未回収の印をクリック → その痕跡。`is-live` の帯のどこかをクリック → **その帯で一番古い未回収の痕跡**（`s.traceIndex` は最初の 1 件だけ記録、`hall.js:194-198`）。右上のボタン、出立ボタン。一覧と痕跡の箱はホイールとドラッグでスクロール。
- **キー**（`hall.js:408-424`、`busy` 中は受けない）: `→` / `←` で痕跡を回って選ぶ、`↓` / `↑` で候補の一覧を 84 px スクロール、`PageDown` / `PageUp` で `max(84, 見えている高さ − 84)` px、`Enter` で出立、`Esc` は押しはじめだけ設定（`uiClick`）、`J` / `j` は押しはじめだけ手記（`onJournal` があるとき）。
- **キー案内の更新**: 中身を作り直すたびに `HallKeysFor(痕跡 2 件以上, 候補一覧があふれている)` で書き直します。

### 3.5 スクロールとふち

一覧と候補の箱は、続きがある側だけを薄くします（`meta.css:414-416`, `:451-453`）。

| 箱 | 下に続く | 上に続く | 両方 |
| --- | --- | --- | --- |
| これまでの踏破者 | 上から 84% まで不透明、下端で 0 | 上端で 0、上から 16% で不透明 | 上端 0、14% で不透明、86% まで不透明、下端 0 |
| 痕跡の候補 | 上から 86% まで不透明、下端で 0 | 上端で 0、上から 14% で不透明 | 上端 0、12% で不透明、88% まで不透明、下端 0 |

判定は「中身 − 見えている高さ > 2 px」を「続きがある」とし、スクロール位置との差が 2 px 以上で各側を付けます（`hall.js:145-151`, `:265-271`）。

### 3.6 画面側の小さな規則（純粋関数、`hall.js:62-69`）

`pastTraceLine(lineage, life)`: その生の痕跡が回収済み（`CollectedBy` が数）なら、view の「痕跡：第 g 代 ＜名＞が受け取った」を `MetaText.TraceCollected(g, 名)`「…が回収した」に置き換えます。訪れただけで回収になる規則（INHERITANCE R-10、試作 5）に合わせるためです。それ以外は view の文のままです。テストは `test/meta/traceLines.test.mjs:65-75`。

### 3.7 読むデータとコールバック

- `ctx: { stage, audio, settings, lineage, seed?, onDepart(lifeStart|null), onSettings(), onJournal?(), onTitle?() }`
- `hallView(lineage)`（`Inheritance.js:1012-1031`）: `Title` `Lead` `Empty` `NextHeirHeading` `NextHeir`（`heirView`: `Generation` `GenerationLabel` `Name` `Title` `Line` `Tier` `TierName` `TierReason` `Attire[{Part, Text, Color|null}]`）`PastLivesHeading` `PastLives[]`（`pastLifeView`: `Generation` `GenerationLabel` `Name` `Line` `TierName` `Ending` `EndingLabel` `EndingLine` `PlaceLine` `TraceLine`、新しい順）`TraceHeading` `NoTraces` `Traces[]`（`traceView`: `Id` `Generation` `HeirLine` `Layer` `NodeId` `PlaceLine` `EndingLabel` `Cleared` `Note` `Visible` `AppearsLine` `CountLine` `Candidates[{Id, Kind, KindLabel, Name, Detail, …}]` `Withheld[{Name, Detail}]`）`TraceRules[]` `SurvivorHeading` `Survivor`（`Generation` `Line` `Lines[]` `DurationLine` `BonusLivesLeft`、無ければ null）`SurvivorSpent`（`Generation` `Line` `Lines[]`、無ければ null）`StartButton`。
- 系譜を直接読む所: `lineage.Lives[]`（`Generation` `Name` `Layer` `Ending` `TraceId` `CollectedBy`）で印を置く、`lineage.Traces[]`（`Id` `Ending`）で札のアイコン、`lineage.Player.CarvingTaken` で層の名前、`lineage.Current` で出立時の `startLife` 要否。
- 書き込みはしません（R-12: 継承の間では痕跡を受け取らない）。`startLife` は新しい系譜を返すだけで、保存は router です。

---

## 4. 終わりのカード（`ending.js`）

### 4.1 要素と配置

根のクラスは死（倒れた・瘴気死）なら `is-death`、生（生存ルート・踏破）なら `is-alive` です。`layer = ctx.layer || 最後の生の Layer || 7`。

| 要素 | 位置・大きさ | 見た目 |
| --- | --- | --- |
| 背景 | 全面 | 1.7 節（その層、ベール、減光 .75） |
| 光 `ending-glow` | 340,70 1240×940 | 楕円の放射（closest-side）。生は wick 26% → 透明、死は violet 22% → 透明。不透明度は毎フレーム、死 `0.35 + 0.25k`（周期 6200）、生 `0.6 + 0.25k`（周期 3800）、`k = 0.5-0.5cos(2π·now/周期)` |
| 場所の見出し | 0,64 幅 1920、高さ 26 | 中央揃え、間 24。左右に 120×1 の線（ink 0% → ink 55%、右の線は左右反転）、中央に `LayerTitle(layer, 層の名前)`、Shippori 700 26px/1 `.2em` ink、影 `0 2px 14px ground 90%`。実測: 線 653.2 と 1146.8、文字 797.2（325.5 幅） |
| 紙 `ending-sheet` | x 440、幅 1040、高さは中身（最小 560）、y = `max(40, round((1080 − 高さ)/2))` | 和紙（1.5 節）、padding 64 92 52、縦並び間 16、角 3。影 `0 34px 80px ground 85%`、内側に `0 0 0 1px washi-ink 10%` と `0 0 60px washi-ink 7%`。回転の中心は (50%, 40%)。最後の傾きは死 −0.4°、生 +0.3° |
| 頭（横並び、縦中央、間 24） | 中身の左上 = 紙の左上 + (92, 64) | — |
| ・印 | 72×72 | 丸、枠 2px washi-ink、中にアイコン 40px washi-ink（瘴気死 miasma、踏破 star、他は person） |
| ・題 | 印の右 +24、高さ 72 | `EndingTitle`、Shippori 800 72px/1、字間 `.32em`、1 字ずつ別の箱 |
| 主の行 `HeirLine` | 頭の下 +16、さらに上 margin 8 | Shippori 700 26px/1.3 `.1em` |
| 一文 `Sentence` | +16 | Shippori 400 34px/1.6 `.06em`、1 字ずつ別の箱（空白もそのまま 1 つの箱）。字ごとに改行できる |
| 横線 | +16 + margin 8、高さ 2 | washi-ink 28%、中心は左端 |
| 行の一覧 | +8 +16、行の間 12 | 各行は baseline 揃え、間 16、Zen 400 22px/1.5。行頭の点は 8×8 の丸 washi-ink を 4px 持ち上げる |
| 下段（footer） | 紙の下端に寄せる（`margin-top:auto`） | 上 padding 24、上に 1px washi-ink 22%、左右に振り分け（縦中央、間 24） |
| ・次の踏破者 `NextLine` | 左 | Shippori 700 26px/1.3 `.08em` |
| ・ボタン | 右、300×62 | washi-btn（1.5 節）、`'継承の間へ'` + 小札「Enter」 |
| 幕 | 全面 | ground、z 50 |

傾きを外して測った縦位置（紙の上端 T から、`flat.mjs`）: 頭 T+64（72）、主の行 T+160（33.8）、一文 T+209.8（54.4）、横線 T+288.2（2）、行の一覧 T+314.2（1 行 33、間 12）、footer は下端から: 線 = T+H−52−87、ボタン = T+H−52−62。行 3 つのとき H = 592.2・T = 244、行 2 つなら最小の 560・T = 260 です。中身の横位置は x 532〜1388（幅 856）、ボタンは x 1088。

### 4.2 動き（`ending.js:94-120`）

`nT` は題の字数、`nS` は一文の字数（空白を含む）、`nL` は行数です。`t1 = 800 + nT×140 + 200`、`t2 = t1 + 200 + nS×38 + 200`、`t3 = t2 + 300 + nL×260 + 200`。倒れた（3 字・14 字・3 行）なら t1 1420、t2 2352、t3 3632、ボタンは 3932 開始です。

| 対象 | 変化 | 長さ | ease | 遅延 |
| --- | --- | --- | --- | --- |
| 幕 | o 1→0 | 900 | out | 0 |
| 場所の見出し | o 0→1 | 1400 | out | 200 |
| 紙 | o 0→1、y +26→0、s 0.985→1、r 死 −0.8°→−0.4°／生 0.6°→0.3° | 900 | out | 300 |
| 音 | 倒れた `lifeEndDeath`、生存ルート・踏破 `survived`、瘴気死は無音 | — | — | 300 |
| 印 | o 0→1、s 0.8→1 | 700 | out | 700 |
| 題の 1 字ずつ | o 0→1、y +10→0 | 520 | out | 800 + i×140 |
| 主の行 | o 0→1 | 600 | out | t1 |
| 一文の 1 字ずつ | o 0→1 | 260 | out | t1 + 200 + i×38 |
| 横線 | o 0→1、s 0.2→1（中心は左端、縦横とも） | 700 | out | t2 |
| 行 i | o 0→1、x −10→0 | 480 | out | t2 + 300 + i×260 |
| 次の踏破者 | o 0→1 | 600 | out | t3 |
| ボタン | o 0→1、y +8→0 | 500 | out | t3 + 300 |

### 4.3 操作

- **途中で飛ばす** `showAll()`（`ending.js:132-138`）: ボタンがまだ出きっていない間（`button.style.opacity < 1`）に、ボタン以外のどこかを押す（pointerdown）か、`Enter` `Space` `Esc` のどれかを押すと、時計を止めて全部を不透明・transform なしにし、背景と光だけ動く新しい時計に替えます。
- **継承の間へ** `toHall()`（`ending.js:139-148`）: 出きった後の `Enter` / `Space`、またはボタンのクリック。`uiConfirm`、紙を o →0・y →−16（420 ms in）、幕 → 1（600 ms in、遅延 150）、閉じたら `onHall()`。出きった後の `Esc` は何もしません。
- **JS の癖**: `showAll` と `toHall` は紙の transform を消すので、飛ばした後と退場の瞬間に傾きが 0° に戻ります。意図した見た目ではないので、Unity では傾き（−0.4° / +0.3°）を残すことを勧めます（8 章）。

### 4.4 画面側の小さな規則（純粋関数、`ending.js:23-40` `withWaitingTrace`）

同じ層に古い未回収の痕跡があると、その層では古い方しか現れません（INHERITANCE 試作 4）。最後の生が死んで痕跡を残し、その層に先客がいるとき、`endingView` の「次の踏破者が第 n 層を訪れると、3 つまで選んで受け取れる」を `TraceWaits`（先客 1 件、その主の名前）または `TraceWaitsMany`（2 件以上、件数）に置き換えます。踏破した生の札の痕跡なら、`ClearedTraceLeft(...)` の行の直後に同じ待ちの行を差し込みます。先客が `traceForLayer` で一番に立つ痕跡かどうかで判定します。テストは `test/meta/traceLines.test.mjs:21-63`。

### 4.5 読むデータとコールバック

- `ctx: { stage, audio, ending, lineage?, layer?, onHall() }`。`ending` に系譜そのものが来たら `endingView` を通します。
- `endingView(lineage)`（`Inheritance.js:1076-1111`）: `Ending` `Title` `Sentence` `HeirLine` `Lines[]` `NextLine` `Button`。
- 系譜を直接読む所: 最後の生（`Layer` `TraceId`）、`lineage.Traces`、`lineage.Current`、`lineage.Player.CarvingTaken`、`traceForLayer`、`SevenLayers.Of(layer).DisplayName(...)`、`InheritanceText.TraceVisit` / `ClearedTraceLeft` / `LayerTitle`。

---

## 5. 3 画面が読む継承の規則（`Inheritance.js`）

C# の実体はありません（`docs/INHERITANCE.md:5`、issue #104 / #106 / #108 が持ち主）。JS は文書から設計したもので、規則の番号 R-01〜R-27 と「試作で決めたこと」1〜16 が INHERITANCE.md にあります。画面に関わる要点は次のとおりです。

- **系譜**（`newLineage` `:550`）は凍結した JSON 値: `Version` `Seed`（10 進文字列の ulong）`Lives[]` `Current` `Traces[]` `Survivor` `Player{CarvingTaken, RootDefeated}`。
- **名前**（`heirName` `:258`）: 40 名の配列を、系譜の seed から `mix(seed, 0x5A17+cycle) = (seed×0x9E3779B97F4A7C15 + salt) mod 2^64` の `SeededRng` で `RngExtensions.Shuffle` します。周回の頭が前の周回の最後と同じなら先頭 2 つを入れ替えます。C# の `DungeonCore/IRng.cs` の `SeededRng` と `Shuffle`（`:82`）が同じ実装なので、ulong の unchecked 演算で同じ名前が出ます。
- **装束**（`AttireTiers` `:90-96`、`tierAfter` `:273`）: 前の生の到達層で段 0〜3（`TierByLayer [0,0,1,1,2,2,3,3]`）、歪みの根を倒したら 4。色見本の色もここにあります。
- **生存者ボーナス**: ツール枠 4（`RunLoadout.SurvivorBonusToolSlots`）、デッキ上限 45、次の 1 生（裏ボスで 2 生）。
- **view-model**: `heirView` `traceView` `survivorView` `survivorSpentView` `pastLifeView` `hallView` `traceNodeView` `pickState` `endingView`（`:904-1111`）。文言は `InheritanceText`（`:99-185`）。
- **保存**: `toJSON` / `fromJSON`（検査付き）/ `serialize` / `deserialize`（`:1118-1233`）。

---

## 6. Unity 側の現状と再利用できるもの

**3 画面の対応物はありません。** `Depiction/DemoFlow.cs`（デッキ → 出立 → モード → 戦闘の連続 → 結果）と `Depiction/DemoScreens.cs`（`ModeSelectScreen` `SurrenderButton` `EndScreen`）は戦闘デモの流れで、タイトル・継承の間・生の終わりのカードは持っていません。`EndScreen` は戦闘の勝敗の画面です。C# 全体を `Lineage` `Inheritance` `継承の間` で検索しても該当はありません。

再利用できるもの:

- `Assets/View/Backdrop/Script/`（`Backdrop.Script` asmdef、エンジン非依存）: `BackdropSpec`（`For` `Mix` `HazeAlphaAt` `SkyAt`、`Rgba.Hex/Bytes/WithAlpha`）、`BackdropMotion`（`FlickerAlpha` `MoteAt` `MoteAlpha`）、`ExplorationBackdropSpec.For(layer).Veil`。値は JS と同じです（JS が C# の 1:1 移植）。
- `Assets/View/Backdrop/BackdropLayers.cs`: 描き方の参考だけです。ベール・周辺減光・全体 alpha・粒の速さが無く、粒の大きさも JS と違います（1.7 節）。Depiction / Exploration の画面が使っているので触らず、Trial 側に別の描き手を作ります。
- `DungeonCore/SevenLayers.cs:59` `DisplayName(bool)`、`:84` `Of(layer)`、`DungeonCore/RunLoadout.cs:39` `SurvivorBonusToolSlots`、`BattleCore/Constants.cs:27-28` `DeckMin` `DeckMax`、`DungeonCore/ExplorationState.cs` `RunPhase`、`DungeonCore/IRng.cs`。継承の C# 移植がそのまま使えます。
- `Exploration/Script/ExplorationScreenBuilder.cs:426-435` の 4 つの終わりの文は `InheritanceText.EndingSentence` と同じ文言です（R-24）。テストで突き合わせられます。
- `UiKit.Rect` / `Point`（`UiKit.cs:54-80`）の組み立て方と、`UiTween.Apply`（イージング 3 種）の式。`ProceduralArt` と `Depiction/VisualArt.cs` のテクスチャで作る角丸・影は、メッシュの Kit に置き換える方針なので参考だけです。
- フォント: `Resources/Fonts` に Bold 系はありますが、Shippori 400 / 800 と Plex 400 が足りません（1.2 節）。

---

## 7. C# のファイル分割と Kit への要求

### 7.1 置き場所と名前空間

- 画面: `unity-project-kit/Assets/View/Trial/Meta/`、名前空間 `Trial.Meta`。JS の 1 モジュールを 1 ファイルにし、関数名は PascalCase で同じにします（`mountTitle` → `TitleScene.Mount`、`leave` → `Leave`、`breathe` → `Breathe`、`drawTrace` → `DrawTrace`、`withWaitingTrace` → `MetaLines.WithWaitingTrace` など）。
- 画面の純粋な部分: `Assets/View/Trial/Meta/Script/`、名前空間 `Trial.Meta.Script`、エンジン非依存の asmdef（参照: `Backdrop.Script`、継承のコア、`DungeonCore`）。`dotnet test` 用の csproj を `unity-port/Trial.Meta.Script(.Tests)` に置くのは既存（`Depiction.Script` 等）と同じ形です。
- 継承の規則: コアの 1 つとして `unity-port/InheritanceCore/`（名前空間 `InheritanceCore`、静的クラス `Inheritance`）を勧めます。DungeonCore と同じく `npm run unity:sync` で写します。置き場所は architect が決めてください。

### 7.2 実装の単位（unit）

| # | unit | ファイル | 行数の目安 | 依存 |
| --- | --- | --- | --- | --- |
| 1 | 継承のコア（規則・文言・view-model） | `InheritanceCore/Inheritance.cs`（系譜の記録・`StartLife` `RecordDeath` `RecordSurvival` `RecordEnd` `TakeTrace` `NoteCarving` `MapSpecFor` `TraceForLayer` `HeirName` `TierAfter` `MergeMastery` `HeirJournal` 等）、`InheritanceText.cs`、`InheritanceViews.cs`（`HallView` `HeirView` `TraceView` `SurvivorView` `SurvivorSpentView` `PastLifeView` `TraceNodeView` `PickState` `EndingView`）、`Lineage.cs`（不変の record 型） | 1500 | DungeonCore、BattleCore（Constants） |
| 2 | 継承の保存とテスト | `InheritanceCore/LineageJson.cs`（`ToJson` `FromJson` の検査込み。Unity に Newtonsoft が無いので手書きか System.Text.Json 相当の小さな読み書き）、`InheritanceCore.Tests/*`（`test/inheritance/Inheritance.test.mjs` 680 行の移植、R-xx ごと） | 1300 | unit 1 |
| 3 | meta の純粋部分 | `Trial/Meta/Script/MetaText.cs`（text.js 全部）、`MetaLines.cs`（`PastTraceLine` `WithWaitingTrace`）、`HallBackdropSpec.cs`（1.7 節の部屋）、`MetaTimelines.cs`（`SealAt(n)`、終わりの `t1/t2/t3` などの時刻計算）、`TitleMenu.cs`（項目・選べる条件・最初の選択・上下移動・決定の分岐）と `Trial.Meta.Script.Tests`（traceLines.test.mjs の移植、時刻計算、メニュー） | 900 | unit 1、Backdrop.Script |
| 4 | meta の共通ビュー | `Trial/Meta/MetaUtil.cs`（util.js: `SceneRoot` `SoundOf` `Styler` `RiseIn` `RollNumber` `ModalSwallow` `Icon`）、`MetaBackdrop.cs`（backdrop.js: 1.7 節の 5 段階を Kit の Graphic で）、`WashiTile.cs`（`ApplyWashi` の繊維タイル）、`MetaStyle.cs`（1.5 節の chip-btn・primary・washi・washi-btn・keys・見出しの組み立て） | 1200 | Kit、unit 3 |
| 5 | タイトルと終わりのカード | `Trial/Meta/TitleScene.cs`、`Trial/Meta/EndingScene.cs` | 1300 | unit 4、Kit |
| 6 | 継承の間 | `Trial/Meta/HallScene.cs`（`Mount`、見出し、右上、時間割、退場、キー）、`Trial/Meta/HallParts.cs`（踏破者のカード、七層の柱と印と炎、痕跡の箱、次の踏破者の箱） | 1700 | unit 4、Kit |

継承のコアを別の分析担当（core / app）がすでに持つなら、unit 1・2 はそちらに寄せてください。3 画面はこの unit が無いと何も描けません。

### 7.3 Kit への要求

1. **フォント**: `ShipporiMincho-Regular.ttf`（400）、`ShipporiMincho-ExtraBold.ttf`（800）、`IBMPlexMono-Regular.ttf`（400）を Resources/Fonts に足し、太さごとに TMP の動的 SDF アセットを作る。字間は `characterSpacing = em×100`。
2. **文字の影**: CSS の `text-shadow`（ぼかし 12〜34 px、色の不透明度 30〜90%）。SDF の underlay は padding で上限が決まるので、大きいものは文字の外形に合わせた柔らかい矩形を後ろに置く方法も用意する。
3. **1 字ずつの動き**: TMP の頂点を字ごとに動かす部品（不透明度・x/y・拡大）。タイトルの縦書き 24 字、襲名の名前、終わりの題と一文で使う。字は CSS の inline-block と同じく「字幅 + 字間」で並ぶ。
4. **縦書きの列**: 1 字 1 行、送り = 文字サイズ ×(1 + 字間)、句読点「、」「。」は右上へ寄せる（約 +0.6em 右、−0.6em 上。`out/crop-tagline.png` で確認できる）。
5. **イージング**: JS の `out` `in` `inout` `linear` `smooth` に加え、CSS の `ease` と `ease-out` の cubic-bezier。
6. **場面の時計**: 遅延付き tween、完了の通知（await かコールバック）、`Wait(ms)`、要素とプロパティごとに 1 本だけ走るチャネル、`Stop()` で全部を完了扱い、毎フレームの絶対時刻 ms、視差を減らす設定で 1 ms に縮める切り替え。
7. **Styler**: RectTransform と CanvasGroup の `o x y s r` をまとめた代理。中心は CSS の transform-origin に合わせて pivot を選ぶ。
8. **ベクターの形**: 角丸矩形の塗り・枠（1 / 2 / 3 px）・破線の枠（印、色見本）・左だけの太い枠（カード 3px、注記 2px、破線 2px）、円の塗りと枠（破線を含む）、多 stop の線形グラデーション（縦・横）、楕円の放射グラデーション（closest-side）、box-shadow（外側のぼかし・広がり・y オフセット、内側のぼかし、0 0 0 1px / 2px の輪）、それぞれ縁をなめらかにする。
9. **SVG パス**: アイコン（24 単位、線幅 2.2、丸い端と角、サイズに合わせて拡大）と人影 `Figure('swordsman')`（12 本の塗り + 線幅 4 の丸い角の縁取り）。使うアイコンは miasma / person / star / journal / stairs / plus / back。
10. **スクロール**: ScrollRect、ホイール、ドラッグ、px 指定のスクロール、8 px のスクロールバー、続きがある側だけ薄くするふち（RectMask2D の softness と padding を側ごとに切り替える形でよい）。
11. **並べ方**: CSS flex の小さな部分集合（縦横、gap、padding、`margin-left:auto`、`margin-top:auto`、space-between、中央と baseline 揃え）と、`136px | 残り | 中身幅` の 3 列。TMP の推奨サイズで高さを先に計算し、終わりの紙のように中身の高さで位置が決まる箱を置けること。
12. **日本語の改行**: 文節で折る（`auto-phrase` 相当）と 2 行を揃える（`balance`）。最低でも禁則（行頭に「、。）」を置かない）。
13. **ツールチップ**: 七層の印にポインタを置いたときの説明（HTML の `title` 相当）。
14. **キーの積み重ね**: 上のハンドラが先に受けて飲み込む、押しっぱなしの判定、オーバーレイが開いている間は下の画面に届かない。
15. **音**: `Play(name, delayMs)` と環境音の切り替え（`title` `hall`）。9 つのプリセットは 1.4 節。

---

## 8. 食い違いと決めておくこと

- **継承の規則が C# に無い**: 3 画面の前提です。JS の `Inheritance.js` を移植し、決まった C# が後から来たらそちらが勝ちます（INHERITANCE.md の方針）。既定の動きは変わらない（既存の戦闘・探索は系譜を知らない）ので、Battle.unity には影響しません。
- **フォントの太さ**: 400 / 800 が無いと、縦書きと終わりの一文と大きな見出しの印象が変わります。
- **文字の寸法**: Chrome と TMP では行の高さの取り方と baseline の位置が違います。0 章の JSON を正解として、要素の左上を合わせてください。
- **文節の改行と balance**: TMP に無いので、規則の箱や理由の 2 行の折れ方が変わりやすい箇所です。
- **大きな文字の影**: SDF の underlay だけでは 30 px 級のぼかしが出にくいです。
- **縦書きの句読点**: TMP は縦書き用の字形を使わないので、手でずらします。
- **終わりのカードの傾き**: JS は飛ばしたときと退場の瞬間に 0° へ戻ります。傾きを残すのを勧めますが、1:1 を優先するなら JS どおりにします。
- **同じ要素への tween の重なり**: JS は代理ごとに別なので重なることがあります。C# はチャネルで 1 本にします。
- **スクロールバー**: 実機の Chrome では一覧があふれると 8 px のバーが出てカードが 426 幅になります。Unity でも同じにするかを決めてください。
- **手記ボタン**: hall.js は `onJournal` を受けますが、router は渡していません。Unity でも router に合わせて出さないのが JS どおりです。
- **背景の時刻**: JS はページの時刻、Unity は `Time.unscaledTimeAsDouble` です。周期の位相がずれるだけで、見た目は同じです。
