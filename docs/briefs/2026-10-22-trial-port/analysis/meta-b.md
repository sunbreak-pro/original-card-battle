# meta-b: 出立・所持カード・設定・手記の Unity 移植計画

担当範囲は、ブラウザ試遊版の出立画面（デッキを組む → 出立の支度）、所持カードの重ね画面、設定・セーブの重ね画面、手記のドロワーと、それらが読むブリッジ（DeckBuilder / DepartureBuilder / DepartureSupplies / CardFaceText / journal の 3 本 / app/intel.js）です。見た目と動きは JS が勝ち、規則は C# が勝ちます。JS にしか無い規則は C# へ opt-in の拡張として移します。

パスの略記は次の 3 つだけを使います。

- `BG` = `C:\Users\user\AppData\Local\Temp\claude\C--Users-user-orca-original-card-battle\1ab86662-d6cc-4831-aa8c-76f9592e0ca6\scratchpad\proto\prototypes\browser-game`
- `ART` = `C:\Users\user\AppData\Local\Temp\claude\C--Users-user-orca-original-card-battle\1ab86662-d6cc-4831-aa8c-76f9592e0ca6\scratchpad\artifact`（`pretty/ocb.css` は DS の整形済み CSS、`ocb.js` は DS の JS）
- `KIT` = `C:\Users\user\orca\workspaces\original-card-battle\trial\unity-port\unity-project-kit\Assets\View`

座標はすべて 1920×1080 のステージ px で、左上原点・下向き y です（CSS の `left/top`）。uGUI では左上アンカー・pivot (0,1)・`anchoredPosition = (x, -y)` に直します。

---

## 0. 共通の約束（4 画面すべてに効くもの）

### 0.1 トークンと色の混ぜ方

トークンは `ART/tokens.css` の 1 行目と 4 行目にあります。この範囲で使う値は次のとおりです。

- `--ground #10141d` / `--panel rgba(16,20,30,.88)` / `--panel-solid #141925` / `--card #171c28`
- `--ink #ecebf2` / `--ink-muted #a9adbd` / `--rule rgba(157,166,186,.26)` / `--steel #6d7787`
- `--amber #e8a84c` / `--wick #ffd89a` / `--violet #8b6fd0` / `--phosphor #b9a3ff` / `--guard #8fc4d8` / `--crimson #c2483f` / `--hp-text #e36a5e`
- `--washi #f0e6d0` / `--washi-ink #1d1a16` / `--floor-tile rgba(10,16,22,.72)`
- `--shadow-card 0 6px 14px rgba(0,0,0,.45)` / `--shadow-tip 0 8px 18px rgba(0,0,0,.45)` / `--shadow-reach 0 0 22px rgba(232,168,76,.5)` / `--shadow-lamp 0 0 0 2px #ffd89a, 0 0 20px 5px rgba(255,216,154,.5)`
- `--radius-panel 6px` / `--radius-tag 4px` / `--stroke-rule 1px` / `--stroke-frame 2px` / `--space-8/12/16/24`

CSS の `color-mix(in srgb, A p%, B)` は、アルファ付きの事前乗算で混ぜます。`B = transparent` のときは「A のアルファを p に」と同じです。Kit に `TrialColor.Mix(a, p, b)` を置き、次の値で単体テストしてください（手計算の検算値です）。

| 式 | 結果 |
| --- | --- |
| amber 16% / panel | rgba(54,46,38,.899) |
| amber 22% / panel | rgba(68,56,41,.906) |
| amber 12% / panel | rgba(45,40,36,.894) |
| amber 20% / panel | rgba(64,53,40,.904) |
| wick 10% / panel | rgba(43,42,44,.892) |
| ink 8% / panel-solid | #252a35（不透明） |

### 0.2 フォント

ブラウザは Google Fonts から `IBM Plex Mono 400/600/700`、`Shippori Mincho 400/700/800`、`Zen Kaku Gothic New 400/700` を読みます。`KIT/Resources/Fonts` には ShipporiMincho-Bold / ZenKakuGothicNew-Regular・Bold / IBMPlexMono-Bold・SemiBold しかありません。この範囲では次の 3 つが足りません。

- Shippori Mincho 400: 詳細のフレーバー行（`.is-flavour`）、手記のメモ用紙、走り書き
- Shippori Mincho 800: 手記の見出し「手記」（800 36px）
- IBM Plex Mono 400: 主ボタンと設定チップの小さな「Enter」「Esc」

CSS の `letter-spacing: .06em` は TMP の `characterSpacing = 6`（em/100 単位）に当たります。`line-height` は TMP の行間へ写す換算が要るので、Kit の文字ヘルパーで px 指定を受けてください。

### 0.3 時計とトゥイーン（`BG/src/scenes/common/stage.js:80-131`、`BG/src/scenes/meta/util.js:34-64`）

- 1 画面に 1 つの時計を持ちます（`sceneClock`）。戦闘の速さは掛かりません。重ね画面（設定・所持カード・手記）も自分の時計を持ちます。
- `clock.tween(obj, to, ms, ease, delay)` は、同じ `obj` の同じキーへの古いトゥイーンを取り消します（キーが全部消えた古いトゥイーンはその場で完了扱い）。追加と取り消しが重なる個数表示の拡大で効くので、この置き換えの意味を必ず再現します。開始値は「遅延が明けた最初のフレーム」で読みます。
- イージングは `out = 1-(1-t)^3`、`in = t^3`、`inout`、`linear`、`smooth = t²(3-2t)` です。
- `styler(el, {o,x,y,s,r})` は不透明度と `translate(x,y) scale(s) rotate(r)` です。uGUI では CanvasGroup.alpha と、配置位置に足すオフセット・localScale に写します。
- `rollNumber` は整数表示の数を 260ms `out` で転がします（`util.js:51-64`）。
- 1 フレームの dt は最大 50ms で切ります。
- CSS の `transition` と `@keyframes` には CSS の三次ベジエを使います。`ease = (.25,.1,.25,1)`、`ease-out = (0,0,.58,1)`、`ease-in = (.42,0,1,1)` です。上の JS の `out` とは別物なので混ぜないでください。

### 0.4 キー（`stage.js:133-145`、`util.js:116-119`）

- キーは積み重ね式です。上の画面のハンドラが先に受け、true を返すと下へ渡りません。
- 重ね画面は `modalSwallow` で、1 文字のキー・矢印・Enter・Tab・PgUp・PgDn を下へ渡しません。
- 押しっぱなしの自動反復（`e.repeat`）を区別します。Esc・J・C は新しく押したときだけ開閉し、矢印と + − は反復で動き続けます。Input System には OS の自動反復が無いので、Kit で「最初 500ms、以後 33ms ごと」の合成反復を作り、`repeat = true` を付けて流してください。
- 文字キーは `Keyboard.onTextInput` の文字で見ます。出立の ＋ は `'+' '=' ';'`、− は `'-' '_'` と Backspace・Delete です。JIS 配列の ＋ は Shift+; なので `;` も受けます。

### 0.5 重ね画面の土台

ルーター（`BG/src/app/main.js:73-108`）は、重ね画面を z 500 の別レイヤーに載せます。閉じたら破棄し、下の画面の `refresh()` を呼びます。設定は `canSaveQuit` と `saveQuitKind` を受けます。出立と継承の間からは `LINEAGE_SAVE = { canSaveQuit: true, saveQuitKind: 'lineage' }` です（`main.js:71`）。

### 0.6 音

この範囲で鳴らすプリセットは次の 10 個です（`BG/src/scenes/audio/Audio.js:58-82`、`ART/ocb.js:688`）。合成は音の担当の作業ですが、名前と `gap`（連打の間引き）はこの一覧に合わせてください。

| 名前 | 音色 | vol | pitch | dur | gap |
| --- | --- | --- | --- | --- | --- |
| uiClick | wood | .07 | 760 | 30 | 40 |
| nodeHover | paper | .035 | 2600 | 22 | 60 |
| uiConfirm | seal | .10 | 170 | 110 | — |
| uiCancel | slide | .08 | 620 | 110 | — |
| refusal | wood | .09 | 300 | 34 | — |
| cardAdded | tuck | .12 | 1100 | 220 | — |
| rewardReveal | slide | .11 | 1000 | 260 | — |
| consumableUse | pouch | .12 | 1200 | 260 | — |
| journalOpen | page | .12 | 1500 | 280 | — |
| journalClose | close | .10 | 1000 | 200 | — |

### 0.7 共通のボタンの見た目（`BG/src/scenes/meta/meta.css:10-38`）

- すべてのボタンは Tab で選ばれたときだけ、2px の `--wick` の輪郭を 3px 外側に描きます（`:focus-visible`）。無効のボタンはカーソルが矢印のままです。
- `.ocbg-meta-primary`（主ボタン）は枠 2px amber・角 6・地 amber 16%・Shippori 700 28px・字間 .2em・中央揃え・間 16 です。中の小札 `small` は Plex Mono 400 16px・字間 .04em・内余白 4/8・枠 1px rule・角 4 です。ホバーで地 amber 28%・`--shadow-reach`・上へ 2px、無効は不透明度 .32 です。遷移は背景・影・位置 140ms `ease`、不透明度 200ms です。`.is-compact` は高さ 56・左右余白 28・22px です。
- `.ocbg-meta-btn`（普通のボタン）は地 `--panel`・枠 1px steel・角 6・Zen 700 20px・字間 .06em・左右余白 24・高さは親いっぱいです。ホバーで枠 ink・地 ink 8%/panel-solid・上へ 1px（140ms）です。
- `.ocbg-meta-chip-btn`（右上の設定チップ）は地 panel・枠 1px rule・角 6・Zen 700 20px・間 8 で、`small` は Plex Mono 400 16px です。ホバーで枠 ink・上へ 1px です。`.is-square` は余白 0 です。
- `.ocbg-meta-x`（閉じる ×）は 44×44・枠 1px rule・角 6 で、`exile` アイコン 22px ink を入れます。ホバーで枠 ink です。
- `.ocbg-meta-keys`（操作の案内行）は Zen 400 16px/22px・字間 .06em・折り返し無しです。`.is-right` は右揃えです。
- `.ocbg-meta-fade` は全面の `--ground` で、z 50 です。

---

## 1. 出立（`BG/src/scenes/meta/departure.js`、814 行）

1 つの画面に 2 つの段（デッキを組む / 出立の支度）があり、見出し帯は両方の段の上に載ります。Unity の DeckSelectScreen と DepartureScreen を 1 画面にまとめた形です。

### 1.1 起動時の状態（`departure.js:103-159`）

