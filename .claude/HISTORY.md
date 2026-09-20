# HISTORY.md - 変更履歴

> セッション単位の変更履歴（降順）。各エントリは「概要」+「変更点」。要約は `README.md` の Development History、進行状況は `MEMORY.md`。古いエントリは肥大化したら `HISTORY-archive.md` へ退避。

### 2026-09-19 - 戦闘描写の仕上げ（Issue #31 と手触りの QA 残り）

#### 概要

ドメイン再読み込みなしの 2 回目の再生で絵が四角になる不具合（#31）を直した。扇の座標計算を純関数にしてテストを足し、手触りの PR で見送った QA の Suggestion を片付けた。

#### 変更点

- **#31**: `ProceduralArt` は `??=` をやめ、`Cached(ref slot, make)` で `!slot || !slot.texture` を見てから作り直す。Icons 配列も同じ経路。`VerticalGradient` は元からキャッシュしないので変更なし。BattleDepiction と test1 をそれぞれ 2 回、PlayMode テストの後にも 1 回再生して、人影・盾・丸ピップを撮影で確認
- **扇**: `Depiction/Script/HandFan.cs`（`FanPlace` と `HandFan.Place`、UnityEngine に依存しない）。置き場所を Script にしたのは、EditMode テストが型で直接呼べるため（名前で引くと型の検査が効かない）。`HandFanTests` を追加（中央が一番高い / 左右対称 / 下の角が基準線より下がらず、端の札は基準線に乗る / 1 枚と 2 枚）
- **式の修正**: 札ごとに自分の角の沈み分を足していたため、出荷値でも中央の隣が 0.14 px 高く、角度を上げると M 字になった。手札全体を端の札の沈み分だけ持ち上げる形にした。見た目は中央が数 px 上がる。`fanDegreesPerCard` に `[Range(0, 15)]`
- **QA の残り**: `SettleHand` は札と開始値を組で持つ。ホバーの当たりは札の定位置・傾き・拡大なしの姿で取る（間隔 / 拡大率を 3 通り変えて、浮く前後で判定が同じことを確認）。`UpgradePrefabs` は FigureView が無いと警告し、参照切れで子が残っていればつなぎ直す（2 つ以上なら警告）。`EveryThrowLineCardSaysWhoseFiguresItLandsOn` は `Opening.Hand` も見る
- **ブランチ**: #33 のマージ先が main ではなく `fix/depiction-followups` だったため、`fix/depiction-polish` に取り込んだ（この PR に #33 の変更も入る）
- **確認**: エラー 0 / EditMode 94 / PlayMode 1 / dotnet test 54 / 同期 2 回目 0 件 / 撮り直し 10 枚は手札より上が前回と同じ（差は 4-hover の標的マークだけ）
- **見送り（QA Suggestion）**: ホバー判定のカメラが null 固定（Overlay 前提、旧コードから）/ テクスチャだけ破棄されたとき古いスプライトを破棄しない、`Icon()` のラムダ確保（実害なし）

### 2026-09-19 - 戦闘描写の手触りの手直し（ホバー、扇形の手札、標的マーク）

#### 概要

Issue #25 の所感 3 点を入れた。手札の札はホバーで浮いて前面に出る。手札は浅い扇形に並ぶ。投げ上げ線を使う札を持つと、効果が乗る人影を四角い標的マークが囲む。

#### 変更点

- **ホバー**: `DepictionPlayer.UpdateHover` がポインタ位置を読み、札を 28 px 持ち上げて 1.05 倍にし、前面に出す。扇の角度は保つ。当たりは札の定位置で取るので、浮いた札の下端でも点滅しない。演出中とドラッグ中は切る
- **扇形**: `HomeOf` が定位置を決める。端の札を基準線に据えて中央を上げ、傾きで下の角が沈む分だけ持ち上げる（画面の下端で切れないため）。1 段あたり 2.5 度。持った札は直立し、戻るときに角度も戻る
- **標的マーク**: 新規 `TargetMarkView`（四隅の鉤型 8 本、表示中はゆっくり呼吸する）。Figure プレハブの子に足し、`FigureView.targetMark` で持つ。色は札の属性色、投げ上げ線を越えると濃くなる
- **台本**: `CardFace.Affects`（`UnitSide?`、投げ上げ線の札で必須）。どの人影を囲むかは台本が決める。EditMode テスト 1 本を追加
- **プレハブ**: `Tools > Depiction > Upgrade Prefabs` が既存の Figure プレハブに標的マークを足す（既にあれば何もしない）。`BuildFigure` も同じ部品を作る
- **確認**: 再コンパイル後のエラー 0 / EditMode 描写分 9 / PlayMode 1（2.0 秒の上限）/ dotnet test 54 / 撮影で 3 点を確認

### 2026-09-19 - アート制作の調べ直し（0 円化の観点）

#### 概要

キャラクター・動き・背景を作るためにこうだいさんがすることと、予定の出費（CSP 6,900 円、画像 API 月 $30、Live2D PRO）を 0 円に近づける道を調べ直した。全工程を 0 円で回せるが、人の手の時間が増え、8 GB の GPU で通るかの実測が先に要る。

#### 変更点

- **調査**: visual-production-pipeline の survey ワークフロー（14 エージェント、反証役と批評役つき）。結論を左右する 6 件（Gemini の無料枠なし / Live2D FREE の上限 / CSP の価格と 3 か月無料 / Animagine XL 4.0 と Illustrious XL v2.0 のライセンス / ComfyUI ポータブルの同梱 CUDA 13.0）は一次情報で再確認
- **正本**: `tools-and-prerequisites.md` の確認日を更新し、§11「0 円化の比較」を追加。背景除去の重み（isnet-anime Apache-2.0 / BiRefNet MIT）を確認済みにした
- **見つかった実装の穴（未対応）**: `FigureView.SetSprite` は静止画専用で RenderTexture を受けられない。塗った絵では `DepictionFx.Flash`（乗算）の白点滅が見えない。`DepictionFx.cs:15-16` のヒットストップと揺れが v2 §5.3 とずれる
- **判断待ち**: CSP 購入済みか / #14 をローカル主へ差し戻すか / #23 を最初に実測へ変えるか / ボスを 2D Animation にするか
- **レポート**: `docs/reports/2026-09-19-visual-production-survey.html`（Artifact `https://claude.ai/artifact/PJ47QE4BHqboyVFmNDXKfS`、life-editor Note `note-d9c7c241`）

