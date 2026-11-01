# HISTORY (chat-design)

### 2026-11-02 - 瘴気の息づきが % の変化で途切れない形を出す（#728）

#### 概要

息づきの位相を持ち越す `MiasmaBreath` 型を `Backdrop.Script` に足し、旧い探索画面を乗り換えました。PR #757 は書いた時点で open で、merge はこうだいさんの作業です。

#### 変更点

- **`MiasmaLook`**: `AdvanceBreath`、`BreathAtPhase`、`FillAlphaOf`、`ShadeBreathing` を足しました。`Breath` の説明に、% を変えながら呼ぶと位相が飛ぶことを書きました。
- **`MiasmaBreath`**: 探索の画面の `AdvanceBreath` の中身を移しました。テストを `MiasmaBreathTests.cs` に 10 件足しました。
- **`BackdropMotion`**: 息づきの余弦の式を `CosineDip` の 1 か所にまとめました。値は変わりません。
- **検証**: `npm run build` / `lint` / `test:run` / `unity:check` と `dotnet test` が exit 0 です。Unity Editor での見た目は未確認です。
- **完了条件の読み替え**: Issue の `grep -n "Math.Cos"` は `View/Trial` の図形と音声にも当たるので、息づきの式に絞って `View/Backdrop` と `View/Exploration` で 1 か所を確かめました。
- **申し送り**: `2026-11-02-design-trial-miasma-breath-jump.md`（試遊版の探索画面に同じ飛びが残る件）です。

### 2026-11-01 - design 宛ての 4 件を 1 件 1 PR で進める（#711 / #608 / #85 / #287）

#### 概要

4 件をそれぞれ `origin/main` から切ったブランチで PR にしました。#711 と #608 は Issue を閉じる PR で、#85 と #287 は差分の絵が無いので参照だけの PR です。PR はすべて open で、merge はこうだいさんの作業です。

#### 変更点

- **#711（PR #736）**: `CAMP_FACILITIES_DESIGN.md` の §3.3 と §3.1 を、死亡地点での受け取りと、ノードの種類に直しました。
- **#608（PR #741）**: `BackdropView.cs` から `followCornerInfo`、`ShownLayer`、`Update` を消しました。最初のフレームで階層が届くことはコードの順番で確かめました。
- **#85（PR #742）**: `briefs/polearm_warped.md` §6 に、影絵の関門の実測と、絵が映る画面を書きました。
- **#287（PR #743、RPG-by-card#15）**: Unity 側で `Figure.prefab` の棚に 2 体を足しました。Editor を開かず YAML を書いたので、Unity では未確認です。2 枚の仕様カードの §6 を合わせました。`polearm_unyielding` に離れた点が 94 個あることも書きました。
- **検証**: 4 本とも `npm run build` / `lint` / `test:run` / `dotnet test` が exit 0 です。`npm run unity:check` は `origin/main` と同じ 162 件で exit 1 です。Unity の前回のコンパイルが古いためで、`BackdropView.cs` は Unity のコンパイラに個別に通してエラー 0 件でした。
- **決定**: こうだいさんが、立ち絵を試遊版の人型へ差し込むと決めました。差分はそれが入ってから作ります。仕様カード 3 枚と PR #742 / #743 をこの形に直しました。
- **申し送り**: `2026-11-01-design-trial-art-destination.md`（`kind: conflict`）、`2026-11-01-design-trial-art-insert.md`、`2026-11-01-design-silhouette-gate-size.md`、`2026-11-01-design-camp-legacy-wording.md` の 4 件です。

### 2026-10-21 - design 宛ての 4 件を 1 件 1 PR で進める（#623 / #85 / #287 / #244）

#### 概要

`/goal` で渡された 4 件を、origin/main から切ったブランチ 1 本ずつで PR まで進めた。PR #662 / #671 / #672 / #673 はすべて open。#623 だけ Closes で、ほかの 3 件はこうだいさんの作業（差分の絵・案づくり）が残るので Refs にした。

#### 変更点

- **#623（PR #662）**: `battle-visual-v1.md` v1.10。§3.1 に、カードの技名（22 px から）と説明（16 px から）が枠に収まらないときだけ 12 px まで縮める例外を書き、`CardView.cs` の `MinFontSize = 12` にそろえた。特性の箱は今のコードが縮めないので 16 px のまま。12 px は最後の守りとし、16 px 未満で出る 5 枚の文を短くする件は cards へ申し送った
- **#85（PR #671）**: `briefs/polearm_warped.md` を #455 の人型 226 px（`StandardHeight`）とロースター v4.23 に合わせた。予兆の札・数字の高さ、#498 の追従、2 体の組の出る枠、ずれた行番号
- **#287（PR #672）**: `briefs/polearm_crystal.md` と `polearm_unyielding.md` を同じく直した。#349 の 2 段目の札、`.meta` のコミット、v4.23 の代役の枠も入れた
- **#244（PR #673）**: 依頼文を `battle-visual-v1.md` v1.9 と今のコードに合わせ、勝ったときの結果画面（段が上がった札）を頼む項目を足した
- **申し送り（`.claude/comm/handoff/`）**: cards 宛て「説明が 16 px 未満で出る 5 枚の文を短くする」、battle 宛ての食い違い 2 件（`battle_ui_ux_v2.md:1187` の技の前の名前、浮かぶ数字の高さの §5.0 と View）
- **検証**: 4 本のブランチの先頭で `npm run build` / `lint` / `test:run`（204 件）/ `dotnet test`（10 プロジェクト）/ `npm run sources -- --check` が exit 0
- **乖離**: #85 の commit を一度、既にあった `docs/design-85` の名前で切り損ねて #623 のブランチに積んだ。push 前に soft reset で外し、`docs/design-85-v423` に積み直した。`style-guide.md` §2 と `asset-intake.md` §3 の 450 px 前提は範囲外として予定に回した

