# HISTORY (chat-design)

### 2026-11-04 - design 宛ての 6 件を 1 件 1 PR で進める（#916 / #905 / #906 / #287 / #85 / #891）

#### 概要

`/goal` で渡された 6 件を、1 件 1 ブランチで PR まで進めました。同じ文書を触る組は積みました（#923 → #926、#928 → #930）。PR はすべて書いた時点で open で、merge はこうだいさんの作業です。

#### 変更点

- **#916（PR #923、Closes）**: 暗い層を星空の 7 層だけにしました。空の平均の明るさは 4 層 0.721 → 0.669、5 層 0.035 → 0.488、6 層 0.008 → 0.456 です。探索の幕は 5 層 62%、6 層 63% に厚くしました。赤の斜線のコントラストは 7 層すべて 3 以上です。`battle-visual-v1.md` v1.18。
- **#905（PR #926、Closes）**: 見本の札を v4.11 の札に替え、体幹崩しの演出（突、こうだいさん決定）を §5.7 に書き、背景の絵の受け口を試遊版も読む形に直しました。`EffectGrammar` の直しは battle への申し送りにしました。
- **#906（PR #927、Closes）**: 試遊版の用語の説明と部品の見本から岩の構えと踏み込み斬りを外しました。JS の元は試遊版のブランチにあるので、`MetaTextTests.UnityDepartures` で 1 行だけ突き合わせから外しました。
- **#287（PR #928、Refs）・#85（PR #929、Refs）**: §17 の条件 5 を 7 層だけの条件にし（`style-guide.md` v1.10）、3 枚の仕様カードを正典 v5 に合わせました。待機の絵 3 枚の色を測り、銅色から赤茶で「竜は藍紫」と合わないことを書きました。
- **#891（PR #930、Refs）**: `style-guide.md` v1.11 で 3 系統の描き分けを入れ、王・龍神・新しい層などの案を報告書にしました（https://claude.ai/artifact/XR9Sh4RCuSwnJmd4R8QZn6）。
- **決定（2026-11-04 こうだいさん）**: 系統は色ではなく形と姿で見せ、色は比較的自由に描く。竜の待機の絵 3 枚の銅色はそのまま使います（`style-guide.md` §14.1 と仕様カード 3 枚を直した）。第 7 層は「三王の座」、寄生の 2 層は星空より明るく置きます。同じ日に、王 3 体・龍神・還らずの英雄の見せ方、暗き死海の骨、残りの日数の色も推奨案に決まりました（#891 の 10 項目がすべて決着）。
- **検証**: 6 本とも `npm run build` / `lint` / `test:run` / `sources -- --check` は exit 0 です。`dotnet test` は 6 本とも exit 1 で、落ちるのは origin/main でも落ちる `TheProfiles_MatchTheCanonTable` の 1 件だけです（#830 への追従待ち、#898）。
- **申し送り**: `2026-11-04-design-905-pierce-thrust-effect.md`、`2026-11-04-design-891-dragon-body-colour.md`（`kind: conflict`）、`2026-11-04-design-891-canon-look-proposals.md`、`2026-11-04-design-trial-audio-dead-sea-name.md` の 4 件です。

### 2026-11-03 - 上級の槍 2 体と錆槍の穂先の幅を試遊版でも切らない決まりにする（#287 / #85）

#### 概要

design 宛ての #287 と #85 を、1 Issue 1 ブランチで PR まで進めた。どちらも origin/main から切り、触る文書は重ならない。PR #879 と #881 はどちらも open で、Issue は差分の絵（#772 の試遊版への差し込み待ち）が残るので Refs にした。

#### 変更点