### 2026-09-19 - 戦闘描写の後片付け（Issue #26 / #27 / #28 / #29 前半）

#### 概要

描写セッションで範囲の外に出した 4 件を片付けた。同期が改行だけの差分を作らなくなり、`BattleScreenView` は描写シーンで自動起動しなくなり、攻撃者と一字札の文字を台本が持ち、1 行動 2.0 秒の上限が PlayMode テストになった（計画書: archive/2026-09-19-depiction-followups.md）。

#### 変更点

- **同期（#27）**: `sync-unity-project.mjs` は改行を LF にそろえて比べ、書くときは宛先の改行の流儀を保つ。`package.json` に `engines.node >=20.12`
- **自動起動（#26）**: `BattleScreenView.Bootstrap` は `DepictionPlayer` のあるシーンで生成しない。`DepictionPlayer.Start` の `Destroy` の回避を削除。`test1` 限定にしなかったのは、ビルド設定のシーンが `SampleScene` で、`-replayTrace` の無人ビルドが自動起動に頼るため
- **台本の型（#29 前半）**: `Cue.Source`（`UnitSide?`、Slash で必須）、`UnitFrame.RangeGlyph`、`Cue.RangeGlyphAfter` を追加。`DepictionPlayer.Attack` と `FigureView` は台本から取る。`FigureView.Glyph` は削除。EditMode テスト 2 本を追加
- **2.0 秒の上限（#28）**: `Assets/View/Depiction/Tests/PlayMode/` に asmdef と `DepictionPlaybackTests`。View は既定アセンブリにあって asmdef から参照できないので、名前で引く。全 7 イベントが 2.0 秒未満、`Finished`、最後の画面（HP 47 / Guard 0 / 予兆「防御」）を確かめる
- **確認**: 同期 2 回目 0 件 / 再コンパイル後のエラー 0 / EditMode 62 / PlayMode 1 / dotnet test 54 / 描写シーンで `BattleScreenView` 0 個 / `test1` で View v1.1 とトレース再生が動作 / 撮り直し 10 枚は前回との差 0.001% 以下。1 行動は撮影ありで最長 1.81 秒（順 6）
- **気づき（未対応）**: `ProceduralArt` は絵を `??=` で静的に抱える。再生開始時のドメイン再読み込みを切ったこのプロジェクトでは、2 回目の再生から破棄済みの絵を返し、人影や盾が四角になる。スクリプトの再読み込みで直る
- **持ち越し**: #25（手触り、人手）、#30（強弱のしきい値）、#29 後半（v4.2 コアの変換器）

### 2026-09-19 - Unity 戦闘描写（固定台本の 1 ターン）

#### 概要

Unity Editor の `BattleDepiction` シーンで、v4.2 の形の 1 ターン（7 つの出来事）を固定台本から再生できるようにした。順 2〜4 はドラッグ待ち、ほかは自動。ルールの計算は作っていない（計画書: archive/2026-09-19-unity-battle-depiction.md）。

#### 変更点

- **台本**: `unity-port/unity-project-kit/Assets/View/Depiction/Script/` に純 C# の `DepictionFrame` / `DepictionEvent` / `Cue` / `DepictionRunner` と、プラン §4 を手書きした `TurnSliceScript`。asmdef は `noEngineReferences`。BattleCore に依存しない
- **View**: `CardView` / `FigureView`（`SetSprite` が立ち絵の差し替え口）/ `StatusBarView` / `OmenBadgeView` / `ReceiverView` / `ThrowLineView` / `CornerInfoView` / `DepictionPlayer` / `DepictionFx`。既存 `ArenaView.cs` の `FigureView` と名前がぶつかるため、名前空間 `Depiction.View` に入れた
- **プレハブとシーン**: `Tools > Depiction > Build Prefabs And Scene` が無いものだけを作る。プレハブ 7 つは Unity リポ `Assets/Prefabs/Depiction/`、シーンは `Assets/Scenes/BattleDepiction.unity`
- **操作**: 単体向きは受け皿の上で離したときだけ確定、自分向きは投げ上げ線より上、台本と違う札は手札に戻る。デバッグの `autoPlayDrags` / `debugStepMode` / `debugCaptureDir` は既定で切
- **同期**: `sync-unity-project.mjs` が `Assets/View/Depiction/` を再帰で写す（`.cs` と `.asmdef`）
- **準備**: Unity リポに初回コミット（`7b203b8`）、URP を 17.5.0 に揃えた、両リポに `feat/unity-battle-depiction`
- **確認**: 再コンパイル後のエラー 0 / EditMode 60 / 60 / dotnet test 54 / 54 / Play で 15 枚撮って拡大確認。1 行動は最長 1.97 秒（順 6）
- **気づき（未対応）**: `BattleScreenView` がどのシーンでも自動起動する（描写シーンでは `DepictionPlayer.Start` が自動生成分だけ消す）。Unity リポの既存 C# に改行コードだけの差分が出る
- **レポート**: `docs/reports/2026-09-19-unity-battle-depiction.html`（Artifact `https://claude.ai/artifact/MLZm2svaAm6L24HnnNZSMR`）

### 2026-09-19 - カードバランスの前提 8 件の決定と Unity 戦闘描写セッションの前準備

#### 概要

カード 80 種に 2026-09-14 / 09-16 の決定を当てはめて分析し、こうだいさんの回答 12 件で前提を決めた。Unity Editor で戦闘の描写を作るセッションのプランと冒頭プロンプトを書いた。

