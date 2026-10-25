# HISTORY (chat-battle)

### 2026-10-21 - battle 宛ての Issue 4 件を 4 本の PR へ

#### 概要

battle 宛ての open Issue 4 件を、Issue ごとのブランチと PR にした。4 本とも build / lint / test:run / dotnet test が exit 0 で、merge は人の作業。#589 は 3 本に分ける Issue の 1 本目で、Issue は開いたまま。

#### 変更点

- **#646（PR #658）**: `MasteryProfilesTests` の `Has.Count` 1 行を `RankTraits!.Count` に。Unity の NUnit 3.5 で配列に `Count` が無く落ちていた
- **#615（PR #664）**: `unity:check` が古い写しで exit 3、未コンパイルのアセンブリで exit 4 を返す。`--allow-leftovers` / `--allow-unbuilt` で従来の 0。持ち場のフォルダを `unity-sync-plan.mjs` の `OWNED_ROOTS` に 1 か所で並べ、`unity:sync --prune` で古い写しと `.meta` を消す
- **#643（PR #665）**: 札のドラッグをフレームごとに JSON 1 行で記録する `DragTraceRecorder`（新規）と、行を作る純 C# の `DragTrace`。`DepictionPlayer.cs` と `CardView.cs` は無変更。掴んだ札は private の `_dragging` をリフレクションで読む
- **#589 の 1 本目（PR #667）**: `DepictionFrame.Enemies`（敵ごとの数値パネル・スタンス一覧・予兆・マス・斜線の向き・色見本の印）と `Cue.Unit`。`CoreScriptWriter` がコアのイベントを `BattleEvent.Unit` で振り分ける。数値パネル 3 枚以上を重ねない `StatusPanelLayout.PlaceRow`

#### 実測・知見

- **2 体の戦闘では、これまで 2 体目のイベントが 1 体目の数字と予兆に入っていた**。探索から 2 体の組に入ると、予兆の札が 2 体目のもので上書きされていた（PR #667 で台本側は直った。画面は 1 体ぶんのまま）
- **本物の Unity プロジェクトの古い写しは 0 件**、`unity:check` は 20 アセンブリで exit 0（2026-10-21）。`Exploration.Bridge` も Unity でコンパイル済みだった
- `unity:sync` の書き込みは、merge 前のコードをメインの Unity に置かないよう、4 本とも `--dry-run` だけにした

#### 次

- #589 の 2 本目（画面の部品）と 3 本目（斜線の向きと色見本）。`DepictionPlayer.cs` を触る #585 / #606 / #612 / #619 が落ち着いてから main で切る
- merge の後、メインのチャットが `unity:sync` と Unity の EditMode（#646）、ドラッグの記録（#643）を確かめる

### 2026-10-10 - battle 宛ての Issue 20 件を 20 本の PR へ

#### 概要

battle 宛ての open Issue 20 件を、Issue ごとのブランチと PR にした。実装は Issue ごとに別の作業場所で並列に進め、1 件ずつ独立したレビューと手直しを通した。同じ設計書（`battle_core_v4.md` / `battle_ui_ux_v2.md` / `SOURCES.md`）の版の行と変更履歴を触る 15 本は 1 本の鎖に積んだ。19 本は 2026-10-10 に merge 済みで、PR #435（#242）が open。

#### 変更点

- **main から切った 4 本**: #235（CS0618 を 0 件に、PR #416）/ #302（立ち絵の枠を絵の縦横比に合わせる、PR #417）/ #335（呪縛と鉤爪の文言、PR #418）/ #394（燐刃の十字受けを v4.13 に、PR #419）
- **積んだ 15 本**（merge 順）: #364 → #403 → #334 → #59 → #58 → #304 → #311 → #357 → #389 → #30 → #323 → #356 → #361 → #405 → #213（PR #420〜#434）
- **#242**: `battle-visual-v1.md` の形でカード・床・予兆の札・スタミナの札・状態チップを作り直し、ランプと床のマスは台本で渡す（PR #435）。§4〜§7 の残りは申し送りへ
- **申し送り**: 14 件を `.claude/comm/handoff/` に置いた（攻撃の目盛りの読み手 #364、習熟の札の割り当て #59、出立のツールの効果 #58、根の枯らしの膠着 #334、ターン開始の最悪の時間 #361、構えの見せ方 #323、予兆の語の注記 #357、#242 の残り 4 件など）

#### 実測・知見