- `owned` は `ctx.owned` が null なら全カタログ、空なら `Expedition.InitialKinds`、それ以外はその一覧です。どの場合も `DeckBuilder.WithPrototypeKinds` で試作デッキの種類を足します（`:109-110`）。ルーターは `InitialKinds ∪ 試作デッキの種類` を渡します（`main.js:280`）。
- `limits = DeckLimits.From(ctx.lifeStart)` です。生存者ボーナスがある生ではデッキ上限 45・ツール枠 4、無い生では 20〜40・3＋3 です。
- 背景は `createBackdrop(root, hallBackdropSpec(root, 0.5), { vignette: 0.6 })` で、毎フレーム `backdrop.paint(now)` を呼びます（`:116-117`）。継承の間と同じ背景を強さ 0.5 で描きます。描画器は継承の間の担当（meta-a）のものを使います。
- デッキの初期値は次の順に決まります（`:128-140`）。同じ生の下書きがあれば `DeckBuilder.ReadOverCap(draft.deck, limits)`、無くて `ctx.savedDeck` があればそれを `ReadOverCap`、どちらも無ければ `DeckBuilder.Prototype(limits)` です。どれも `WithOwned(owned)` を通します。読めた結果の `value` は通知文です。上限を超えたデッキなら `noticeOverCap = true` にします。
- 支度は `DepartureSupplies.NewBuilder(limits)` で作り、下書きのツール・消耗品・才能を順に戻します（`:141-146`）。
- `randomSeed = (lifeStart.Seed（無ければ ctx.seed、無ければ 1）を整数にして & 0x3fffffff) || 1` です（`:157`）。Seed は巨大な整数の文字列で来ることがあるので、C# では `BigInteger`／`ulong` で読んでから下位 30 ビットを取ります。
- 選択中のカード `selected` が、キーボードのカーソルを兼ねます。

### 1.2 下書き（`departure.js:52-90`、`:148-151`、`:710`）

- 鍵は `ocb.departure.draft.v1` です。中身は JSON `{ id, deck: Save文字列, tools: [...], consumables: [...], talent }` です。
- `id` は `lineageSeed|generation|lifeSeed` で、同じ生のものだけを読み戻します。
- メモリ上の写しを先に見て、次に localStorage、古い版の sessionStorage の順に読みます。読み書きはすべて例外を握りつぶします。
- `refreshDeck` と `refreshSupplies` のたびに書きます。出立したら消し、以後はルーターが `ctx.savedDeck` として持ちます。
- Unity では PlayerPrefs に同じ JSON を置くのが近い動きです。ゲームの記録とは別の、1 台ごとの便利機能として扱います。

### 1.3 見出し帯（両方の段の上、`departure.js:171-198`、`:740-742`）

| 要素 | 位置 x, y, w, h | 見た目 |
| --- | --- | --- |
| 戻るボタン `.ocbg-meta-back` | 64, 24, 内容幅, 52 | 地 panel・枠 1px rule・角 6・左右余白 18・間 8・Zen 700 18px・字間 .06em・折り返し無し。中身は `back` アイコン 22px ink と文字。文字は 1 段目で `InheritanceText.ToHallButton`「継承の間へ」、2 段目で `DepartureBuilder.BackLabel`「デッキに戻る」に替わる |
| 設定チップ | 1752, 24, 120, 52 | `.ocbg-meta-chip-btn`。「設定」＋小札「Esc」。`ctx.onSettings` があるときだけ置く |
| 手記チップ（正方形） | 1680, 24, 56, 52 | `journal` アイコン 30px ink。`ctx.onJournal` があるときだけ置く。ルーターは出立に渡していないので、試遊版では出ない |
| 段の表示 `.ocbg-meta-stepper` | 1236, 24, (右端−1236), 52 | 右寄せ・間 12。右端は x=1872 から、設定があれば 120+16、手記があれば 56、どちらかがあればさらに 24 を引いた値。試遊版は設定だけなので 1712（幅 476） |

戻るボタンのホバーは枠 ink と左へ 2px（140ms）です。段の表示は「〔1〕デッキを組む ── 〔2〕出立の支度」です。各段は Shippori 700 20px・字間 .1em・不透明度 .5（200ms で遷移）、番号の丸は 32×32・枠 2px steel・Plex Mono 600 16px です。今の段は不透明度 1 で、丸の枠が amber、地が amber 18% です。間の線は 56×1 の `--rule` です。

見出し帯は 2 つの段の後に DOM へ足し直すので、ポインターと Tab の順では段の中身より後ろに来ます。

### 1.4 1 段目: デッキを組む（`departure.js:200-507`）

段の箱は (0, 0, 1920, 1080) です。

**題の行**（64, 88, w 1160）は横並び・縦中央・間 24 です。

- 題 `h1` は Shippori 700 32px/1.2・字間 .06em・折り返し無しで、文字は `deck.Title`（例「デッキを組む（80 種から 20〜40 枚、1 種 3 枚まで）」）です。
- 生存者ボーナスの札は `deck.RaisedLine` が空でないときだけ出します（`:165-169`、`meta.css:443-446`）。内余白 7/16・枠 1px wick・角 6・地 wick 10%/panel・Zen 400 18px/1.2 です。中身は、10×10 の wick の点（`0 0 8px wick` の光）、太字 700 18px・字間 .04em の「生存者ボーナス」、`RaisedLine`（例「デッキ上限 +5（40 → 45）」）の 3 つです。

**通知と案内**

- 通知 `p`（1236, 92, w 636）は Zen 400 18px/1.4 ink です。保存デッキが読めなかった理由、または上限超えの理由を出します。
- 案内行（1236, 100, w 636、右揃え）は「←↑↓→ 札を選ぶ　＋ − 枚数　PgUp PgDn ページ　Esc 設定」です（`onSettings` が無ければ末尾は「Esc 戻る」）。通知があるあいだは隠します。
- 通知は、デッキを差し替えたとき、または上限超えのデッキが上限以下に戻ったときに消えます（`:498`、`:502-507`）。

**絞り込みの列**（64, 146, 1140, 50、`:213-233`、`meta.css:165-170`）

- 横並びで間 8 です。各ボタンは高さ 48・左右余白 14・`flex: 1 1 0`（中身より狭くはならない）・地 panel・枠 1px rule・角 6・Zen 700 18px・間 8 です。ホバーで枠 ink、選択中は枠 2px amber・地 amber 16%/panel です。遷移は枠と地に 140ms です。
- 並びは、属性が `DeckBuilder.ShownAttributeOptions`（全て / 攻撃 / 防御 / スキル / スタンス）、12px の空き、コストが `DeckBuilder.CostOptions`（全コスト / コスト 1 / コスト 2 / コスト 3）です。
- 属性ボタンのアイコンは 20px で、攻撃 `sword` phosphor、防御 `shield` guard、スキル `star` amber、スタンス `flag` hp-text です。「全て」にはアイコンがありません。
- コストボタンには 26×26 の丸（枠 2px amber・Plex Mono 700 16px）で数を入れます。「全コスト」には丸がありません。
- 押すと `filter` を変えてページを 0 に戻し、uiClick を鳴らして、絞り込みと格子を描き直します（アニメーションはありません）。
- 幅の配分は CSS の flex をそのまま真似ます。各ボタンの最小幅を「左右余白 28＋アイコン（20＋8）＋文字幅」とし、残りを均等に割り、最小幅を下回るものはその最小幅で止めて残りを割り直します。手計算では、全て・攻撃・防御・全コストが約 106、スキル約 110、スタンス約 128、コスト n 約 131 になります。

**カードの格子**（64, 214, 1140, 800、`:236-281`、`meta.css:172-184`）

- 6 列 × 3 行 = 1 ページ 18 枚です。升は (列×190, 行×266, 182, 262) に置きます。
- 升の中のカード面ボタン `.ocbg-meta-cell-face` は 156×219 で、升の中で横中央（左 13）・上 0 に置き、角 6 です。中身はカード（216×304）を 0.72 倍にしたものです（左上原点で縮めて 156×219 の枠に収める）。
- ホバーで上へ 6px、`0 10px 22px ground 80%` の影を付けます（位置は 140ms `ease-out`、影は 140ms）。ホバーに入るたびに nodeHover を鳴らします。
- 選択中（`.is-sel`）は、面ボタンに `0 0 0 3px amber` と `0 0 20px amber 40%` の 2 重の光を付けます。
- 持っていない種類（`.is-unowned`）は面の不透明度 .4 で、「未所持」の札を升の横中央・上 92 に重ねます（内余白 4/10・地 panel-solid・枠 1px steel・角 4・Zen 700 16px、ポインターを通す）。
- 升の下端の行 `.ocbg-meta-cell-row` は左右 13 内側・下 0・高さ 36 で、両端揃えに「−」「×n」「＋」を並べます。
  - −／＋ は 44×36・地 panel-solid・枠 1px steel・角 4・Zen 700 22px です。ホバーで枠 ink・地 ink 8%/panel-solid（120ms）、無効は不透明度 .28 です。
  - 枚数は最小幅 44・中央・Plex Mono 700 26px amber で、`deck.CountText(id)`（「×2」か空）を出します。
  - ＋ は `deck.CanAdd(id)` が偽のとき無効、− は `CountOf(id) == 0` のとき無効です。
- 面ボタンの左クリックと右クリックは、どちらも `choose(def)`（選択）です。右クリックのメニューは出しません。

**ページ送り**（64, 1016, 1140, 48、`:284-295`）

- 両端揃えで「〈 前のページ」・ページ文字・「次のページ 〉」です。
- ボタン `.ocbg-meta-nav` は高さ 48・左右余白 20・間 8・地 panel・枠 1px rule・角 6・Zen 700 18px で、アイコン `back`／`forward` は 20px ink です。ホバーで枠 ink、無効は不透明度 .3 です。
- ページ文字は Zen 400 20px で、`DeckPage.Text`（例「1 / 5 ページ（80 種）」）です。
- 送ると nodeHover を鳴らし、`drawGrid(±1)` を呼びます。端で送れないときは何もしません。

**カードの詳細**（1236, 146, 636, 452、`:298-324`、`meta.css:193-200`）

- 地 panel・枠 1px rule・角 6 です。
- 未選択なら、上下中央に「カードを押すと、効果の文が出ます。」を Zen 400 20px/1.5 で出します。
- 選択中なら次の 3 つを置きます。
  - 原寸のカード 216×304 を (16, 16) に置きます。
  - 文の列（252, 16, w 368）に `DeckBuilder.ShownDetailOf(def)` を出します。1 行目（名前）は Shippori 700 30px/1.2・字間 .06em・下余白 12 です。残りは Zen 400 18px/1.5・下余白 8 で、最後の行が「「」で始まればフレーバーとして Shippori（400）にします。
  - 用語の列（16, 334, w 604）は `face.Detail.Terms` を「語：意味」で並べます。縦並び・間 4・上余白 12・上罫 1px rule・最大高さ 104 で、あふれたら縦スクロールです。各行は Zen 400 16px/1.4、語は Shippori 700 16px・右余白 10 です。