#### 変更点

- **分析**: 列 = コストにすると、アタック 8 / 12 / 16 は 1 スタミナあたり 8 / 6 / 5.3 で安い札ほど得になる。回復の値が未定で、2 だと複数枚前提の特性 25 個が発火しない。距離の廃止で、単属性ムーブの 4 段・最適間合いの列・遠用の目盛り・位置(中)・`shift` 2・T0 の列が意味を失っていた。
- **決定（`battle_core_v4.md` §18）**: 回復 3 / 目盛り 6 / 13 / 21（ガード 4 / 9 / 15）と 4 列目 30 / 22 / 状態は使うと減る型とターンで減る型 / 強化 ×1.5 / 単属性ムーブは コスト 1 + 付随効果 / 位置の特性 16 枚 / 最適間合いの列を廃止。数値は叩き台で、試験台で測る。
- **Unity 描写の方針**: 固定台本で描写だけ先に作る / 配置はプレハブ、動きはコード（`battle_ui_ux_v2.md` §6 方針 1 を置き換え）/ 1 ターンの縦切り / 影絵のまま。C# は `unity-port/` が正本、プレハブ / シーン / .meta は Unity リポが正本。
- **プラン**: `vision/plans/2026-09-19-unity-battle-depiction.md`（前準備 / 台本 7 出来事 / 完了条件 / 冒頭プロンプト）。
- **レポート**: `docs/reports/2026-09-19-card-balance-and-unity-prep.html`（Artifact `https://claude.ai/artifact/4KxwAn9VwA2JsNRnRqV5i9`）。
- **見つけたが直していないもの**: Unity リポの初回コミットと URP 17.5.0 への修正が未実施。`unity-editor-mcp` は登録済みだがセッションに出ず、古い `unity.exe`（CLI）が 10 個残っている。`battle_ui_ux_v2.md` §6 と `swordsman_cards_v4.md` は未改訂。

### 2026-09-16 - 保留中の判断 95 件への回答と設計書への反映（状態のスタック制、位置はボスと精鋭だけ、コストは旧段表の列）

#### 概要

保留中の判断 95 件に回答し、設計書へ反映した。前提は 2026-09-14 の決定（`battle_core_v4.md` §16 / `battle_ui_ux_v2.md` §10）で、既存の節は消さずに新しい節として足し、置き換わる旧記述には一行の注記だけを入れた。本文の全面改訂（v4.2 / v2.1）は別タスクに回した。

#### 変更点

- **回答の内訳（一覧 A〜K の行数）**: 操作 4 / 固定コスト 8 / 近間・遠間 13 / 背水 3 / 状態 8 / 規則 11 / 開示度 7 / 習熟と継承 9 / 探索とゲーム全体 10 / UI と演出 7 / 色とアート運用 12。行の合計は 92 で、見出しの 95 件は一覧の題と報告 HTML の数字に合わせた
- **反映先**: `battle_core_v4.md` §17（17.1〜17.9）、`battle_ui_ux_v2.md` §11（11.1〜11.7）、`concept-v3.md` §14、`tools-and-prerequisites.md` §9 #18〜#29、`visual-production-pipeline/SKILL.md` の S0 と U3。置き換わる旧記述には「2026-09-16 の決定で置き換え（→ §xx）」の一行注記だけを入れた
- **状態をスタック制へ**: カードは「<状態名> を n 付与する」と書く。既定は付いた側のターン開始に 1 減る。1 回きりだった 威圧 / 脆化 / 疲労 / 強化 / 集中 / 見切り も全てスタックに揃えた。出血と再生はスタックが強さと回数を兼ねる。上限はプレイヤーの種類 6 つだけで、敵には置かない。軽足は「俊敏」へ改名した
- **位置はボスと精鋭だけ**: 近間 / 遠間を持つ敵を 瘴気の司祭 / 甲冑の番人 / 群れの長 の 3 体に絞り、通常 6 体は位置を持たない。カードの位置条件は自分の位置だけを読み、判定は常に動く前に行う。決定木は位置を持たない敵で 2 通り、ボスと精鋭で 4 通りになる
- **コストは旧段表の列**: カードごとに T0〜T3 から 1 列を選び、列番号がコスト、列の値が効果になる。満たしにくい条件を持つ札ほど安い列に置く。コスト 0 は習熟の「軽さ」でだけ生まれ、初期のカードと敵の技は 1 以上にする
- **3 段目は 1 枚と才能**: 習熟の 3 段目を持てるのは 1 つの生で 1 枚だけ。開始時に選んだ才能の 1 枚は刻みが 2 倍で溜まる。刻みは満たした文脈の数だけ加算し（1 回のプレイで 0〜4）、継ぐときは段をそのまま継ぐ。遺産の候補は習熟 1 段以上のカードと埋まった頁を全部並べる。2 段目以上のカードは階層 3 以上で死んだときだけ遺産になる。生存者は次の 1 生だけツール枠 +1 とデッキ上限 +5 を得る。刻限の訓練は 1 回で指定した 1 枚に刻み 1 を入れる
- **相手の選び方**: 受け皿と投げ上げ線の併用に決めた。単体の攻撃とデバフは受け皿、全体攻撃と自分向きの札は投げ上げ線を使う。皿の外で離した単体向きの札は手札に戻る
- **背水の陣（#80 の置き換え）**: 自分が遠間で威力が伸び、スタミナ 2 以下でさらに上乗せする単属性のアタックにした。叩き台は威力 8、遠間で +5、スタミナ 2 以下でさらに +3
- **規則の訂正**: §9 手順 7 の Guard +2 を +3 に直した。手札を捨てるのはターン終了だけにした。ダメージは足してから掛ける。敵側の「相手の Guard が 0」は「無防備」へ改名した。全体攻撃は規則だけ残し、カードは 100% の段階で足す
- **探索とゲーム全体**: デッキは下限 20 / 上限 40、刻限は 1 ノード 1 回、ツールは共通 3 枠、消耗品 3 枠、クリアは最終ボス「歪みの根」の撃破、クラスは剣士固定、連戦は既定 3 戦と選べる 9 戦。休憩は毎回選べる。アーマーの解除は、試験台の基準 9 項目が全て目標値に入った時点を「一巡」とする
- **開示度と演出**: 段数は 3 段のまま、1 段目は種別と咎める側の一字を出す。上げ方は遭遇で 1、観察で 2 とした。観察カードの習熟 3 段目は情報だけでなく状態も付与できる技になる。演出の強弱は面の確定値で 4 段に分け、特性の発火には短い合図を重ねる
- **色とアート運用**: 属性 5 色は役割色を借りる。24 px 以上の文字は 3:1 でよく、近い色の 4 組は形の差で見分ける。描画は CLIP STUDIO PAINT PRO（買い切り 6,900 円）を買う。C# は `unity-port/` を正本にしたまま Unity 側へ写し、写しもコミットする。RPG-by-card の初回コミットを入れ、URP は 17.5.0 に直す
- **資料**: 判断の一覧と根拠を `docs/reports/2026-09-16-v4-2-decisions.html` に、外部 AI に渡すゲームの説明を `docs/briefs/2026-09-16-game-brief.md` にまとめた（Artifact `https://claude.ai/code/artifact/4e4f93e8-bf8e-4715-b78d-4ea0686ea319`）
- **触っていないもの**: `src/`、`unity-port/`、`swordsman_cards_v4.md` と `enemy_roster_v4.md`、モックアップ、README（Development History の節が無い）。設計書の本文に入れたのは誤記の訂正だけで（`battle_core_v4.md:254` の手順 4、`:260` の手順 7、`:91` の手薄）、v4.2 / v2.1 の全面改訂は別タスクに残した