- **同じ設計書を触るブランチは、main から切ると冒頭の版の行で必ずぶつかる**。20 本の総当たりで 50 組が衝突し、そのほとんどが `battle_core_v4.md` と `battle_ui_ux_v2.md` の Status の行だった
- **20 本をまとめて merge した状態でも `dotnet test` と `unity:check` は exit 0**（BattleCore 657、Bridge 287、Script 185 ほか）
- **ワークフローの一時的な作業場所からは、レーンの `handoff/` へ書けない**。申し送りは各作業場所に置かれ、後から集める必要があった

#### 次

- PR #435 の merge の後、メインのチャットで `Battle.unity` の見た目を確かめる

### 2026-09-27 - Unity の BattleCore の警告 CS8632 と CS0618 を PR へ（#209）

#### 概要

Unity が BattleCore とそのテストを nullable の文脈なしでコンパイルしていたため、`string?` の注釈が 1 つずつ CS8632 になっていた。asmdef の隣に `csc.rsp` を置いて文脈を揃え、PlayMode テスト 1 本の廃止 API を外した。PR #236 は open で、merge は人の手番。

#### 変更点

- **`csc.rsp`**: `unity-port/unity-project-kit/Assets/Core/csc.rsp`（`-nullable:enable` の 1 行）を足した。`unity-port/.gitignore:90` の `*.rsp` に当たるので `git add -f` で入れた
- **同期**: `sync-unity-project.mjs` が同じファイルを `Assets/Core/csc.rsp` と `Assets/Tests/csc.rsp` へ写す。`BattleCore.Tests.csproj` も nullable 有効なので、テスト側にも同じ文脈を渡した
- **CS0618**: `BattlePlaybackTests.cs:94` を `FindObjectsByType<MonoBehaviour>()` へ。`DemoFlowPlaybackTests.cs` と同じ形
- **起票**: #235（`Assets/View/Depiction/` に残る CS0618 4 件。design レーンの作り直しの後）

#### 実測・知見

- **asmdef の隣の `csc.rsp` はそのアセンブリにだけ効き、`Assets/csc.rsp` を置き換える**。Unity 6000.5.5f1 の `ScriptCompilationBuildProgram.exe` と `UnityEditor.CoreModule.dll` を逆コンパイルして確かめた。rsp は空白と CR / LF で区切るので CRLF でも読めるが、`#` のコメントは扱わない
- **Unity の生成した rsp（`Library/Bee/artifacts/*/*.rsp`）を Unity 同梱の csc に渡すと、Editor を起動せずに Unity と同じ警告が出る**。CS8632 は エディタ用 57（BattleCore 43、BattleCore.Tests 14）→ 0、プレイヤー用 43 → 0。新しい警告は出なかった
- **Issue の 86 件はプレイヤー用の 43 件をログが 2 回ずつ書いたものと推定**。Unity のログは同じ警告を 2 回書く（`Logs/Editor-prev.log` 291〜314 行）
- **CS0618 は Issue の見立てより多い**。`FindFirstObjectByType` も廃止で、`BattleBootstrap.cs:49` はプレイヤー用のビルドにも入る
- `npm run build` / `lint` / `test:run` / `dotnet test` / `unity:sync -- --dry-run` は exit 0

#### 次

- メインのチャットが `npm run unity:sync` の後に Console で CS8632 が 0 件かを見て、Unity リポに `csc.rsp` 2 つと `.meta` をコミットする

### 2026-09-26 - デモの使い勝手と §5 / §19 の本文入れ 5 件を PR へ（#203 / #206 / #205 / #207 / #211）

#### 概要

`/goal` で渡された 5 件を、1 件 1 ブランチ（origin/main から。#211 だけ #203 を取り込んで）で PR まで進めた。どの PR も `npm run build` / `lint` / `test:run` / `dotnet test` / `npm run sources -- --check` が exit 0。merge は人の手番で、#224 は上限 4 の確認待ち。

#### 変更点