- 詳細を描くたびに、原寸のカードを不透明度 0・y+10 から 200ms `out` で出します。
- `choose(def)` は同じカードなら何もしません。違えば uiClick を鳴らし、詳細・升・デッキ一覧の印を描き直します。

**枚数の箱**（1236, 614, 636, 150、`:327-342`、`meta.css:202-216`）

- 地 panel・枠 1px rule・角 6・内余白 16/22・縦並び間 12 です。
- 1 行目は横並び・縦中央・間 24 です。
  - 合計は最小幅 128 の箱に、Plex Mono 700 64px amber・最小幅 80・右揃えの数と、Zen 700 24px の「枚」を置きます（ベースライン揃え・間 8）。数は `rollNumber` で 260ms `out` で転がします。
  - ゲージは残りの幅を使い、下に 24 の余白を取ります。溝は高さ 14・角 7・地 ground 70%・枠 1px rule・はみ出しを切ります。中身は幅 `min(100, 100×Total/DeckMax)%` で、地 steel です。デッキが規則を満たすと amber になります。幅は 260ms `ease-out`、色は 200ms で遷移します。
  - 下限の印は `left = 100×DeckMin/DeckMax %` に、上下に 2px はみ出す幅 2 の ink の縦線を置きます。20/40 なら 50%、20/45 なら 44.4% です。
  - 目盛りの数字は溝の下 22px に、Plex Mono 600 16px で下限（中央揃え）と上限（右端に右揃え）を出します。
- 2 行目の状態文 `.ocbg-meta-status` は Zen 400 20px/1.45 で、頭に 12×12 の丸（上 8・枠 2px steel）を付けます。満たすと丸が塗りの amber になり、`0 0 10px amber 60%` で光ります。文は `deck.StatusText` です。

**いまのデッキ**（1236, 776, 636, 128、`:344-429`、`meta.css:217-233`）

- 地 panel・枠 1px rule・角 6・内余白 10/16・縦並び間 6 です。
- 見出し行は高さ 16・ベースライン揃え・間 12 です。「いまのデッキ」（Zen 700 16px・字間 .1em）、種類数「N 種」（Plex Mono 600 16px、`MetaText.OwnedCount`）、右端に続きの案内（Zen 400 16px・字間 .06em）を並べます。
- 続きの案内は、一覧が 3 行を超えるときだけ「Shift+↑↓ 続きを読む」を出します。頭の記号は、下に続きがあれば「▼」、末尾まで読んだら「▲」です（12px amber・右余白 6・上へ 1px）。
- 本体は高さ 84 です。札を折り返して並べ（行の間 6・札の間 8）、縦にスクロールし、スクロールバーは見せません。1 行は札の高さ 24＋間 6 = 30px で、行単位で止まります（scroll-snap）。
- 札（チップ）は高さ 24・内余白 2/8・角 4・地 ink 7%・Zen 400 16px/20px で、文字は `deck.DeckLines()` の「突き ×2」です。ホバーで地 ink 13%、選択中のカードの札は 1px の amber の内枠と地 amber 16% です（140ms）。Tab では選べません（tabindex -1）。
- 端のぼかし: 下に続きがあれば下端 14%（約 12px）、上に続きがあれば上端 14% を透明へぼかし、両方なら上 24%・下 14% をぼかします。
- 札を押すと `reveal(def)` を呼びます。今の絞り込みでそのカードが隠れていれば絞り込みを「全て / 全コスト」に戻し、そのカードを含むページへ格子を送ってから選びます。送り方向のアニメーションは、絞り込みを戻した場合は無し、それ以外は前後の向きです。すでに選択中なら uiClick だけ鳴らして印を描き直します。
- 選択中のカードの札は、必ず一覧の見える範囲へスクロールします（`markChip`、`:393-407`）。
- マウスホイールでもスクロールします（ブラウザ既定で、行単位に吸着）。

**プリセットと進むボタン**

- プリセットの列（1236, 916, 636, 50）は間 12 で、`.ocbg-meta-btn` を 3 つ均等幅で並べます。「試作デッキ」は `DeckBuilder.Prototype(limits)`、「ランダム」は `randomDeck()`、「空にする」は `new DeckBuilder(owned, limits)` です。
- 「出立の支度へ」（1236, 984, 636, 72）は主ボタンで、小札は「Enter」です。デッキが規則を満たさないあいだは無効です。

### 1.5 1 段目の動き・音・状態の流れ

| きっかけ | 状態 | 見た目の動き | 音 |
| --- | --- | --- | --- |
| 入場（`:743-754`） | 先頭の升を選ぶ | 全面の ground を 1→0 に 500ms `out` で消す。升を不透明度 0・y+18 から 380ms `out`、遅延 150＋i×28ms で出す | なし |
| ページ送り | `page±1` | 新しい升を不透明度 0・x＋36×向き から 240ms `out`、遅延（列番号）×18ms | nodeHover |
| ＋（`add`、`:464-475`） | `deck.Add` が通れば選択をそのカードへ移し、詳細を描き直す（uiClick は鳴らさない） | その升の枚数を 1.5 倍から 240ms `out` で 1 倍へ | cardAdded |
| ＋が断られた | なし | ＋ボタンを x = 6, −6, 4, −4, 0 へ各 32ms `linear` で揺らす | refusal |
| −（`remove`） | `deck.Remove` | 枚数を 0.7 倍から 200ms `out` で 1 倍へ | uiCancel |
| プリセット（`replace`、`:457-463`） | デッキを差し替え、通知を消す | 全升の枚数を不透明度 0・1.4 倍から 260ms `out`、遅延 i×12ms | rewardReveal |
| デッキが変わるたび（`refreshDeck`、`:489-501`） | 升・合計・ゲージ・状態文・一覧・印・進むボタン・下書きを更新 | 合計を 260ms で転がし、ゲージ幅を 260ms `ease-out` で動かす | — |

### 1.6 2 段目: 出立の支度（`departure.js:509-658`）

段の箱は (0, 0, 1920, 1080) で、最初は隠れています。

**題と案内**

- 題の行（64, 88, w 1160）は 1 段目と同じ作りで、文字は `supplies.Title`（例「出立の支度（ツール 3 枠・消耗品 3 枠・才能 1 枚）」）、ボーナスの札は `supplies.RaisedLine`（例「ツール枠 +1（3 → 4）」）です。
- 案内行（1236, 100, w 636、右揃え）は「Tab 項目を移る　Space 押す　Esc 設定」です。

**ツールと消耗品**（左の列）

- 見出し `h2` は Zen 700 20px/1.4・字間 .04em・折り返し無しです。ツールの見出し（64, 146, w 1140）は `supplies.ToolHeading`、消耗品の見出し（64, 636）は `supplies.ConsumableHeading` です。
- 枠の列は、ツールが (64, 184, 1140, 64)、消耗品が (64, 674, 1140, 64) です。枠の数だけ札を (i×224, 0, 212, 64) に置きます（`drawSlots`、`:559-573`）。
  - 空の枠は地 ground 55%・枠 2px 破線 steel・角 6・内余白 0/14・間 12 です。中身は番号の丸（26×26・枠 1px steel・Plex Mono 600 16px）と「空き」（Shippori 700 20px/1.2・字間 .04em・あふれは「…」）です。押せません。
  - 埋まった枠は実線の amber の枠・地 amber 12%/panel で、名前と、右端の `exile` アイコン 16px（不透明度 .55、ホバーで 1）を出します。ホバーで地 amber 20%/panel です。押すとその品を外し、uiCancel を鳴らします。
  - ボーナスで増えた枠（`supplies.IsBonusToolSlot(i)`）は、空のあいだ破線の色が wick になり、ツールチップ「生存者ボーナス」を持ちます。
- 品の札の格子は、ツールが (64, 262, 1140, 360)、消耗品が (64, 752, 1140, 270) です。札は 2 列で ((i%2)×580, ⌊i/2⌋×90, 560, 82) に置きます。ツール 8 個で 4 行、消耗品 5 個で 3 行です。
- 品の札 `.ocbg-meta-supply`（`supplyCard`、`:528-548`、`meta.css:245-255`）は横並び・縦中央・間 12・内余白 8/14/8/16・地 panel・枠 1px rule・角 6 です。1 つ以上積んだ品は枠が amber です（140ms）。
  - 左の文の箱は残り幅を使います。上の行（ベースライン揃え・間 10）に、名前（Shippori 700 22px/1.2・字間 .04em）、読み `item.Reading`（Zen 400 16px）、右端の側の札を並べます。側の札は内余白 2/8・枠 1px rule・角 4・Zen 400 16px/1.2 で、戦闘の品は `range` 16px＋「戦闘」、探索の品は `stairs` 16px＋「探索」です（`item.Side == EffectSide.Battle` で決める）。
  - その下の説明文は Zen 400 16px/1.35・上余白 4・2 行で切ります。文は `playerNote(option.Note)` で、設計書の出典「（…md…）」と、「凍結」を含む括弧書きの付いた 1 文を取り除きます（`:43-50` の正規表現 2 つ）。
  - 右に幅 152・間 6 の「− ×n ＋」の行を置きます。部品は格子の升と同じです。
- ＋が通ると consumableUse を鳴らし、札に脈打つ輪を出します（500ms `ease-out`、`box-shadow 0 0 0 0 amber 60%` → `0 0 0 14px amber 0%`、押すたびに最初から）。断られたら refusal を鳴らし、＋を揺らします（1.5 節と同じ揺れ）。− が通ると uiCancel です。

**才能**（右の列）

- 見出し（1236, 146, w 636）は `DepartureBuilder.TalentHeading` で、折り返します。
- 才能の格子（1236, 212, 636, 300）は 3 列 × 6 行で、ボタンを ((i%3)×214, ⌊i/3⌋×50, 204, 42) に置きます。中身は `DepartureBuilder.TalentPage(deck, page, 18)` です。
  - ボタンは内余白 0/12・間 8・地 panel・枠 1px rule・角 6・Zen 700 18px・折り返し無しで、名前があふれたら「…」です。ホバーで枠 ink です。
  - 頭のアイコン（18px）は属性で決めます。攻撃 `sword`、防御 `shield`、スキル `star`、スタンス `flag` で、色は 1.4 節の属性の色です。属性が複数の札は `star` の ink にします。
  - 選んだ札は枠 2px amber・地 amber 22%/panel・`0 0 14px amber 35%` の光で、アイコンの色が `--ground` になります。
  - 押して `supplies.ChooseTalent` が通ると uiConfirm を鳴らします。