### 2026-09-14 - 戦闘への所感 3 点の反映と判断待ちへの回答（近間 / 遠間、投入量の廃止、画面の簡素化）

#### 概要

こうだいさんの所感 3 点（画面の情報量を減らす / 間合いをキャラクターごとの 2 値にする / カードごとのスタミナ振り分けをやめる）を、影響範囲の洗い出しと 3 案の採点を経て設計書に決定として記録した。視覚制作の判断 16 件と所感から出た判断を 8 回の質問で確認し、回答を反映した。相手の選び方は、4 方式を触って決めるためのデモを作った。

#### 変更点

- **規則**: `battle_core_v4.md` §16（所感と回答時の補足の原文、決定 3 つ、置き換わる記述、間合いは近間 / 遠間でキャラクターごとの常設の値・素の効果なし・ムーブ面で切り替え、投入量の廃止とコストの決め方は保留、習熟は旧段表を上る、背水の陣は #80 を置き換え、回答表）。§0 / §1 / §3 / §14 に見直し中の印
- **UI**: `battle_ui_ux_v2.md` §10（上帯をやめて左上にターン・階層・連戦・瘴気、語の予算 10 字、投入帯の廃止、相手の選び方はデモで決める、近間 / 遠間は立ち位置と一字札、Particle 許可、Yuji Syuku 不使用）。旧 §10 変更履歴を §11 に振り直し、§8 の条件 1 / 2 / 4 を △ に戻した
- **注記**: カード表・敵ロースター・tier1 / tier2・concept-v3 に見直し中の注記。実装プランは ON HOLD
- **視覚制作**: `tools-and-prerequisites.md` §9 を回答済みの 17 件に（体勢差分なし・差分 66 枚・候補 80 枚で約 $5.4、Particle 許可、Yuji Syuku 不使用、作業ファイルは OneDrive、月の上限 $30 など）。SKILL.md の `pose` 区分・`-pose`・Particle 禁止を直した
- **デモ**: `docs/mockups/2026-09-14-drag-select-demo.html`（受け皿 / ボタン列 / タップして確認 / 投げ上げ線 × 1 体 / 2 体、操作回数と時間の比較、自己テスト 26 項目）。Artifact `https://claude.ai/code/artifact/467a57de-b088-46d8-a834-5cd756440f6d`。確認役の指摘 3 件（D の 2 体戦の境目を 2 体の中点へ、ポインタのキャプチャ、舞台の高さを実測で合わせる）を直し、自己テストを再実行した
- **.env**: `chore/ignore-env`（PR #21、main 向け）。RPG-by-card はステージ済みの `.gitignore` に `.env`、`.gitattributes` に `*.psb` / `*.moc3` の LFS を追加（コミットは Unity リポの運用を決めるとき）
- **残る判断**: 固定コストの決め方、相手の選び方（デモの後）、背水の陣の効果
- **触っていないもの**: モックアップ v4（作り直しと座標修正は別項目）、`unity-port/`、`src/`、RPG-by-card のコミット

### 2026-09-14 - キャラクターの外見と UI/UX を作るツールと前提の調査、一連実行スキル visual-production-pipeline

#### 概要

敵 9 体とプレイヤーの外見、戦闘の UI / UX と演出を作るためのツールと前提を、6 観点の並列調査・反証・批評のワークフローでまとめ、HTML レポートと Artifact と life-editor の Note（タグ card-battle）に残した。同じ流れと、その先の制作（setup / character / background / ui / vfx）を回すプロジェクトスキル `visual-production-pipeline` を作った。

#### 変更点