### 2026-10-06 - 見た目の文書の食い違い 3 件と、戦闘の速さの切り替えの置き場（#322 / #366 / #324 / #327）

#### 概要

design 宛ての open Issue 4 件を、1 Issue 1 ブランチで PR まで進めた。`battle-visual-v1.md` を触る #322 と #366 は積み、ほかは origin/main から切った。PR #373 / #374 / #375 / #376 はすべて open。

#### 変更点

- **#322（PR #373）**: §0.2 の 8 枚の手札の間隔を、§4.8 と同じ 126 px（幅 1098 px）にした。128 px は札が 200 px だったころの値。#213 の design の分（§6.1 と §10 の背水の陣の行を決着済みに）も同じ PR に入れた
- **#366（PR #374、#373 の上に積んだ）**: `battle-visual-v1.md` v1.2。速さのボタンを手記のボタンの左（右から 92、y 20、120×56）に置き、山形のアイコン 1〜3 本と「×1.25」の数だけで出す。1 つのボタンで 3 段を巡り、敵の番も押せる。#234 の決定で設定は手記の外に出たので、手記には入れない。§7.5 に #234 の決着、§5.5 の敵の番の暗くするボタンをターン終了だけに絞った
- **#324（PR #376）**: `style-guide.md` の `battle_ui_ux_v2.md` §4.5 / §4.3 の参照を `battle-visual-v1.md` の「色」と §2.4 へ。#180 の未決 2 項目を外した。依頼文は記録なので日付つきの注だけ。§2.4 の 4 層が明るい地になり、§17 の条件 5 が 4 層で成り立たないことが分かったので §19.3 に残した
- **#327（PR #375）**: `asset-intake.md` §4 の絵のファイル名を、ロースター §1.8 の内部 ID のまま改名しないと決め、#128 待ちの記述と §11 の未決を外した
- **申し送り（main 宛て、`.claude/comm/handoff/`）**: `BattleSpeedButton.cs` を §4.1 の値に寄せる作業（battle、デモの降参ボタンの重なりも）と、正本の食い違い「静止時の固定ラベルの内訳」（`battle_ui_ux_v2.md` §1.5 と `battle-visual-v1.md` §1）
- **レビュー**: 4 本とも、事実・範囲・日本語の 3 つの観点でサブエージェントが読み、反証役が確かめた。直すべき点 5 件（設定の画面を戦闘中に開かないと断定した文、#180 の閉じた日など）と細かい指摘を取り込んだ
- **検証**: 4 本のブランチの先頭で `npm run build` / `lint` / `test:run`（204 件）/ `dotnet test`（6 つのまとまり）が exit 0。`npm run sources -- --check` も exit 0
- **乖離**: 範囲の外は触っていない。#234 の `CAMP_FACILITIES_DESIGN.md` の分と、`docs/reports/2026-09-21-asset-intake-rules.html` の古い未決は残した（予定に記載）

### 2026-09-27 - 戦闘画面とカードの見た目を Claude Design の案 1a で確定する（#240）

#### 概要

こうだいさんが Claude Design で作った案のうち 1a を採用したので、戦闘画面とカードの見た目の元になる文書 `.claude/docs/art_document/battle-visual-v1.md` に書き起こした。PR #245（open）。前段の依頼文（#228、PR #229 merged）もこの日に済ませた。

#### 変更点

- **元になる文書**: 色のトークン（world-v1 の色と Guard の `#8fc4d8`）、文字の大きさ、1920×1080 の配置の px、掴んだときと敵の番、カード 200×280 の部品と状態、ランプ 2 状態と判定に使う敵、意味の伝え方 4 段と状態の文、凡例、見本 11 枚の文、アイコンの path
- **こうだいさんの決定**: Claude Design のチャットの指示 11 件を #240 の本文に写し、`SOURCES.md` §4 の 1 段目として扱った
- **案から直した文**: 岩の構え（置いたとき 1 回だけ）、観察（手薄: ドロー +1）、手薄（2 枚以下）、集中（値の列を 1 つ右）。照合はサブエージェントが設計書と BattleCore で行い、該当行を自分で確かめた
- **判断を置いた点**: ランプは敵に向ける札が届く敵のうち 1 体、自分に向ける札がいちばん近い敵。予兆の語は実装と札の特性の 防御 / 移動。素直な札は破線の箱で「特性なし（+2 込み）」
- **Issue**: #241 / #242（battle）、#243（cards、予兆の語）、#244（design、残りの 3 画面）。#180・#56・#234・#213 に決定をコメント
- **レポート**: `docs/reports/2026-09-27-battle-visual-finalized.html`（Artifact で発行）
- **検証**: `npm run sources -- --check` が exit 0。Claude Design の画面の撮影は Chrome が固まって断念し、値はファイルの中身から取った
- **乖離**: Scope の外は触っていない。Claude Design のファイルは直していない（貼る依頼文をレポートに置いた）