- 才能のページ送り（1236, 520, 636, 40）は小さい版（高さ 40・左右余白 14・16px・アイコン 18）です。送ると nodeHover を鳴らします。

**開始の位置の箱**（1236, 576, 636, 324、`:600-636`）

- 地 panel・枠 1px rule・角 6・内余白 14/20 です。上から、見出し「開始の位置」（Zen 700 16px・字間 .1em）、高さ 112 の床の図、5 行の説明の順に流し込みます。
- 床の図（`drawLane`）は 1 マス 88px で、マスの数は `max(6, 敵のマス)` です。`pc = supplies.PlayerStartCell`、`gap = supplies.StartGap`、敵のマスは `pc + gap + 1` です。
  - 各マスは DS の `FloorTile(82)` を ((i−1)×88＋3, 58) に置きます。高さ 40・角 6・地 `--floor-tile`・枠 1px rule です（`ART/pretty/ocb.css:374-398`）。
  - 自分のマスは届く範囲の色（amber .55→.18 の縦グラデーション＋`--shadow-reach`）を不透明度 .65 で、上端の 3px の amber の線を不透明度 1 で出します。
  - 自分は `person` 34px amber を ((pc−1)×88, 12, 88, 44) の下端中央に、敵は同じ形の violet を敵のマスに置きます。
  - 間合いの札は DS の `DistanceChip(gap, gap×88)` を (pc×88, 61) に置きます。幅は `max(100, gap×88)`、高さ 34 です。札は地 panel-solid・枠 1px steel・角 6・Plex Mono 700 28px で、`range` 22px と数を出します。両側に ink 55% の細い括弧の線が伸びます（`ART/ocb.js:487-501`）。
- 説明の 5 行（Zen 400 18px/1.6）は `supplies.TalentLine(deck)`、`supplies.StartLine`、`supplies.ToolsLine`、`supplies.ConsumablesLine`、`MetaText.DeckCount(deck.Total)`「デッキ: N 枚」です。

**状態文とボタン**

- 状態文（1236, 912, w 636）は 1.4 節と同じ作りで、文は `supplies.StatusText(deck)`、満たす条件は `supplies.CanSetOut(deck)` です。
- 「デッキに戻る」（1236, 972, 236, 72）は普通のボタンです。
- 「出立する」（1488, 972, 384, 72）は主ボタンで、小札は「Enter」です。出立できないあいだは無効です。

`refreshSupplies`（`:638-658`）は、札の枚数と ＋−、両方の枠、才能、床の図、説明、状態文、出立ボタン、下書きをまとめて更新します。

### 1.7 段の切り替えと退場（`departure.js:661-735`）

- **1 段目 → 2 段目**（`showSupplies`）: 忙しいとき・規則を満たさないとき・すでに 2 段目のときは何もしません。uiConfirm を鳴らし、才能のページを 0 に戻し、支度を描き直し、段の表示と戻るボタンの文字を替えます。1 段目を不透明度 0・x −80 へ 260ms `in` で、2 段目を不透明度 0・x +80 から 340ms `out`・遅延 140ms で入れます。終わったら 1 段目を隠します。動いているあいだは忙しい扱いです。
- **2 段目 → 1 段目**（`showDeck`）: uiCancel を鳴らし、デッキと格子を描き直します（格子の入場アニメーションは無し）。2 段目を x +80 へ 260ms `in` で、1 段目を x −80 から 340ms `out`・遅延 140ms で入れます。
- **戻る**（`back`）: 2 段目なら `showDeck`、1 段目なら uiCancel を鳴らして退場し、`ctx.onBack()`（継承の間へ）を呼びます。
- **出立する**（`setOut`）: `supplies.Build(deck)` を作り、`DepartureSupplies.Carry(departure, limits)` で `battleToolIds`・`consumableIds`・`loadout` を作り、下書きを消し、uiConfirm を鳴らして退場します。そのあと `ctx.onSetOut({ deck: departure.Deck, deckBuilder, deckSave: deck.Save(), battleToolIds, consumableIds, loadout, talent: departure.TalentCardId, departure, limits })` を呼びます。
- **退場**（`leave`）: 全面の ground を 0→1 に 520ms `in` で重ね、終わったら次の画面へ移ります。
- **設定を開く**: 忙しくなければ uiClick を鳴らし、`ctx.onSettings()` を呼びます。

### 1.8 出立のキー（`departure.js:756-803`）

- Tab は、キーで選んだ印（`tabbed`）を立ててブラウザに任せます。ポインターを押すと印を下ろします。Tab の順は 2 段目の中身、戻る、設定、手記の順です。無効のボタンは飛ばし、デッキ一覧の札も飛ばします。
- 忙しいあいだは何も受けません。
- Esc は、新しく押したときだけ、設定を開きます（設定が無ければ戻る）。
- J は、手記があり新しく押したときだけ手記を開きます。
- Enter は、Tab で選んだボタンがあればそのボタンを押します（ブラウザの既定動作）。
- 1 段目のキー:
  - Shift+↑／↓ でデッキ一覧を 1 行ずつ送ります。動いたら nodeHover です。
  - PgUp／PgDn でページを送ります。
  - ←／→ でカーソル（選択）を 1 つ動かします。ページの端を越えると前後のページへ送り、前へ越えたら最後の升、次へ越えたら最初の升を選びます。
  - ↑／↓ で 6 つ動かします。ページの外へは出ません。
  - ＋（`+ = ;`）でカーソルの札を足し、−（`- _` Backspace Delete）で減らします。
  - Enter は、デッキが規則を満たせば 2 段目へ、満たさなければ refusal を鳴らします。
- 2 段目のキー:
  - ←／PgUp と →／PgDn で才能のページを送ります（送れるときだけ、nodeHover）。
  - Enter は、出立できれば出立し、できなければ refusal を鳴らします。
  - Space は、Tab で選んだボタンを押します（ブラウザの既定動作）。

Unity では Kit のフォーカス管理で、Tab の順・Space と Enter でのボタン押下・選ばれた印の輪郭を作ります（6 節）。

---

## 2. カードの面（`BG/src/scenes/meta/cardFace.js`、101 行）

出立と所持カードのカードは、DS の `OCB.Card`（`ART/ocb.js:197-241`、CSS は `ART/pretty/ocb.css:39-283`）をそのまま使います。戦闘の手札（`BG/src/scenes/battle/hand-card.js` の `buildCard`、Unity の CardView.cs の移植）とは、同じ DS の CSS を使いますが組み立て方が違います。

### 2.1 大きさ

| 使う場所 | 倍率 | 外形 | 置き方 |
| --- | --- | --- | --- |
| 出立の格子 | 0.72 | 156×219（216×.72 と 304×.72 を丸めた枠） | 左上原点で縮め、枠に絶対配置（`scaledCard`、`:85-95`） |
| 所持カードの格子 | 0.68 | 147×207 | 同上 |
| 出立の詳細 | 1.0 | 216×304 | (16, 16) |
| 所持カードの詳細 | 1.0 | 216×304 | (20, 20) |

縮めるのは変形なので、影（`--shadow-card`）も一緒に縮みます。Unity では 216×304 で組んだカードを localScale で縮め、外側に縮めた大きさの枠を置きます。

### 2.2 DS カードの部品（原寸の px）

- 地 `--card`・角 6・影 `--shadow-card` です。属性の色（`--attr`）を左端 8px の帯と上端 5px の帯に塗り、2px の枠線も同じ色です。属性の色は、攻撃 violet、防御 guard、スキル amber、スタンス crimson で、文字の色は、攻撃 phosphor、防御 guard、スキル amber、スタンス hp-text です。
- コストの丸は (16, 11) の 44×44・枠 2px amber・地 rgba(0,0,0,.45)・Plex Mono 700 28px amber です。安くなっているときは地 amber・字 ground で、下に「▼n」の札（高さ 18・地 panel-solid・枠 1px amber・Zen 400 14px）を下端から 9px はみ出して付けます。
- 属性のアイコンは (68, 21) に 24px です。
- 届く範囲の札は右 12・上 11・高さ 32・内余白 0/8・地 panel-solid・枠 1px steel・角 4・Plex Mono 700 20px です。中身は `range` 18px と数、または `person` 22px と「自分」（Zen 400 14px）です。
- 名前は (16, 60)・右 12・Shippori 700 22px/28px・字間 .02em・折り返し無しです。
- 値の列は (16, 94) から横に並べ、間 14 です。1 列は 24px のアイコンと数（Plex Mono 700 36px/40px）、その下に語（Zen 400 16px/18px ink-muted、上へ 2px）です。スタンスの札には値の列を出しません。
- 説明は (16, 156)・188×66・Zen 400 16px/22px です。スタンスの札は (16, 94)・高さ 88 で、行ごとに `infinity` 18px hp-text を頭に付けます（行の高さ 22・間 6）。
- 特性の箱は左 12・右 7・下 7・高さ 62・地 rgba(0,0,0,.34)・枠 1px rule・角 4 です。左に幅 26 のランプの穴（10×10 の steel の丸、点灯時は 16×16 の wick と光）を置き、右に 2 行（Zen 400 16px/22px と 700）を置きます。素直な札は破線の steel の枠・文字 ink-muted・左余白 12・ランプ無しで、2 行目も 400 です。
- 最後に、全面の黒い薄幕（出せない札のときだけ不透明度 .6）と、2px の枠線を重ねます。

### 2.3 meta のカード（`cardFace.js`）と戦闘の手札（`hand-card.js`）の違い

meta の画面では JS の meta 版の描き方に合わせます。

| 項目 | meta（cardFace.js → OCB.Card） | 戦闘の手札（hand-card.js） |
| --- | --- | --- |
| 移動の札（Kind = Move） | スキルとして描く（amber の枠、`star` amber） | 属性の色を付けない（steel の枠、`star` ink） |
| コスト | `def.cost = PrintedCost || Cost`、表示は `face.Cost`。印刷より安ければ「▼差」 | `face.Cost` と `CostDrop` |
| 「▼n」の位置 | DS の既定（丸の下端から 9px はみ出す） | 丸の中心 (42, 42) に重ねる |
| 届く範囲 | 記号があれば数、無ければ「自分」 | 自分向きなら「自分」、どちらでもなければ出さない |
| 値の列 | 全部（上限なし）。アイコンは Power `sword`、Guard `shield`、Heal `heal`、Close・Push `forward`、Back・Pull `back`、Draw `draw`、その他 `plus` | 先頭 2 列まで。その他は `draw` |
| 名前の収め方 | 縮めない（折り返し無しではみ出す） | `CardTextFit.Name` で最小 14px まで縮める |
| 説明 | 行を `\n` でつないで改行を保つ。4 行以上、または 11 文字を超える行があれば 14px/19px に下げる（`.is-small`）。スタンスの行は折り返してよく、行の高さ 20（小さいとき 18）・最小高さ 22 | `CardTextFit.Description` で最小 14px まで縮める。∞ は文字として描く |
| 特性の箱 | 素直な札は `face.TraitTop` と `TraitBottom` に差し替える。用語に点線の下線を引き、ホバーで「語：意味」を出す（ブラウザの title） | 行ごとに最小 14px まで縮める。用語は独自のツールチップ |
| ランプ・出せない札 | 常に消灯、薄幕なし | 状況で変わる |
| 集中・強化・威圧の印 | なし | あり |