- **レポート**: `docs/reports/2026-09-14-visual-production-survey.html`（結論の 3 分類、組む順、決定済みの前提、手元の環境、工程別ツール 14 行、作るもの、読みやすさの計算、法務、リスク、スキル、事実確認の経過、仮定、未確認、判断 10 件）。Artifact `https://claude.ai/code/artifact/3fcc2afa-df5e-40af-b4c4-d1998d18f8cf`
- **Note**: life-editor `note-423a0061-7e79-471c-8166-757153d91c75`（タグ card-battle を新規作成、url-in-body=yes）
- **スキル**: `.claude/skills/visual-production-pipeline/`（SKILL.md のモード 6 つと工程 S0–S1 / C0–C11 / B1 / U0–U8 / V0–V3、正本 `references/tools-and-prerequisites.md`、`workflows/survey.js`、`scripts/silhouette.py`、`scripts/color_check.py`、`templates/asset-ledger.csv` 30 列）。`.claude/skills/README.md` と `CLAUDE.md` の Skills Quick Reference に登録
- **調査の経過**: 14 エージェント。外部の主張 265 件（確認 231 / 訂正 30 / 未確認 4）。批評の漏れ 14 件と誤り 6 件を補完して書き直した。内部 5 件と外部 5 件を直接確認し、OpenAI の 1 枚あたり価格だけ公式ページに無いので未確認へ落とした。`color_check.py` が統合結果の計算値を再現し、`silhouette.py` と `survey.js` の構文も確認した
- **見つけたが直していないもの**: 両リポジトリで `.env` が無視されていない。RPG-by-card の URP が manifest 17.6.0 と lock 17.5.0 で食い違う。v2 の中で Yuji Syuku の扱いが食い違う。`concept-v3.md:14` と `:16` で主人公の扱いが食い違う。omen / boss / whiff が階層 1 の背景で 4.5:1 未満
- **判断待ち**: Live2D の範囲、最初に通す 1 体、Unity の版、生成の主経路と月上限、主人公の世代差、CSP の購入、`.env` と LFS の修正、色の規則、Yuji Syuku、art の範囲（全 16 件は正本の §9）
- **触っていないもの**: `src/`、`unity-port/`、設計書（`battle_document/` / `enemy_document/` / `vision/`）、RPG-by-card、未追跡の `docs/reports/2026-09-07-unity-scope-inventory.html`。README に Development History の節が無いので README は更新していない

### 2026-09-14 - 戦闘 UI / UX v2（core v4.1 対応の設計書、動くモックアップ、実画面の目視検証）

#### 概要

戦闘コア v4.1（属性 5 つ / 特性 / 状態 10 語 + ボス専用 2 / スタンス枠 / 手札 5 枚 / 2 体戦 / 精鋭とボス / 連戦）に合わせて、戦闘 UI / UX の設計書を v2 として新設した。レイアウトは 3 案を採点して「背骨とレーン」を採用し、操作・演出・Unity 写像の草稿を批評にかけてから書き、3 観点で 2 ラウンド検証した。モックアップを実画面で撮って見つけた 5 件（予測札が矢印の先を隠す、追撃バッジの食い込み、CSS のクラス名衝突、スタミナ表記、手記の閉じるボタン）は、設計書とモックアップの両方で直した。

#### 変更点

- **設計書**: `battle_document/battle_ui_ux_v2.md` を新設。§0 差分と 80% の条件 7 つ、§1 情報設計 26 項目、§2 L1 v2 と 2 体戦・最悪ケース 2 つ・対案 2 つ・休憩と結果画面、§3 ドラッグ主操作とクリック / キー / ゲームパッド、§4 A+ 継承と v4.1 のトークン、§5 演出 21 節と時間予算、§6 Unity 写像（UiTween 継続、入力の規則、View v1.1 からの差分、アセット）、§7 開示度、§8 達成状況（○ 6 / △ 1）、§9 ルールへの要望 25 件。`battle_ui_ux_v1.md` の冒頭に正本移動の注記を置き、View v1.1 の実装記録として残した
- **モックアップ**: `docs/mockups/2026-09-13-battle-uiux-v4-mockup.html`（1 ファイル、6 画面: 配置と対案 / 2 体戦 / 精鋭とボス / 操作（動く）/ 1 ターン再生 / 連戦）。Artifact `https://claude.ai/code/artifact/575f0640-7eba-4486-9c79-edd94d805de7`
- **レポート**: `docs/reports/2026-09-13-battle-uiux-v4-80.html`（採用 / 保留 / 見送り、達成状況、採点、時間予算、検証の経過、実画面 10 枚と所見、残り 20%、要望、判断点）。Artifact `https://claude.ai/code/artifact/43c67b94-a62e-428e-b19b-836c510a05ce`
- **要件 / 索引**: `requirements/tier2-support.md` R2-5 の内容と受け入れ基準を 16 項目に更新。`docs/INDEX.md` に v2 の行
- **検証**: 設計書はルール一致 / 充足と整合 / Unity 実現性の 3 観点で 2 ラウンド（blocking 4 から 2、修正 42 + 35 件）。モックアップは静的検査（should 8 / nit 12、修正 20 件）と、ヘッドレス Chrome で 22 枚撮って切り抜く目視。ラウンド 2 の修正後の 3 観点の再検証は未実施
- **判断待ち**: 対案 A の実測、要望 5 件（2 段予兆の確定性 / 重撃 / 威圧 / 呪縛 / ダメージ式の項の位置）、正本の誤記 2 件（core §9 手順 7 の +2、手順 4 と 8 の捨ての二重）、演出上限の緩和 3 か所（二属性 1.4 から 1.5 倍、置き換え 2,400 ms、ターン開始の最悪 2,100 ms）、属性 5 色の値
- **触っていないもの**: `src/`、`unity-port/`、`battle_core_v4.md` / `swordsman_cards_v4.md` / `enemy_roster_v4.md`、既存のモックアップとレポート。README に Development History の節が無いので README は更新していない

### 2026-09-13 - 戦闘コア v4 の設計確定（7 決定の反映、カード 80 種、敵 9 体、次セッション用プラン）

#### 概要