- **#287（PR #879）**: `style-guide.md` v1.9。§2 の幅の例外に、高さ 226 px での画面の幅（117.7 / 132.4）と左右の端の列を足し、2 体とも縦横比を保って穂先を切らない、試遊版へ差し込むときも同じ、と 1 つの決まりにした。いちばん狭い 8 マス（165 px）でも隣の人型と 45 px 余ることを、Unity リポの待機の絵の外接矩形で確かめた。§19.3 の小竜の論点に目安（原本で中心から左右 560 px）を書いた。2 枚の仕様カードの §2 に同じ決まりを書き、§6 の条件 5 を v1.8（#859）で判定し直した（晶槍は対象外、不退は六層で満たし五層は未決）
- **#85（PR #881）**: `briefs/polearm_warped.md` に同じ決まり（幅 94.2 px）を書き、条件 5 を対象外にした（一層〜三層がどれも明るい層）。本文の「5 行動と 4 行動の食い違い」は 2026-10-04 に解消済みと Issue にコメントした
- **行番号**: 3 枚の仕様カードの §2 の表のうち、大きさの行のずれた行番号を今の行に直した。向き・頭上の空き・数字の位置の行は、`DepictionPlayer.cs` が部分クラスに分かれてずれたまま
- **Unity リポ**: 新しい絵は無いので PR は出していない。絵の棚の RPG-by-card#15 は open のまま
- **検証**: 両ブランチで `npm run build` / `lint`（警告 2、エラー 0）/ `test:run`（148 件）/ `unity-port` の `dotnet test` が exit 0。`npm run sources -- --check` も exit 0
- **乖離**: 範囲の外は触っていない。正本どうしの食い違いは見つからず、申し送りは書いていない

### 2026-11-03 - 7 層の背景を層ごとの色と遠景で描き直し、Krita の指示文の手引きを作る（#89）

#### 概要

こうだいさんの依頼で、下の層ほど同じ黒に見えていた背景を 7 つの色に分け、コードで描く遠景と星を足しました。あわせて、Krita で絵を出すときの指示文と注意点を手引きにまとめました。PR #851 は書いた時点で open で、merge はこうだいさんの作業です。

#### 変更点

- **仕組み（68535d8f）**: `BackdropScenery.cs` に遠景の 8 種類（幹・柱・結晶・尾根・岩壁・肋骨・とぐろ・岩片）と 1 px の縁のぼかしを足しました。粒の組（`MoreMotes`、星は `Motes.Still`）を足し、試遊版の戦闘・探索・タイトル・結果の画面が描くようにしました。読みやすさの検査は遠景を奥から重ねた色で測ります。
- **値（59fa3580）**: 方向の違う 3 案を描いて層ごとに見比べ、組み合わせました。1 層の空の明るさは 0.31 から 0.67 になり、2 層は淡い黄色、7 層は星空です。探索の幕は層ごとの暗い色にしました。`battle-visual-v1.md` v1.13、`TRIAL-PORT.md`、`.claude/CLAUDE.md` を合わせました。
- **報告書（9b92c32f）**: `docs/reports/2026-11-03-layer-backdrops.html` と `2026-11-03-krita-prompt-guide.html`。Krita の背景の指示文は、Danbooru の件数を 2026-11-03 に確かめて新しく組みました。
- **3 層を明るい層に（d622f733、d78e40ad）**: 作業後の回答で、こうだいさんの「上部分は一階層の明るさを…」が「上の層で明るい層を 1 つ増やす」意味と分かりました。3 層を明るい青緑の洞窟にし（空の明るさ 0.008 → 0.534）、明るさの順を「1・2・3 が明るく、4 が最も明るく、5 から 7 へ暗く」に改めました。テスト・文書・報告書も合わせました。1 層を明るくした分は残しています。
- **決定（2026-11-03）**: 設定書を新しい色と明るさに合わせる（申し送り `2026-11-03-design-world-layer-colours-decided.md`）、試遊版に背景の絵の受け口を作る（申し送り `2026-11-03-design-trial-backdrop-art-slot.md`）。
- **検証**: `dotnet test unity-port/UnityCorePort.slnx` が 4,288 件合格（skip 2 は既存）、`npm run unity:check` がエラー 0、`npm run sources -- --check` が exit 0 です。7 層は Unity と同じ値で描くブラウザの道具で撮って確かめました。Unity Editor での見た目は未確認です。
- **乖離**: 範囲の外は触っていません。旧い `BackdropLayers.cs`（`Battle.unity`）は色だけが変わり、遠景と星は描きません。点検役の指摘のうち、縁の帯の幅を画面倍率に合わせる件と、星の位置を毎フレーム書く件は、軽いので直していません。
- **申し送り**: `2026-11-03-design-world-layer-colours.md`（`kind: conflict`。3 層の青緑と 6 層のコバルトが `world-v1.md` §4 の主な色から外れる件）です。

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