説明の行は `descriptionRows(face)`（`:25-28`）で決めます。`face.TextLines` があればそれ、無ければ `face.Description` を「。」ごとに区切った行です。スタンスの札（`face.Lasting`）は行を ∞ 付きで出し、説明文は空にします。

C# 側では、カードの面を 1 つの部品（DS のカード）にまとめ、meta 用の結び付け（`Trial.Meta.CardFace`）と戦闘用の結び付けを別ファイルにするのが素直です。DS のカードの部品を誰が書くかは、戦闘の手札の担当と調整が要ります（5 節）。

---

## 3. 所持カード（`BG/src/scenes/meta/owned.js`、236 行）

探索中に C キーかボタンで開く重ね画面です（ルーターの `openOverlay('cards')`、`main.js:94-95`）。`ctx.expedition` から、持っている種類（`ex.Owned`）、習熟の記録（`ex.Mastery`）、戦闘デッキ（`ex.Deck`）を読みます。読むだけの画面です。

### 3.1 配置（`owned.js:59-75`、`meta.css:322-350`）

- 幕は全面で、地 ground 86% です。押すと閉じます。
- 板は (40, 36, 1840, 1008) で、地 panel-solid・枠 1px steel・角 6・影 `--shadow-tip` です。以下の座標は板の中の座標です。
- 見出し行は左 28・右 24・上 20・横並び・縦中央・間 16 です。「所持カード」（Shippori 700 34px/1.2・字間 .16em）、種類数「N 種」（Plex Mono 600 22px）、右端に案内「←↑↓→ 選ぶ　PgUp PgDn ページ　C か Esc で閉じる」、閉じる × を並べます。
- 格子（28, 92, 1100, 830）は 6 列 × 3 行で、升を (列×184, 行×274, 176, 262) に置きます。
  - 升は縦並び・横中央・上余白 4・角 6 のボタンです。ホバーで地 ink 5%（140ms）、カードは上へ 5px（140ms）です。ホバーに入るたびに nodeHover を鳴らします。
  - 中身はカードを 0.68 倍にしたものです。カードの名前は習熟で育った名前（`book.Mastered(def)` の面）です。
  - 選択中は地 amber 10% と 2px の amber の内枠です。
  - 升の下の行は左右 10・下 10・高さ 34・中央・間 8・Zen 400 16px です。習熟の丸 3 つ（12×12・枠 2px amber・段数まで塗る・間 4）、「N 段」、戦闘デッキに入っている札なら `draw` 18px amber（ツールチップ「デッキの札」・左余白 4）を並べます。
- ページ送り（28, 940, 1100, 48）は出立と同じ部品で、ページ文字は「1 / 5」です。
- 詳細（1160, 92, 652, 896）は地 panel・枠 1px rule・角 6 です。
  - 原寸のカードを (20, 20) に置きます。
  - 文の列（260, 20, w 372）に、育った名前（h3）、名前が変わっていれば「元の名：X」、デッキに入っていれば `draw` 18px amber と「デッキの札」を並べます。
  - 下の箱（20, 344, 612, 532）は縦並び・間 24・縦スクロールで、次の 3 つを置きます。
    - 習熟の欄（上罫 1px rule・上余白 12・間 10）: 見出し「習熟」（Zen 700 18px・字間 .14em）、大きな丸 3 つ（20×20・間 8）と「N 段」（Shippori 700 30px・字間 .06em）を並べた行。続けて次のどれか 1 つを出します。
      - 習熟の型が無い札は「習熟で名は変わらない」。
      - 3 段なら「最上の段」。
      - 2 段で、ほかの札がこの生の 3 段を持っていれば、`MetaText.TopTaken(その札の育った名前)` を囲み（内余白 8/12・左に 2px の steel の線・地 steel 10%）で出し、その下に「刻み N」を Plex Mono 600 18px で出します。
      - それ以外は「次の段まで」・刻みのゲージ・「刻み t / need」（Plex Mono 600 20px）を 3 列（auto / 1fr / auto、間 12、Zen 400 18px）で並べ、次の名があれば「次の名：X」を足します。刻みのゲージは高さ 10・角 5・地 ground 70%・枠 1px rule で、中身は amber・幅 `min(100, ticks/need)%` です。`need` は `profile.Thresholds[rank]` です。
    - 永続能力の欄: 見出し「永続能力」と、スタンスの札なら行ごとに `infinity` 18px hp-text と文、それ以外は「なし」。
    - 残りの行: `DeckBuilder.ShownDetailOf(育った札)` の 2 行目以降を Zen 400 18px/1.5・下余白 6 で並べます。フレーバーは Shippori です。
  - 選び直すたびに、原寸のカードを不透明度 0・y+8 から 200ms `out` で出します。

### 3.2 動き・音・キー（`owned.js:169-225`）

- 開くとき: 幕を 0→1 に 200ms `out`、板を不透明度 0・y+14 から 260ms `out` で出し、journalOpen を鳴らします。
- 閉じるとき: journalClose を鳴らし、幕を 160ms `in` で消し、板を不透明度 0・y+10 へ 170ms `in` で動かしてから `ctx.onClose()` を呼びます。閉じ始めたら、以後のキーはすべて飲み込みます。
- ページ送り: 新しい升を不透明度 0・x＋30×向き から 220ms `out`、遅延（列番号）×16ms で出し、nodeHover を鳴らします。
- 選択: uiClick を鳴らします。
- キー: Esc と C は新しく押したときだけ閉じます。PgUp／PgDn でページを送ります。←／→ は一覧全体を 1 つずつ動き、ページをまたぎます。↑／↓ は 6 つ動き、ページの外へは出ません。それ以外は `modalSwallow` です。

---

## 4. 設定・セーブ（`BG/src/scenes/meta/settings.js`、198 行）

どの画面の上にも重ねる板です。戦闘の速さは戦闘画面の切り替えと同じ値を持ちます。

### 4.1 配置（`settings.js:38-150`、`meta.css:295-320`）

- 幕は全面で、地 ground 72% です。押すと閉じます。
- 板は x 540・幅 840 で、高さは中身で決まります。y は `max(24, round((1080 − 高さ)/2))` です。手計算では高さ約 745・y 約 168 です。板は panel-solid の地・1px steel の枠・角 6・`--shadow-tip` の影で、内余白 28/36/24・縦並び間 6 です。
- 見出し行は両端揃え・下余白 12 で、「設定・セーブ」（Shippori 700 34px/1.2・字間 .16em）と閉じる × を置きます。
- 行 `.ocbg-meta-set-row` は 3 列の格子（176px / 1fr / auto）・縦中央・間 16・最小高さ 64・内余白 6/14・角 6 です。ラベルは Shippori 700 22px/1.2・字間 .08em です。フォーカス中の行は地 ink 6% で、左端に幅 3・上下 14 内側・角 2 の amber の線を出します（140ms）。
- 行の並びは次のとおりです。
  1. 「戦闘の速さ」: 切り替えボタン 3 つ。`BattleSpeed.Steps`（Slow / Normal / Fast）で、`speed1`〜`speed3` 22px ink のアイコンと `BattleSpeed.Label`（「×1.0」「×1.25」「×1.5」）を出します。
  2. 「演出短縮」: 「入れる」「切る」。行の下に注記「演出短縮を入れると、速さに関わらず演出が縮みます。」（Zen 400 16px/1.45・左余白 206・上 −4・下 4）を置きます。
  3. 見出し「音の大きさ」: Zen 700 20px・字間 .1em・上 12・内余白 16/14/6・上罫 1px rule。
  4. 「全体」「効果音」「環境音」: つまみの行。
  5. 「消音」: 「消す」「鳴らす」。
  6. 下の帯: 横並び・右寄せ・間 16・上余白 16・内余白上 16・上罫 1px rule。左に注記（最大幅 300、`MetaText.SaveNoteOf(kind)`）、保存ボタン（`.ocbg-meta-btn`、高さ 56、`MetaText.SaveQuitOf(kind)`）、閉じるボタン（主ボタンの小型「閉じる」＋「Esc」）。保存ボタンと注記は、`ctx.onSaveQuit` があり `canSaveQuit !== false` のときだけ出します。
  7. 案内「↑↓ 項目　←→ 変える　Esc 閉じる」を右揃え・上 12 で置きます。
- 切り替えボタンは最小幅 132・高さ 48・内余白 0/16・中央・間 8・地 panel・枠 1px steel・角 6・Zen 700 20px で、文字は Plex Mono 600 です。ホバーで枠 ink、選択中は枠 2px amber・地 amber 16%/panel です（140ms）。
- つまみの行は、溝（高さ 10・角 5・地 ground 80%・枠 1px rule、当たり判定は上下に 14px 広い）、塗り（amber・角 5・幅 v%）、つまみ（24×24 の丸・地 panel-solid・枠 2px amber・`0 0 10px amber 40%`・中心を v% に）、数（幅 56・右揃え・Plex Mono 600 24px、0〜100）です。
- 下の帯でフォーカス中のボタンは `0 0 0 2px wick` の輪郭を出します。
- `saveQuitKind` の文言は次のとおりです。'lineage' は「保存してタイトルへ」と「ここまでの記録は保存されています。つづきからで、ここへ戻ります。」、'battle' は「保存して閉じる」と「戦闘の途中を保存して閉じます。つづきからで、この場面に戻ります。」、'life' は「保存して閉じる」と「今の状態を保存してゲームを閉じます。生は終わりません。」です。

### 4.2 値と結び付け（`settings.js:32-55`、`:87-128`、`:157`）