棚卸への 7 決定（移動も属性にして特性の基準にする / ドラッグ + 矢印 + 場 / T0 に弱い効果を残す / スタンス枠 1 / 状態 10 語 + ボス専用 / 敵 9 体と連戦モード / 設計に集中して実装は次セッション）を設計書に落とした。`battle_core_v4.md` を新設して数値の正本を v3 から移し、剣士カード 80 種（初期 40 / 習得 40）と 80% 用の敵 9 体を具体化した。実装は次セッションで、冒頭プロンプトを実装プランに置いた。

#### 変更点

- **規則**: `battle_document/battle_core_v4.md`（§0 差分表、§2 属性 5 つと面・解決順・特性 8 条件 × 7 効果・スキーマ追加項目 push / hits / ally、§3 T0 の扱いと minInvest 0 は 20 種まで、§4 スタンス枠 1、§5 状態 10 語 + ボス専用 2、§7 敵ごとの間合いと 2 体戦、§8 手札 5 枚 / 全捨て / デッキ 15〜80 / 同種 3、§9 ターン進行、§10 定数、§11 実装への写像、§12 連戦モード、§13 試験台の基準 9 項目、§14 未確定 6 点）
- **カード**: `card_document/swordsman_cards_v4.md`（80 種。単属性 40 / 二属性 40、minInvest 0 は 20、アタック面 38 種で 近 15 / 中 14 / 遠 9、特性 20 枚、探索の面候補 12。ボス 瘴気の司祭の固有 3 種を含む）
- **敵**: `enemy_document/enemy_roster_v4.md`（通常 6: 長柄の歪み兵 / 影走りの犬 / 錆びた鎧の亡者 / 弩の狩人 / 靄の射手 / 双刃の歪み兵、精鋭 2: 甲冑の番人 / 群れの長 + 取り巻きの犬、ボス 1: 瘴気の司祭（適応 3 条件、専用の状態 瘴気纏い / 呪縛）。100% 用の残り 10 体は名前と役割。連戦の順 §6）
- **UI**: `battle_ui_ux_v1.md` v1.2 §3.6（帯 = 投入量、矢印 = 相手、場 = 自分と全体、手札 5 枚の扇、HUD 追加部品、演出の文法）
- **要件 / 索引 / プラン**: tier2 R2-2 / R2-3 に v4 の起票と受け入れ基準を追記。`INDEX.md` に v4 の行。`vision/plans/2026-09-13-battle-v4-implementation.md`（完了条件 5 つ + 次セッション冒頭のプロンプト）
- **レポート**: `docs/reports/2026-09-13-battle-v4-cards-enemies.html`（決定の反映、規則の差分、カード 80 種の表、敵 9 体の表、次セッションのプロンプト）。前編 `2026-09-13-battle-100-inventory.html` に決定済みの注記
- **2 回目の決定（同日）**: 目盛りを T0 アタック = 5 に合わせて全体を約 1.6 倍（単属性 5 / 8 / 12 / 16、HP 50、敵 HP 通常 50〜70 / 精鋭 90〜110 / ボス 160、構え +3、出血 / 再生 2）。特性を条件 12 × 効果 10 に広げ（重撃 / 転換 / 追撃、連打 / 手薄 / 相手の状態 / 自分の状態）、初期 32 + 習得 40 = 72 枚に付与。特性の無い 8 枚は威力 / Guard +2 で補う。敵は常時 Guard のスタンスだけ 1.6 倍せず（ターン数を守るため）、特性 15 個。core v4.1 / cards v4.1 / roster v4.1
- **未確定**: 「0 投入 5 × 5 枚」（試験台の最初の基準。超えたら T0 を 3 に戻すか minInvest 0 を減らす）

### 2026-09-13 - 戦闘 100% の棚卸（7 条件 × core v3、8 領域 58 項目、80% までの順番）

#### 概要

こうだいさんの方針「バトルを 80% まで仕上げてから探索へ」を受け、7 条件（1 ターン 5 枚ドロー、タイプ 4 種と 2 つまでの重複、同種 3 枚まで、80 種のうち 40 種は習得、ホバーとドラッグのカード感、カードごとの攻撃エフェクト）を `battle_core_v3.md` に突き合わせ、100% に要る要素を棚卸した。衝突 4 点は全て v3 を改訂する側で解き、実装済みの投入 / 間合い / 予兆 / 構え / 崩しは残す。現在地は重み付きで約 20%。

#### 変更点

- **レポート**: `docs/reports/2026-09-13-battle-100-inventory.html`（Artifact `https://claude.ai/code/artifact/3f7541f9-f0fe-4e74-aef4-d07737d40e85`）。§1 条件の突き合わせ、§2 タイプ 4 種と 10 パターン（移動は属性）、§3 状態 10 語とスタンス枠 1、§4 ターンの流れ v4 案と「0 投入 × 5 枚」対策（T0 は威力 0）、§5 カード 80 種の配分（初期 40 / 習得 40、単独 40 / 二面 40）、§6 敵 19 体（80% は 9 体）、§7 試験台の基準 9 項目、§8 ドラッグの高さで投入量を決める操作と HUD の追加部品、§9 演出の文法（系統 8 × 投入段 × 固有の飾り）、§10 棚卸表 58 項目、§11 80% までの 6 段階
- **タスク**: MEMORY の予定に「戦闘を 80% へ（順 1〜6）」を追加。判断待ち 7 点
- **触っていないもの**: 設計書本体（v4 は判断後に書く）、`unity-port/`、`src/`

### 2026-09-12 - 戦闘 UI v1.1: 見た目 A 採用 + 鮮やかさと描き込み + 戦闘コア v3 と UGUI View を Unity に実装

#### 概要

こうだいさんの決定（A 採用、実画面はもう少し鮮やかで描き込みを、敗北画面と手記は凝ってよい）を設計書 v1.1 とモックアップに反映し、そのまま Unity に実装した。`unity-port/BattleCore/` を battle_core_v3（投入量 0〜3、予兆、敵 Guard、構え、崩し、冷静 / 死力、瘴気、探索からの入力、演出用イベント列）へ全面改修し、`unity-project-kit/Assets/View/` に L1 レイアウトと A+ トークンの View を 11 ファイルで書いた。`dotnet test` 54 / 54、Unity EditMode 54 / 54、Windows プレイヤーをビルドしてスクリーンショットで配置を確認し、重なり 4 か所を直した。