- **#203 降参**（PR #220）: デモの戦闘中だけ右上に「降参する」。`DepictionPlayer.Halt` で入力と出来事を止め、`DemoSession.Surrender` がその時点の値のまま負けとして集計し、連戦も終わる。コアが決着済みならコアの結果を残す
- **#206 スタンス札 3 枚まで**（PR #223）: `Cards.Validate` と `DeckBuilder.CanAdd` が 4 枚目を断る（Stance 属性で数える、`STANCE_CARDS_MAX`）。S13（倍率 1 つ、脆化が先）と S15 を §5.1 / §8 / §10 / §11 へ入れ、§19 の未反映は習熟の S5〜S8 だけになった。`SOURCES.md` の自分の行を直した
- **#205 ターンで減る型の上限 4**（PR #224、確認待ち）: 3 方向（上限 / 付け方 / 減り方）の案を比べて上限 4 を採った。設計書を先のコミットで書き、コアは `StatusSet` で 4 に止め、`StatusApplied` に `Dropped` / `Landed` を足した。Issue の再現は #206 でデッキが変わるので、固定の 29 枚で回す
- **#207 威圧と見切りの減る境目**（PR #225）: コアの挙動（#188）を §5 に写した。威圧は当たるアタックか Guard の値を持つ行動で減り、見切りは Guard が 1 以上吸収した撃で減る。壁のダメージは撃に数えない。境目のテスト 7 本
- **#211 選択画面と終了画面**（PR #226）: 背景を不透明に、壊れた保存デッキの理由を 1 行、保存は「戦闘へ」の時だけ、1 体戦の後に敵を選び直せる、開始の例外でモード画面へ戻る、休憩の元を 1 つに
- **食い違いの Issue**: #221（疲労の効き方、`battle_core_v4.md` §5 に従った）、#222（燐刃の見切りの手記、同じく §5 に従った）
- **進め方**: 計画は Issue ごとの読み手 + #205 の 3 案比較 + 重なりの点検役で作り、実装は origin/main の写しで並列に組んで差分を当てた。各 PR は別の目（2〜3 観点）で読み直してから出した。View の C# は Unity 6 の DLL を参照する作業用のプロジェクトで、Editor を起動せずにコンパイルを確かめた

#### 次

- #223 / #224 / #225 は `battle_core_v4.md` §15 の先頭 1 行だけが互いに衝突する。後に merge する PR で両方の行を残す
- PlayMode テストと画面での見え方はメインのチャットで確かめる（`npm run unity:sync` の後）

### 2026-09-23 - デモ版 #187 を 5 本の PR へ（#188〜#192）

#### 概要

カードのバランスを実際に戦って確かめるデモを、計画書（2026-09-23 にこうだいさんが判断を確認）どおり 5 本のブランチと PR に分けて作った。`Battle.unity` を Play すると、デッキ選択 → モード選択 → 戦闘 → 終了画面 → 次へ、が 1 周通る形になった。5 本とも PR は open で、merge は人の手番（計画書: archive/2026-09-23-battle-demo.md）。

#### 変更点

- **#188 カード 80 種**（PR #195）: `CardCatalog` を初期 40 と習得 40 の partial に分けて 80 種を入れた。コアに状態 9 語・特性の条件 12 種と効果 10 種・スタンス枠と除外置き場・回復と崩し・このターンに出した属性の並びを足した。コスト −1 は払えるかの判定と予測にも効く。台本には HP の増減の合図（`CueKind.HpChange`）と構えの行を足した。#179 の 3 枚を v4.3 の値へ
- **#189 敵 11 体**（PR #198）: 通常 6・精鋭 2・ボス 3。精鋭とボスの 2 行動（引き直し、同じ行動は 2 度取らない）、スタンスは 1 戦に 1 回、多段攻撃、一覧の予兆の種別。ボスの適応と段階は省き、専用の状態は近い語で代えた。錆槍の竜兵の表示名を揃えた（#179）
- **#190 デッキ選択**（PR #199）: 判定・絞り込み・ページ・文面・試作とランダム・保存用の文字列を Bridge の `DeckBuilder` に置き、画面は実行時に組んだ。`DepictionPlayer` に `Hold` / `Restart` / `BattleFinished`
- **#191 モードと終了画面**（PR #200）: 連戦の持ち越しは HP と現在のスタミナだけ（デッキは毎戦混ぜ直す）。休みは HP 30% とスタミナ全回復。集計は出来事の並びから数える。進み方と文面は Bridge の `DemoSession`
- **#192 通しの試験**（PR #201）: 11 体 × 種 20 個 × ランダムなデッキで詰まり 0 件。個別の試験（出せる札が無いターン、再シャッフル、届かない手札、スタミナ 0）。勝率の表を PR に載せた
- **見つけたこと**: 灰弩・燐弓・大黒蛇が間合い 3 以上で膠着する（#196、cards）。敵の鈍足 1 は減り方の規則で効かない（#197、正本の食い違い）。デモ側では、見守りなしの打ち手が間合い 2 以上で下がらないようにし、ランダムのデッキに詰める札を必ず入れた
- **検証**: どの PR も `npm run build` / `lint` / `test:run` / `dotnet test` が exit 0。View の C# は Unity 6 の DLL を参照して C# 9 でコンパイルできることを作業用のプロジェクトで確かめた。どの PR も別の目で読み直し、挙がった点を直した