- 速さは `ctx.settings.speed`（倍率の数か段）を `stepOf` で段に直します。どれにも当たらなければ `BattleSpeed.Default` です。
- 音量は `audio.settings` の写し、無ければ既定値（master .8、effects 1、ambience .6、muted false）です。
- つまみは 0.05 刻みに丸めます。値が変わったら `audio.setMaster`／`setEffects`／`setAmbienceVolume` を呼び、描き直して uiClick を鳴らします。引いているあいだも 5% ごとに鳴ります（uiClick の gap 40ms で間引かれる）。ポインターを捕まえて、溝の左端からの割合で値を決めます。
- 消音は `audio.setMuted` です。
- 変わるたびに `ctx.onChange({ ...今の設定, master, effects, ambience, muted, speed: 倍率, speedStep, shortFx, reverb? })` を呼びます。ルーターはこれを書き戻し、音にも当てます。
- `audio.onChange` を購読し、ほかの場所での変更（戦闘の切り替えや消音キー）を板に映します。破棄で購読を外します。

### 4.3 動き・音・キー（`settings.js:152-186`）

- 開くとき: 幕を 0→1 に 200ms `out`、板を不透明度 0・0.97 倍・y+10 から 220ms `out` で出し、uiClick を鳴らします。
- 閉じるとき: uiCancel を鳴らし、幕を 160ms `in` で消し、板を不透明度 0・0.98 倍・y+6 へ 160ms `in` で動かしてから `ctx.onClose()` を呼びます。
- フォーカスの順は、速さ・演出短縮・全体・効果音・環境音・消音・（保存）・閉じるです。
- ↓／↑ で 1 つ動き、端で反対側へ回ります（nodeHover）。←／→ はその行の値を 1 段変えます（端では止まる、uiClick）。Enter と Space は、ボタンの行なら押し、それ以外は →と同じです。
- Esc は新しく押したときだけ閉じます。それ以外は `modalSwallow` です。
- 保存ボタンは uiConfirm を鳴らして `ctx.onSaveQuit()` を呼びます。

---

## 5. 手記のドロワー（`BG/src/scenes/meta/journal.js`、355 行）

右から出る和紙の板で、幅は 576（画面の 30%）です。探索中は J かボタンで開き（`main.js:96-97`、`journalCtx('explore')`）、戦闘中は戦闘画面が `ctx.openJournal(host, { enemyIds, disclosureOf, pageId? })` で開きます（`main.js:453`、`journalCtx('battle')`）。

### 5.1 本の組み立て（`journal.js:23-156`、純粋な関数）

- `bookOf(ctx)`（`:55-94`）は次の順で本を組みます。
  1. 文脈は `ctx.mode` で決めます（'battle' は Battle、'explore' は Exploration）。
  2. 頁は `ctx.book.Pages`、`ctx.pages`、`ExplorationScreenBuilder.Build(ex.Session).Journal.Pages` の順に最初にあるものを使います。
  3. 戦闘中で戦っている敵がいれば、敵の頁をその敵のものだけに絞ります。
  4. 戦っている敵ごとに頁を作り直します。戦闘中は必ず、それ以外は頁が無いときだけです。開示度は `disclosureOf(id)`（無ければ `ex.DisclosureOf`、それも無ければ 0）を 0〜2 に収めます。頁は `e.Rows` があればそれ、`EnemyPages.Has(id)` なら `EnemyPages.Page(id, level)`、どちらも無ければ「頁の中身は、まだ書かれていない。」の 1 行です。
  5. 戦闘中は、敵の頁を戦っている順に並べます。
  6. 用語の頁が無ければ `JournalTerms.Page()` を足します。
  7. 用語の頁に「構え」の次の行として「スタンス」の行を差し込みます（`withStanceTerm`、`:101-108`）。
  8. 希少な頁を持つ生なら、ダンジョンのタブの最後に「希少な頁」の頁を差し込みます（`rarePagesPage`、`:116-130`）。中身は「受け継いだ頁 N 枚」「この生で拾った頁 N 枚」と注記 2 行です。
  9. タブの順（敵・ダンジョン・メモ・用語）に並べ替えて `new JournalBook(pages, context)` にします。
- `tabsOf(book, page)`（`:144-156`）はタブの列を返します。戦闘中は敵の頁ごとに 1 つ（名前は `JournalBook.TabText`）、探索中は「敵」のタブ 1 つです。続けてダンジョン・メモ・用語です。開けない頁のタブは閉じた状態にします。各要素は `{ label, open, on, page, enemy }` です。
- 開いたときの頁は `ctx.pageId` があれば `book.Find(pageId)`、無ければ `book.First` です。

### 5.2 配置（`journal.js:178-306`、`meta.css:352-387`、`:426-433`、`:268-273`）

- 幕は全面で、地 ground 30% です。押すと閉じます。
- 板は (1344, 0, 576, 1080) です。地は和紙（6.4 節）、文字 washi-ink、内余白 26/28/18、縦並びです。影は `-18px 0 40px ground 70%`、左に 1px の washi-ink 25% の線を引きます。
- 上から次の順に並べます。
  - **頭の行**（横並び・縦中央・間 12）: `journal` 30px washi-ink と「手記」（Shippori 800 36px・字間 .3em・間 12）。戦闘中は右寄せで「閉じるとプレイできます」（Zen 400 16px/1.3）。閉じるボタン「閉じる（J）」は右寄せ（案内があれば左余白 8）・高さ 40・内余白 0/14・枠 1px washi-ink 45%・角 4・Zen 700 16px で、ホバーで地 washi-ink 8% です。
  - **タブの列**（上 20）: タブの数だけ等分の格子・間 6 です。タブは高さ 46・Shippori 700 20px・字間 .08em・地 washi-ink 6%・下に透明の 3px の線・角 4/4/0/0 です。ホバーで 11%、開いているタブは 13% と下の線が washi-ink です。閉じたタブは不透明度 .32 で押せません。敵のタブは内余白 0/4・Shippori 700 18px/1.15・字間 .02em で、語の途中で折りません。タブが 5 つなら 17px・字間 0、6 つなら 15px・字間 0 です。ツールチップは名前です。
  - **下のタブ**（上 12、折り返し、間 6/8）: 探索中の敵のタブと、ダンジョンのタブが、頁を 2 つ以上持つときだけ出します。札は内余白 6/14・枠 1px washi-ink 35%・角 16・Zen 700 16px/1.2 で、開いている頁は枠 2px washi-ink・地 10% です。文字は敵の名前、`dungeon-N` の頁なら「第 N 層」、それ以外は頁の題です。
  - **題の行**（上 20・下余白 12・下罫 1px washi-ink 25%、両端揃え・ベースライン揃え）: 題（Shippori 700 28px/1.25・字間 .06em）と開示度（Zen 700 16px・間 10）です。開示度は 10×10 の丸 2 つ（枠 1.5px washi-ink・開示度まで塗る・間 4）と `page.DisclosureLabel`「開示度 1 / 2」です。
  - **本文**（残りの高さ・縦スクロール・内余白 4/8/12/0）: スクロールバーのつまみは washi-ink 30% です。
  - **案内**（上 10・内余白上 10・上罫 1px washi-ink 18%）: 「←→ 頁　J か Esc で閉じる」（Zen 400 16px）。
- 本文の行（`rowsOf`、`:215-230`）は次の 4 種類です。
  - 見出し（ラベルだけの行）: Shippori 700 18px・字間 .14em・上 20・下 8。用語の頁の「【用語】」の後は対の形、それ以外は積む形に切り替えます。
  - 対の形: 2 列の格子（132px / 1fr、間 14）・内余白 8/0・下に点線 1px washi-ink 25%。ラベルは Shippori 700 18px/1.35、文は Zen 400 16px/1.5 で改行を保ちます。
  - 積む形: 縦並び・間 4・内余白 10/0・下に点線。ラベルは Shippori 700 18px/1.35・字間 .04em です。
  - 文だけの行: Zen 400 18px/1.7・内余白 10/0・改行を保ちます。
  - 開示度が足りない行の文は「？」の札（最小幅 44・内余白 0/8・中央・破線 1px washi-ink 45%・角 3・Zen 700 16px/1.6、ツールチップ「？」）にします。
- 敵の頁には、メモを持つ文脈（`onMemo` か `memo` がある）で、最後に「自分のメモ」の行を付けます（`EnemyPages.WithMemo`）。戦闘中は文の末尾に「戦闘中は書けない」が付きます。
- **メモの頁**（`memoOf`、`:233-269`）: 受け継いだ走り書きがあれば、見出し「受け継いだ走り書き」の下に積む形で並べます。ラベルは `InheritanceText.ScribbleName(世代, 名前)`、文は Shippori 18px/1.6 です。続けて見出し「自分のメモ」を置きます。
  - `onMemo` があれば書ける用紙にします。最小高さ 360・上 12・内余白 4/6・枠なし・Shippori 400 20px/36px・字間 .04em・文字と入力位置の印は washi-ink です。36px ごとに罫線（35〜36px に washi-ink 22% の 1px）を引き、罫線は文と一緒にスクロールします。空のときの薄字は頁の空の行の文（不透明度 .55）です。フォーカス中は地 washi-ink 4% です。
  - 入力は 400ms 待ってから `ctx.onMemo(text)` で渡し、閉じるときと破棄のときにも渡します。入力中のキーは下の画面へ流しません。Esc だけは用紙から抜けてドロワーを閉じます。
  - `onMemo` が無ければ、メモの文（無ければ空の文）を文だけの行で出します。

### 5.3 動き・音・キー（`journal.js:208-213`、`:300`、`:309-343`）

- 開くとき: 板を x+576 から 220ms `out` で入れ、幕を 0→1 に 220ms `out` で出し、journalOpen を鳴らします。
- 閉じるとき: メモを渡し、journalClose を鳴らし、幕を 180ms `in` で消し、板を x+576 へ 180ms `in` で出してから `ctx.onClose()` を呼びます。
- 頁を替えるとき: nodeHover を鳴らします（開いた直後は鳴らさない）。本文を先頭へ戻し、最初の子を不透明度 0・y+8 から 200ms `out` で出します。
- キー: Esc と J は新しく押したときだけ閉じます。←／→ と Tab は、開ける頁を全部つないだ順で前後へ動き、端で回ります。↓／↑ は本文を 60px ずつスクロールします（その場で動く）。それ以外は `modalSwallow` です。

---

## 6. 和紙・スクロール・フォーカスなどの部品の詳細

### 6.1 和紙（`util.js:121-171`、`meta.css:268-273`）

和紙の地は `--washi` の上に、繊維のタイル（360×360 の繰り返し）と 2 つの楕円の放射グラデーションを重ねます。終了画面のカード（meta-a）も同じものを使います。