#### 変更点

- **設計書 / モックアップ**: `battle_ui_ux_v1.md` v1.1（§4.4 A 採用、§4.5 A+ トークンと描き込み 11 要素、§4.6 敗北画面、§4.7 手記ドロワー、§6.4 実装状況）。モックアップの skin A を A+ に更新し、敗北の例と手記ドロワーを作り替え（Artifact v2）
- **戦闘コア v3（C#）**: `Types.cs`（Tier / CardDef / EnemyDef / Omen / BattleInit / BattleEvent 群）、`Constants.cs`（§10 の定数）、`Combat.cs`（間合い補正・丸めは AwayFromZero・Guard 適用・構え・瘴気ペナルティ・投入量の選択）、`Cards.cs`（剣士 6 種 + 応急処置の 4 段表）、`Enemy.cs`（長柄の歪み兵 5 行動・決定木・予兆・空振り回避）、`BattleReducer.cs`（§9 のターン進行）、`ViewModel.cs`（TierView / OmenView / JournalView / 既定投入）、`IBattleView.cs`（HUD 全項目）、`BattleStore.cs`（`BattleInit` 受け取り）
- **テスト**: 4 ファイルを v3 向けに書き直し（54 件）。`ParityTests.cs` は `V2_PARITY` 定義時だけコンパイル。`tools/gen-trace-actions.mjs` は `TRACE_V2=1` が無いと停止。`Resources/trace-actions.txt` を v3 形式（勝利まで 77 手）で再生成
- **View（UGUI）**: `BattleTheme` / `UiTween` / `ProceduralArt` / `UiKit` / `ArenaView` / `BattleHud` / `HandView` / `JournalDrawer` / `ResultOverlay` / `BattleDirector` / `BattleScreenView`。イベント列を §5 の ms で再生してから確定値を描く。`Application.runInBackground`、`-captureDir` の定期スクリーンショット、`-replayTrace` を追加
- **同期 / 文書**: `sync-unity-project.mjs` が View の全 .cs を写す。`unity-port/README.md` に v3 の使い方。レポート `docs/reports/2026-09-12-battle-uiux-80.html` に実装節とスクリーンショット
- **人手が要る残り**: Editor 前面での手触り確認（乱数「実戦」）、本物の立ち絵 / 書体 / 効果音

### 2026-09-12 - 戦闘 UI / UX の 80% 設計（情報設計・操作・見た目 3 案・演出仕様・Unity 写像）

#### 概要

戦闘画面の UI / UX を「完成度 80%」まで設計した（実装は先、コードは書かない）。`battle_core_v3.md` の値 5 つと予兆を「敵の頭上 → 床の狙い帯 → 手札の上のスタミナ」の縦一列に置き、1 枚のプレイをカード選択 → 投入量チップの 2 操作に収めた。見た目は A 霧と灯り / B 鉄と革 / C 墨と朱 の 3 案をモックアップで比べ、A を推奨（最終選択はこうだいさん）。演出 10 節を ms / 補間 / 音の有無で表にし、Unity の Canvas 6 層と DOTween / Animator の分担、アセット一覧に落とした。

#### 変更点

- **設計書**: `.claude/docs/battle_document/battle_ui_ux_v1.md` を新設（§1 情報設計 20 項目、§2 L1 / L2、§3 操作、§4 見た目 3 案と階層 5 段、§5 演出、§6 Unity 写像、§7 開示度の仮置き、§8 残り 20%、§9 ルールへの要望 5 件）
- **モックアップ**: `docs/mockups/2026-09-12-battle-uiux-mockup.html`（1 ファイル完結。見た目 3 案 × 階層 3 段 × レイアウト 2 種 × 開示度、手札が実際に操作でき、1 ターン再生と 被弾 / 崩し / 勝敗 の例）。Artifact `https://claude.ai/code/artifact/750029ad-d93f-4cdc-b923-9dce0b1bd041`
- **レポート**: `docs/reports/2026-09-12-battle-uiux-80.html`（採用 / 保留 / 見送り、達成状況、演出の主要値、残り 20%、仮定と要望）。Artifact `https://claude.ai/code/artifact/4712190b-73c3-48b1-a07e-b725cfb6fcd1`
- **要件**: `requirements/tier2-support.md` R2-5 の内容と受け入れ基準を 9 項目に更新。`docs/INDEX.md` に設計書を追記
- **ルール側への要望**: 開示度の段階定義 / 崩し後の低投入見込みの表示可否 / clamp のイベント通知 / 敵 Guard の対称表示 / 端数処理（MidpointRounding）の確定
- **ブランチ**: `docs/battle-uiux-80`（未マージの `docs/concept-v3-inheritance-loop` から分岐）

### 2026-09-12 - 三者評価を受けた 5 決定の反映（瘴気 / 手記 / HP 回復 / 遺産の残存 / セーブ前倒し / C# 正本化）

#### 概要

三者評価の判断待ち 6 点にこうだいさんが回答し、設計書へ反映した。衰弱を「瘴気」に改名して階層ごとの濃度で蓄積する仕組みにし、図鑑を「手記」（持ち歩く帳面、メモあり、戦闘中は読むだけ、死亡地点に残り選んだ頁だけ継ぐ）に改めた。HP の回復モデル（戦闘中カード / 階層間休憩 / 探索イベント）と遺産の残存規則（痕跡は回収まで残る、1 つの生で 1 件）を決め、セーブ要件を Phase 4 の頭へ前倒しし、戦闘コアの正を C# に切り替えた。

#### 変更点