- 繊維のタイルは毎回同じ絵になります。乱数は Park-Miller で、`seed = 48271` から `seed = seed×48271 mod 2147483647`、値は `seed/2147483647` です。
  - しみ 26 個: 呼ぶ順は cx、cy、半径 `30 + r×70`、不透明度 `0.012 + r×0.02`（小数 3 桁）です。washi-ink の放射グラデーション（中心がその不透明度、外周 0）を描きます。
  - 繊維 260 本: 呼ぶ順は x0、y0、長さ `6 + r×r×46`（2 回呼ぶ）、角度 `(r−.5)×π×.9`、曲がり `(r−.5)×長さ×.6`、不透明度 `0.04 + r×0.07`、太さ `0.5 + r×0.9` です。端点は `(x0 + cos×長さ, y0 + sin×長さ)`、制御点は `((x0+x1)/2 + 曲がり, (y0+y1)/2 − 曲がり)` の二次曲線で、端は丸めます。
  - しみも繊維も 3×3 にずらして描き、継ぎ目を消します。
- 楕円の 1 つ目は中心 (30%, 18%)、色 `mix(washi 65%, ink 35%)` = #efe8dc、60% で透明になります。2 つ目は中心 (78%, 92%)、washi-ink 7%、55% で透明になります。大きさは CSS の既定（farthest-corner）です。576×1080 の板では、1 つ目の楕円の半径が約 884×995、2 つ目が約 1525×1040 です（どちらも割合の止まりの前の値）。
- Unity では、繊維のタイルを実行時に 360×360 の Texture2D へ描き、RawImage の uvRect で繰り返すのが軽い方法です。描く処理はエンジンに依存しない形にして、dotnet で画素の検算ができるようにします。

### 6.2 スクロールの箱

- デッキ一覧（1.4 節）: 行単位に吸着し、スクロールバーを見せず、片側だけぼかします。uGUI の RectMask2D は上下に同じぼかししか掛けられません。そこで、ぼかしを 12px に設定し、ぼかしたくない側は padding を負にして、ぼかしの帯を見える範囲の外へ逃がします。
- 手記の本文と所持カードの下の箱: 縦スクロールと、8px 幅・角 4 のつまみです。つまみは本文が steel、手記が washi-ink 30% です。
- 出立の用語の列: 最大高さ 104 で、あふれたら縦スクロールします。

### 6.3 フォーカスとツールチップ

- Tab の順は DOM の順で、無効と tabindex −1 を飛ばします。Shift+Tab は逆順です。Space と Enter で選んだボタンを押します。輪郭は 2px の wick を 3px 外側に、角は 6＋3 で描きます。ポインターを押したら印を下ろします。
- ブラウザの title のツールチップ（用語の下線、ボーナスの枠、デッキの札の印、手記のタブ、「？」）は、Unity では遅延付きの小さな札で代えます。見た目は DS のツールチップ（地 panel-solid・枠 1px steel・`--shadow-tip`）が近く、遅延は 500ms を目安にします。

---

## 7. 読むデータと呼ぶもの、JS にしか無いもの

### 7.1 出立

- 入力: `ctx.lifeStart`（`Inheritance.startLife` の LifeStart: `DeckMin / DeckMax / ToolSlots / ConsumableSlots / Seed / Life.Generation / Lineage.Seed`）、`ctx.owned`、`ctx.savedDeck`、`ctx.seed`。
- 出力: `ctx.onSetOut(payload)`（1.7 節）、`ctx.onBack()`、`ctx.onSettings()`、`ctx.onJournal()`（試遊版では未使用）。
- C# に既にあるもの: `DeckBuilder` の Page / CountText / CanAdd / Add / Remove / CountOf / Total / IsValid / StatusText（上限以内の文）/ DeckLines / Build / Save / Filter / CostOptions / DetailOf / AttributeOptions、`DeckPage.Text`、`DepartureBuilder` の ToolOptions / ConsumableOptions / Tools / Consumables / ToolCount / ConsumableCount / CanAddTool / CanAddConsumable / AddTool / RemoveTool / AddConsumable / RemoveConsumable / PlayerStartCell / StartGap / StartLine / ChooseTalent / TalentIn / TalentPage / TalentHeading / BackLabel / SetOutLabel / ToolsLine / ConsumablesLine / TalentLine / StatusText / CanSetOut / Build、`Depiction.View.DepartureSupplies` の Tools / Consumables / Of、`ItemCatalogue.Tool / Consumable`、`ItemDef.Reading / Side / Note`、`Loadout.Of`、`Expedition.InitialKinds`、`CardCatalog.PrototypeDeck`、`DeckRules.PlayerStartCell / StartGap`。
- JS にしか無いもの（`BG/docs/EXTENSIONS.md:10-73`、L-1〜L-9）:
  - 規則の核: `DeckLimits`（Default / From / With / IsDefault / Equals）、`Cards.Validate(deck, limits)`、`DeckRules.ValidateDeck / CanAdd / Validate` の limits 引数。
  - DeckBuilder: limits 付きのコンストラクター、`Limits`、上限を読む CanAdd / Validate / StatusText（上限超えの文「あと n 枚外すと戦えます」を含む）、`Random(seed, size, limits)`、`Prototype(limits)`、`Load / Read / LoadOrPrototype` の limits、インスタンスの `Title` と `TitleFor(limits)`、`RaisedLine`、`ReadOverCap`、`OverCapNotice`、`WithOwned`、`WithPrototypeKinds`。
  - X-17 の表示語: `ShownAttributeOptions`、`ShownAttributeWords`、`ShownDetailOf`。C# の語（技 / 構え）は残し、画面だけが「スキル / スタンス」を使います。
  - DepartureBuilder: limits 付きのコンストラクター、`Limits`、上限を読む CanAddTool / CanAddConsumable / ToolsLine / ConsumablesLine、デッキ側の上限を使う `Validate`（`LimitsWith`）、インスタンスの `Title / ToolHeading / ConsumableHeading` と `…For(limits)`、`RaisedLine`、`IsBonusToolSlot`、`BonusLabel`。
  - DepartureSupplies: `NewBuilder(limits)`、`Carry(departure, limits)`。
  - 画面の側だけ: `playerNote` の文の削り方、`randomDeck()`（持っている種類だけで 30 枚まで、最大 16 回試す）、下書き。
  - Inheritance（継承の核）全体が JS にしか無く、`InheritanceText.ToHallButton` と `ScribbleName` もそこから来ます（meta-a と継承の担当の範囲）。
- 既定（limits 無し）の動きは C# のままにします。試遊版だけが limits を渡します。

### 7.2 所持カード

- 入力: `ctx.expedition`（`Owned`、`Mastery`、`Deck`）、または `ctx.owned / book / deck`。出力: `ctx.onClose()`。
- C# に既にあるもの: `CardCatalog.All`、`Mastery.TopRank`、`MasteryProfiles.Find`、`MasteryBook.RankOf / TicksOf / ProfileOf / Mastered / TopRankHolder`、`MasteryProfile.Thresholds / NameAt`、`DeckBuilder.DetailOf`。
- JS にしか無いもの: `ShownDetailOf`（X-17）だけです。

### 7.3 設定

- 入力: `ctx.settings`、`ctx.audio`（`settings`、`setMaster / setEffects / setAmbienceVolume / setMuted`、`onChange(cb) → 解除`）、`ctx.canSaveQuit`、`ctx.saveQuitKind`、`ctx.inBattle`。出力: `ctx.onChange(next)`、`ctx.onSaveQuit()`、`ctx.onClose()`。
- C# に既にあるもの: `BattleSpeed.Steps / Default / Multiplier / Label / Chevrons`（`KIT/Depiction/Script/BattleSpeed.cs`）。音の API は音の担当が作ります。

### 7.4 手記

- 入力: `ctx.mode`、`ctx.book / pages / expedition`、`ctx.enemies / enemyIds`、`ctx.disclosureOf`、`ctx.pageId`、`ctx.memo`、`ctx.scribbles`。出力: `ctx.onMemo(text)`、`ctx.onClose()`。
- C# に既にあるもの: `JournalBook / JournalPage / JournalRow / JournalTab / JournalContext / JournalTerms`（`KIT/Journal/Script/`、JS と 1 対 1）、`ExplorationScreenBuilder.Build(session).Journal`、`Expedition.DisclosureOf / Pages`、`Enemies.ById`、`IEnemyIntel / NodeIntel / NoEnemyIntel`。
- JS にしか無いもの（`EXTENSIONS.md:77-145`、J-1〜J-16）:
  - `EnemyPages`（`BG/src/bridge/journal/EnemyPages.js`）: 敵 13 体の頁の文（敵のロースターから一字一句）、開示度ごとの行、`WithMemo / MemoRow`、地図の語 `EncounterName / TendencyLine`。
  - `JournalIntel`（`BG/src/app/intel.js`）: `IEnemyIntel` の実装、情報収集と七層での開示度の引き上げ、`SurveyTarget`、`EncounterAt`。C# では `ExplorationSession.Arrived` が private なので、opt-in の到着の通知（例: Intel が `IArrivalListener` を実装していれば Arrived の最後に呼ぶ）が要ります。
  - 画面の側: `bookOf` の戦闘中のタブの並べ方、`withStanceTerm`、`rarePagesPage`（`Expedition.InheritedPages` も JS にしか無い）。
  - 探索のセッションの `Trial / SurveyTarget / Life` は探索の担当の拡張です。

---

## 8. Unity に今あるものと、使えるかどうか

| ファイル | 今あるもの | Trial で使うか |
| --- | --- | --- |
| `KIT/Depiction/DeckSelectScreen.cs`（224 行） | 6×3 の格子、0.72 倍のカード（CardView のプレハブ）、絞り込み、プリセット、「出立の支度へ」。文字は旧 Text、配置は画面の割合、動き・音・キー・詳細のカード・ゲージ・一覧の札は無い。題は静的な `DeckBuilder.Title`、属性の語は「技 / 構え」、ランダムは `Environment.TickCount` の種で全カタログから引く | 使わない。定数（6 列・3 行・0.72）と、デッキを保存するのは進むときだけ（#211）という決まりだけを参考にする |
| `KIT/Depiction/DepartureScreen.cs`（188 行） | 才能の 3×6 の格子、説明の 5 行（JS と同じ文）、品の行は `DepartureBuilder.LineOf`（探索の品に「（この試験の戦闘では効果が出ません）」を付け、ink2 で描く） | 使わない。JS は品を札で描き、側の札（探索 / 戦闘）を出し、`LineOf` の注記は出さない。JS が勝つ |
| `KIT/Depiction/DepartureSupplies.cs`（50 行） | 純粋な C#。Tools / Consumables / NewBuilder / Of | そのまま使う。Trial 側の薄い包み（`NewBuilder(limits)` と `Carry`）を足す |
| `KIT/Depiction/DemoScreens.cs`（168 行） | 旧デモのモード選択・終了画面・降参ボタン | 使わない。試遊版の出立はモード選択を経ずに探索へ進む |
| `KIT/Journal/JournalDrawerView.cs`（144 行） | 灰色の仮の紙、組み込みの文字、滑り無し、頁ごとに 1 タブ、本文は `page.Body` の素の文字 | 使わない（見た目が仮）。中の `JournalBook` は使う |
| `KIT/Journal/Script/JournalBook.cs`・`JournalTerms.cs` | JS と 1 対 1 | そのまま使う |
| `KIT/Depiction/CardView.cs`（712 行） | プレハブに結び付いた手札のカード | meta では使わない。DS のカードを Trial 側でコードから組む（2 節） |
| `KIT/UiKit.cs` / `UiTween.cs` | 旧 Text と割合の配置。コルーチンのトゥイーン（linear / in / out / inout、全体の Speed） | 使わない。Trial の時計は 1 画面ごとで、同じキーの置き換えと `smooth` と CSS のベジエが要る |
| `KIT/Resources/Fonts` | 5 書体 | 使う。足りない 3 書体を足す（0.2 節） |

---

## 9. C# の分け方（実装の単位）

単位どうしでファイルを共有しません。行数は C# とテストの合計の目安です。

### U-meta-b-1: 出立の枠の拡張（規則、opt-in）約 700 行

- 触るファイル: `unity-port/BattleCore/DeckRules.cs`（`DeckLimits` を足し、ValidateDeck / CanAdd / Validate に `limits = null` の引数を足す）、`unity-port/BattleCore/Cards.cs`（`Validate(deck, limits = null)`）、`KIT/Depiction/Bridge/DeckBuilder.cs`、`KIT/Depiction/Bridge/DepartureBuilder.cs`。
- 新しいテスト: `unity-port/BattleCore.Tests/DeckLimitsTests.cs`、`unity-port/Depiction.Bridge.Tests/DeckLimitsBridgeTests.cs`（JS の `test/ext/limits.test.mjs` の L-1〜L-9 を移す）。
- limits を渡さない呼び出しは今の C# と一字一句同じ動きにし、既存のテストと Battle.unity をそのまま通します。
- `BattleLaunch.Limits` と `Expedition` の limits の受け渡し（EXTENSIONS の「Open」の 2 件）は、戦闘と探索の担当の単位に入れます。入らないと、生存者の 41〜45 枚のデッキが最初の戦闘で例外になります。
- DeckRules.cs と Cards.cs はほかの規則の拡張とぶつかりやすいので、設計の担当が持ち主を 1 つに決めてください。

### U-meta-b-2: DS のカードと meta のカードの面 約 900 行

- 新しいファイル: `KIT/Trial/Ds/OcbCard.cs`（`OCB.Card` と CSS の移植、216×304 の部品、ランプ、出せない札の薄幕）、`KIT/Trial/Meta/CardFace.cs`（`cardFace.js` の移植: `FaceOf`、`DescriptionRows`、`OcbDef`、`CardEl`、`Underline`、`ScaledCard`、`TermRows`）。
- 戦闘の手札の担当が DS のカードを別に作る場合は、`OcbCard.cs` をその単位へ移し、この単位は `CardFace.cs` だけにします。

### U-meta-b-3: 出立の画面 約 1,600 行

- 新しいファイル: `KIT/Trial/Meta/Departure.cs`（`departure.js` の移植。`MountDeparture` と、同じ名前の内部関数 DrawGrid / RefreshCells / Turn / DrawDetail / Choose / DrawList / MarkChip / Reveal / RandomDeck / Replace / Add / Remove / Shake / RefreshDeck / SetNotice / SupplyCard / Pulse / DrawSlots / DrawTalent / DrawLane / RefreshSupplies / ShowSupplies / ShowDeck / Back / SetOut / OpenSettings / Leave / MoveCursor、下書きの読み書き）、`KIT/Trial/Meta/DepartureSupplies.cs`（`Trial.Meta.DepartureSupplies`。既存の `Depiction.View.DepartureSupplies.Of` を包み、`NewBuilder(limits)` と `Carry` を足す。エンジンに依存しない）。
- 新しいテスト: `Carry` と下書きの id と `PlayerNote` の削り方（約 150 行）。
- 依存: U-meta-b-1、U-meta-b-2、meta の共通部品（MetaText、Styler、RollNumber、背景の描画器。meta-a の範囲）、Kit。

### U-meta-b-4: 所持カードと設定の重ね画面 約 1,100 行

- 新しいファイル: `KIT/Trial/Meta/OwnedCards.cs`（`owned.js`: `MountOwnedCards`、DeckIds、Pips、DrawGrid、PaintSel、DrawDetail、Select、Turn、MoveSel、Close）、`KIT/Trial/Meta/Settings.cs`（`settings.js`: `MountSettings`、StepOf、Emit、SegRow、Slider、PaintFocus、Close）。
- 依存: U-meta-b-2（カード）、meta の共通部品、Kit（つまみ、切り替えボタン、フォーカスの行）、音の API。

### U-meta-b-5: 手記のドロワー 約 1,200 行

- 新しいファイル: `KIT/Trial/Meta/Journal.cs`（ビュー: `MountJournal`、Go、RowsOf、MemoOf、Draw、DisclosureDots、Step、Close）、`KIT/Trial/Meta/Script/JournalBookOf.cs`（エンジンに依存しない純粋な部分: `BookOf`、`TabsOf`、`WithStanceTerm`、`RarePagesPage`、`ExpeditionPages`、`EnemiesOf`）。
- 新しいテスト: `test/ext/journal.test.mjs` のうちドロワーの分（J-6・J-16 と bookOf / tabsOf）、約 250 行。
- 依存: U-meta-b-6（EnemyPages）、meta の共通部品（和紙）、Kit（複数行の入力欄、スクロールの箱）。

### U-meta-b-6: 敵の頁と手記の情報（JS にしか無い中身と規則）約 1,100 行

- 新しいファイル: `KIT/Journal/Script/EnemyPages.cs`（`EnemyPages.js` の一字一句の移植。名前と HP は `Enemies` から読む）、`KIT/Trial/App/JournalIntel.cs`（`intel.js`。`IEnemyIntel` の実装と 2 つの引き上げ）。
- 触るファイル: `KIT/Exploration/Script/ExplorationSession.cs` に、opt-in の到着の通知を数行足します。探索の担当の単位と同じファイルなので、持ち主を 1 つに決めてください。
- 新しいテスト: J-1〜J-5、J-7〜J-15 の移植（約 450 行）。

---

## 10. Kit に要るもの（この範囲から見た一覧）

- 1920×1080 の基準キャンバスと、左上原点の `Place(rt, x, y, w, h)`。
- トークンの色と `Mix`（0.1 節）、CSS の影（複数・オフセット・ぼかし・広がり）。
- ベクターの部品: 角丸の塗り・枠（1px と 2px、実線と破線、角ごとの半径）、外側の光と輪（`0 0 0 3px` と `0 0 20px`）、丸（習熟の丸・開示度の丸・状態の丸の光）、細線、縦のグラデーション（届く範囲のマス）、楕円の放射グラデーション（和紙）、SVG のパスの線（24×24、線幅 2.2、端と角は丸、破線あり、塗りあり）。
- アイコン: sword・shield・star・flag・back・forward・journal・exile・range・stairs・person・draw・infinity・heal・plus・speed1〜3（パスは `ART/ocb.js:64-100`）。
- 文字: TMP の書体の選び方（display 400/700/800、body 400/700、mono 400/600/700）、字間（em/100）、行の高さの px 指定、折り返し無し、「…」、2 行で切る、改行を保つ、日本語の禁則。
- 時計とトゥイーン（0.3 節）と、CSS の遷移と一回きりのキーフレーム（脈打つ輪）。
- ボタンの状態（ホバー・押下・無効・フォーカス）と遷移、ホバーに入ったときの音、右クリック、ホイール。
- Tab の順のフォーカス管理と輪郭（6.3 節）。
- キーの積み重ねと合成の自動反復、文字キー、`ModalSwallow`（0.4 節）。
- スクロールの箱: 行への吸着、片側のぼかし、細いつまみ、キーでのスクロール（6.2 節）。
- 横並びの配置: flex の伸び・最小幅・ベースライン揃え（絞り込みの列、見出しの行）。
- 遅延付きのツールチップ。
- 複数行の入力欄（日本語の変換、罫線の地が文と一緒にスクロール、薄字、入力位置の印の色）。
- 和紙のタイルの生成（6.1 節、meta-a と共有）。
- DS の部品: `OCB.Card`、`OCB.FloorTile`、`OCB.DistanceChip`。
- 1 台ごとの保存先（PlayerPrefs）: 出立の下書き。
- 足りない書体 3 つ。

---

## 11. 危険と未確認の点

- **日本語の改行**: CSS は `word-break: auto-phrase` と `text-wrap: pretty / balance` で文節を見て改行します。TMP にはこの仕組みが無いので、詳細の文・品の説明（2 行で切る）・手記の行の折り返し位置が変わります。禁則だけは TMP の設定で合わせます。
- **flex の幅**: 絞り込みの列の幅は字の幅で決まるので、書体の測り方が違うと数 px ずれます。
- **キーの自動反復と JIS 配列**: Input System で OS と同じ反復を作り、`;` と `=` を ＋ として受ける必要があります。
- **メモの入力**: TMP_InputField での日本語変換と、罫線を文と一緒に動かす描き方は試作が要ります。
- **規則の拡張の持ち主**: DeckRules.cs・Cards.cs・ExplorationSession.cs は、この範囲の外の拡張も触ります。ファイルごとに持ち主を 1 つに決めないと、単位がぶつかります。
- **生存者のデッキの受け渡し**: `BattleLaunch.Limits` が入るまで、41〜45 枚のデッキは最初の戦闘で例外になります（EXTENSIONS の「Open」）。
- **Seed の型**: LifeStart.Seed が 2^53 を超える整数で来るとき、ランダムの種の下位 30 ビットの取り方を JS と同じにする必要があります。
- **動きを減らす設定**: ブラウザは OS の「視差効果を減らす」でトゥイーンを即座に終えます。Unity には対応する設定が無いので、今回は無視するか、設定の項目にするかを決める必要があります（未決定）。
- **ブラウザのツールチップ**: title の見た目は OS 任せなので、Unity で何を出すかは決め事になります（6.3 節の案）。
- **右上の手記チップ**: 出立の手記チップは、ルーターが `onJournal` を渡していないので試遊版では出ません。実装はしても、出すかどうかはルーターの担当が決めます。