- **瘴気**（concept-v3 §6、battle_core_v3 §3.1）: 階層 n の濃度 = min(n, 5)。刻限 1 行動ごとに濃度 × 1% 蓄積。20% ごとに最大スタミナ -1、100% で瘴気死。和らげる手段: 防瘴の面（濃度 -1）/ 浄化の香（蓄積 -10%）/ 階層間休憩（蓄積しない）。定数 MIASMA_* を追加
- **HP と回復**（concept-v3 §7.1、battle_core_v3 §4 / §7.2）: HP は戦闘をまたいで持ち越す。戦闘中は `heal` 型カード（応急処置 T1 3 / T2 5 / T3 7）、階層間休憩 30%、階層内休息 15%（刻限 -1）、泉・薬草イベント。無償の全回復は無い
- **手記**（concept-v3 §5、tier1 R1-10、CAMP §3.3）: 敵の頁 / ダンジョンの頁 / メモ。拠点・探索・戦闘のどこでも読めるが、戦闘中は現在の敵の頁を表示するだけで戦闘に効果を与えない。死亡時は痕跡に含めて死亡地点に残し、遺産の枠で選んだ頁だけ継ぐ。concept-v2 A1 は「一部復活」に分類変更
- **遺産の残存**（concept-v3 §8.3、tier1 R1-12、CAMP §3.1）: 痕跡は回収されるまで残る（次の死亡で上書きしない）。1 つの生で受け取れるのは 1 件。企画書 §16「前回死亡者だけ」からの変更として明記
- **セーブ前倒し**（tier1 R1-16）: Phase 6 → Phase 4 の頭。依存を R1-8〜10 に変更し、R1-11 が R1-16 に依存。死亡と生の終了は確定と同時に保存（セーブスカム防止）
- **C# が正**（tier1 冒頭・R1-3、core.md、battle_core_v3 §13、CLAUDE.md、`unity-port/README.md` バナー）: TS の `src/ui/battle-lab/core/` は凍結。`parity:*` は履歴として残し更新しない。R1-3 の受け入れ基準を `dotnet test` に変更
- **名称の一括置換**: 衰弱 → 瘴気、図鑑 → 手記を concept-v3 / core / tier1〜3 / master / CAMP / battle_core_v3 / CLAUDE.md で置換。concept-v3 §13 に決定表を追加、§12 の 2 / 20 / 21 を決定済みに
- **未変更**: コード（`src/` / `unity-port/*.cs`）。評価レポート 2 本は当時の記録として据え置き
- **追加決定（同日）**: 生存ルートでは手記を丸ごと受け継ぐ（死亡より見返りを大きくする）。釣り合いとして探索にランダムな報酬・イベントを持たせる（concept-v3 §7.3 / §8.2 / §12-22、tier1 R1-13、tier2 R2-4、CAMP、master）
- **次セッションのプロンプト**: `docs/prompts/2026-09-12-next-session-battle-uiux.md`（戦闘 UI / UX を 80% に。設計のみ）

### 2026-09-12 - 戦闘コア v3 設計 + 単一ダンジョン化 + 三者評価

#### 概要

前セッションの上書き（concept-v3）を受け、(1) 戦闘の基礎要素を整理して `battle_document/battle_core_v3.md` v1 に設計として落とし、(2) ダンジョンを「一つを深く潜る」構造に変更（ノード式マップは継続、後で変更の余地あり）、(3) 旧個別施設設計 4 本を archive へ移動、(4) 独立エージェント 3 体（ゲームデザイン / 実装 / 反対弁護人）で新設計を旧設計と比較評価した。3 体とも「方向は改善、現状の完成度は旧を下回る（6 → 5）」で一致。評価で見つかった転記ミスは修正し、設計判断は判断待ちとして残した。

#### 変更点

- **戦闘コア v3 設計書（R1-2 成果物）**: 持つ値を HP / Guard / スタミナ（現在・最大） / 間合い / 予兆 の 5 つに限定。間合い相性 1.0 / 0.5 / 0.15 と間合い依存回復（近 1 / 中 2 / 遠 3）は v2 実機検証済みを継承。スタミナ基礎 10（3〜14）、投入 0〜3 がコスト、minInvest、疲労減衰は投入制に吸収。構え（残 3 以上で Guard +2）、崩し = スタミナ削り（体勢ゲージ不採用）、予兆コミット式 + 空振り回避、EnemyDef 決定木 + homeRange、剣士 6 種と長柄兵の投入表、C# 写像表、プレイテスト 6 観点
- **concept-v3 §12**: 3 / 7 / 16 / 18 を決定、8 は基礎部分を決定、14 は単一ダンジョンに決定。評価で指摘の 20〜23（HP 回復 / 遺産の連鎖消失 / 生存ルートの図鑑 / クラス選択）を追加
- **単一ダンジョン化**: concept-v3 / core.md / tier1 / tier2 R2-1 / master / CAMP から「ステージ選択」を除去。R2-1 を「単一ダンジョン・ノード式の設計書化（マップ方式を差し替えられる境界）」に改稿
- **archive**: `shop / blacksmith / sanctuary / guild_design.md` を `.claude/archive/camp_document/` へ git mv。INDEX と CAMP の参照を更新
- **評価で修正した転記ミス**: 遺産の受け取り場所を「死亡地点で選ぶ」に統一（concept-v3 / master / CAMP / CLAUDE.md / tier1 R1-12）。「企画書に経済の記述なし」を「通貨と店の記述なし」に訂正。バナー未付与 11 本（enemy_document 6 / element_system_spec / buff_debuff / ui_ux_design_guide / inventory_design / kickoff）に付与。tier1 R1-1 の受け入れ基準を事実に合わせ「v3 版ギャップ分析」を追加
- **レポート**: `docs/reports/2026-09-12-concept-v3-evaluation.html`（Artifact 発行）
- **判断待ち**: HP 回復モデル / 遺産の連鎖消失 / 図鑑消失の方針 / セーブ前倒し / TS-C# パリティ継続 / 戦闘 v1 でのプロト。README は Development History 節が無いため未更新
