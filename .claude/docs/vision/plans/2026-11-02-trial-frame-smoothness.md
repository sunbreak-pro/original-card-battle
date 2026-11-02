# Plan: 試遊版の Unity 版を Editor で滑らかに動かす

- **Status**: Draft
- **Created**: 2026-11-02
- **Task**: Unity Editor の実際の画面で動作が少し重い。原因を探り、滑らかに動かすための実装計画を作る（こうだいさん依頼）
- **Project**: `C:\Users\user\orca\original-card-battle`（対象は `unity-port/unity-project-kit/Assets/View/Trial/`、Unity リポは `C:\Users\user\Unity\RPG-by-card`）
- **計画書**: `.claude/docs/vision/plans/2026-11-02-trial-frame-smoothness.md`
- **Issue**: Step 0 = #789、Step 1 = #790、Step 2 = #791、Step 3 = #792、Step 4 = #793、Step 5 = #794、Step 6 = #795、Step 7 = #796
- **関連**: 戦闘の動きを 0.2〜0.4 s 遅くした PR #787（同じ依頼の後半。重さとは別の話）

> 以下のパスは `View/Trial/` からの相対で書く（`T/` と略す）。原因はコードを読んで見つけたもので、Profiler ではまだ測っていない。重さの順位は推定であり、Step 0 の計測で並べ直す。

---

## Context

### 何が起きているか

Trial.unity を Editor で遊ぶと、動きが少し重く、ときどき引っかかる。コードを読むと、原因は次の 3 種類に分かれる。

1. **止まっている絵を毎フレーム作り直している（定常の重さ）。** 探索の道の流れる点線、行ける部屋の光る影、霧の濃い層の影絵、手札の位置の書き込みが、見た目が変わらないフレームでも形（メッシュ）を作り直したり、RectTransform を書き換えたりしている。uGUI は 1 つでも書き換わると、その Canvas の描画のまとめ直し（rebatch）が走る。
2. **毎フレームの小さなゴミ（GC の引っかかり）。** 時計（`TrialClock.Step`）がフレームごとに List を 10 個ほど作り、動いている補間 1 つごとにも List を作る。キー入力の見張り（`KeyStack.Pump`）や `VectorGraphic.WatchScales` も毎フレーム List を作る。ゴミが溜まると GC が走り、そのフレームだけ止まる。
3. **初めて出る文字・音の準備（単発の引っかかり）。** フォントは動的アトラス（使う文字をその場で焼く方式）で、前もって焼いているのは戦闘画面だけ。タイトル・継承の間・出立・探索・手記で新しい漢字が出るたびに、1 字ずつ焼いて 2048² のテクスチャを送り直す。音も、起動直後に 100 個以上の効果音を裏で作っており、その配列（1 個 0.3〜1 MB）が大量のゴミになる。

例えると、台所で「皿を 1 枚洗うたびに食器棚を全部並べ直している」状態です（1 番）。加えて「使い捨ての紙皿が毎食溜まり、ときどきゴミ出しで手が止まる」（2 番）と、「初めて作る料理だけ、その場でレシピを調べている」（3 番）も起きています。

### 見つかった原因（推定の重い順）

| # | 種類 | どこ | 何が起きるか |
| --- | --- | --- | --- |
| 1 | 定常 | `T/Explore/ExploreEdgeView.cs:39`（`stroke-dashoffset` の無限アニメ）→ `T/Kit/Graphics/PathGraphic.cs:103-112` | 光っている道 1 本ごとに、毎フレーム線を平らにし直し、点線を組み直す（`Flatten.Run` と `Stroker.Stroke`）。影の層も同じ |
| 2 | 定常 | `T/Explore/ExploreNodeView.cs:48,75`（`ocbg-explore-beckon` の box-shadow 無限アニメ）→ `T/Kit/Motion/CssAnimator.cs:411-415` → `T/Kit/Graphics/BoxGraphic.cs:70-74` | 行ける部屋 1 つごとに、毎フレームぼかした影の箱を作り直す（`BoxMesh.Build` と `OuterShadow`） |
| 3 | 定常（条件つき） | `T/Explore/ExploreBackdrop.cs:324-360` | 瘴気 80 % 以上で、影絵の強さが時間で揺れるため、比べる鍵の文字列が毎フレーム変わる。240×135 の絵を毎フレーム 1 画素ずつ計算し直し、`SetPixels32` と `Apply` をする |
| 4 | 定常 | `T/Kit/Motion/Transitions.cs:447-458` / `:472-477`、`T/Kit/Text/TextGlow.cs:139-214` | 色だけが変わる hover の移り変わりでも、`BoxStyle` を複製して箱の形を作り直す。文字の光（text-shadow）を動かすと、1 字ごとに最大 18×18 頂点の四角を作り直す |
| 5 | 定常 | `T/Battle/Hand.cs:1328-1378`（毎フレームの `Render`）→ `T/Kit/UiNode.cs:222-235`（`Apply`） | 手札が止まっていても全札の pivot・sizeDelta・位置・拡大・回転を毎フレーム書き込む。値が同じかを見ていない |
| 6 | GC | `T/Async/TrialClock.cs:228-329`、`T/Kit/Motion/ClockDriver.cs:77`、`T/Kit/Graphics/VectorGraphic.cs:268`、`T/Kit/Input/KeyStack.cs:275-277` | 毎フレーム List や配列を作って捨てる。補間 1 つごとに `new List<string>(tw.To.Keys)`。`_frameFns.Contains` / `Remove` は数に比例して遅い |
| 7 | 定常 | `T/Kit/Text/Label.cs:144-154`（`SetText`）、`:186`、`:214-239` | 同じ文字を入れ直しても早く抜けない。1 回の `SetText` で TMP の組み直しが 3 回以上走る。`Wrap.Balance` はさらに最大 16 回 |
| 8 | 定常 | `T/Kit/Masks/FadeMask.cs:227-270`、`T/Kit/Masks/ScrollArea.cs:239` | スクロール中は毎フレーム、リストの下の全部品（画面外の行も）を作り直し、全 TMP を組み直す |
| 9 | 単発 | `T/Kit/Text/TrialFonts.cs:116-132`、`T/Battle/BattleScene.cs:414-415` | 戦闘以外の画面は文字を前もって焼いていない。新しい漢字 1 字ごとに 1〜3 ms ほど（推定）かかり、50〜100 字の画面なら 100〜300 ms 止まる |
| 10 | 単発 / GC | `T/Audio/Synth/PresetRenderer.cs:118-160`、`T/Audio/SfxClipCache.cs:200-221,278,413` | 効果音 1 個を作るのに大きな配列を 7 本ほど作って捨てる。起動直後は 100 個以上なので、数百 MB のゴミになる。裏の作業が間に合わない音を鳴らすと、その場で作るので止まる |
| 11 | 単発 | `T/Audio/SfxClipCache.cs:262-272`、`T/Meta/SettingsOverlay.cs` | 残響のつまみを動かすたびに、全効果音を作り直す |
| 12 | 単発 | `T/Audio/Synth/AmbienceEngine.cs:111-132`、`T/Audio/AmbienceHost.cs:60-66` | 音の出力先を変えると、環境音の部品をメインスレッドで作り直す（推定 30〜150 ms） |
| 13 | GPU | `T/Kit/Render/GammaTarget.cs:163-234` | 画面と同じ大きさの描画先を 2 枚持ち、毎フレーム全面をコピーする。Game ビューを大きくするほど重い。大きさを変えると作り直す |
| 14 | 見え方 | `T/Async/TrialClock.cs:74,227`（`MaxStepMs = 50`） | 戦闘の時計は 1 フレームで 50 ms しか進まない。20 fps を切るフレームでは戦闘がスローになり、重さが「もたつき」として見える |
| 15 | GC | `T/Kit/Ui.cs:13,103`、`T/Battle/Fx.cs:271,279` | 演出の部品を毎回 `new GameObject` して `Destroy` する。使い回し（プール）が無い |

### うまくできていて、触らないもの

- 透明度は `CanvasRenderer.SetAlpha` と `CanvasGroup` で変えていて、形は作り直していない。色味（tint）とフィルタも、作った頂点の色だけを塗り直している。
- 実行中にテクスチャを作らない作り（グラデーション・ハッチ・光は全部頂点）。CSS の解釈結果と `PathData` には貯めておく仕組み（キャッシュ）がある。
- 画面の層ごとに入れ子の Canvas があり、まとめ直しの範囲は層の中に収まっている。
- `FieldView` / `Status.PlaceAt` / `Omen.Place` / `HandMarks` は値が同じなら書かない。`Hud.Follow` はリストを使い回している。
- 時間はすべて `Time.unscaledDeltaTime` で、補間は積み上げた時刻から計算している。位置は 0.1 px 単位で、整数 px に丸めていない。

### 守ること

- **見た目は JS の試遊版が正**（`unity-port/TRIAL-PORT.md`）。直したあとも、動きの ms・ease・色・形は変えない。変えてよいのは「同じ絵をどう作るか」だけ。
- 規則（C# のコア）は触らない。
- 旧い画面（`View/Depiction` `View/Exploration`、`Battle.unity`）は対象にしない。

---

## 検討した代替案

| 案 | 採否 | 却下理由 | 復活条件 |
| --- | --- | --- | --- |
| A. 先に測り、重い順に「同じ絵を作り直さない」方向で直す | ✓ | — | — |
| B. Editor の設定だけで済ませる（Gizmos を切る、Game ビューを小さくする、VSync を入れる） | ✗（Step 1 の一部としてだけ使う） | 作り直しとゴミはビルドした版でも起きる。Editor の外でも重さが残る | 計測で、Editor の上乗せ分だけが重さの原因と分かったとき |
| C. 動きの更新を 30 Hz などに間引く | ✗ | 動きそのものがカクついて見え、滑らかにしたい目的に反する | 背景の塵のような、目で追わない飾りに限って、計測で重いと分かったとき |
| D. 描画を shader・パーティクル・Sprite に作り替える | ✗ | 試遊版の CSS をそのまま写す決まりから外れ、見た目の突き合わせができなくなる | 案 A をやり切っても、特定の演出が 1 フレームの予算を超えるとき |
| E. 戦闘の時計の 50 ms の上限を外す | 保留（Step 7） | JS の stage.js と同じ動きをする決まりがある。重さを直せば上限に当たるフレーム自体が減る | 案 A の後も 20 fps を切るフレームが残るとき |

---

## Scope (Touchable Paths)

- 触ってよい:
  - `unity-port/unity-project-kit/Assets/View/Trial/Kit/`（Graphics / Motion / Text / Masks / Input / Render / `UiNode.cs` / `Ui.cs`）
  - `unity-port/unity-project-kit/Assets/View/Trial/Async/`（`TrialClock.cs`）
  - `unity-port/unity-project-kit/Assets/View/Trial/Explore/`（`ExploreEdgeView.cs` `ExploreNodeView.cs` `ExploreBackdrop.cs`）
  - `unity-port/unity-project-kit/Assets/View/Trial/Battle/`（`Hand.cs` `Fx.cs` `BattleScene.cs`、`Flow/BattlePlayer.cs` の毎フレーム処理だけ）
  - `unity-port/unity-project-kit/Assets/View/Trial/Audio/`、`Audio/Synth/`
  - `unity-port/unity-project-kit/Assets/View/Trial/Boot/TrialBoot.cs`、`Meta/` の各画面の入口（文字の焼き込みを足すところだけ）
  - 新規: `T/Kit/Dev/FrameStats.cs`（計測の部品）
  - テスト: `unity-port/Trial.*.Tests/`
- 触らない:
  - `unity-port/BattleCore/` ほか規則のコア、`View/Depiction` `View/Exploration`
  - 動きの長さ・ease・色・形の数値（JS が正。`BattlePacing.cs` も触らない）
  - Unity リポのシーン・プレハブ・ProjectSettings（変えたいときは、こうだいさんに聞いてから）

---

## Steps

0. [ ] **Step 0: 今の重さを測る（土台）。**
   - `T/Kit/Dev/FrameStats.cs` を足す。`ProfilerRecorder` で、フレームの時間（Main Thread）・GC Allocated In Frame・Batches・SetPass を 1 フレームごとに貯め、画面ごとに平均 / p95 / p99 / 33 ms を超えた回数 / 1 フレームのゴミの平均を CSV に書く。Editor だけで動かす（`#if UNITY_EDITOR`）。
   - uloop で、タイトル → 継承の間 → 出立 → 探索（瘴気の薄い層と 80 % 以上の層）→ 戦闘 3 ターン → 手記のスクロール、の順に通して測る。Game ビューは 1920×1080 に固定する。
   - Profiler を 1 回だけ Deep Profile なしで録り、`TMP_FontAsset.TryAddCharacters` `AudioClip.Create` `Canvas.BuildBatch` `GC.Collect` の印が、引っかかったフレームと重なるかを見る。
   - 結果で上の表を並べ直し、計画書の「見つかった原因」に実測の列を足す。
1. [ ] **Step 1: すぐ効いて危なくないもの。**
   - `T/Boot/TrialBoot.cs:109-110`: Editor では VSync が Game ビューの切り替え次第で効かないので、`Application.isEditor` のときは `targetFrameRate` も 60 にする（フレームの間隔が揃い、CPU を使い切らない）。
   - `T/Kit/Text/Label.cs:144`: `SetText` は、文字も組みの条件も同じなら何もせず戻る。
   - `T/Kit/UiNode.cs:222-254`: `Apply` と `Opacity` は、前に書いた値と同じ項目を書かない。
   - `T/Explore/ExploreBackdrop.cs:327`: 影絵の鍵を、文字列ではなく数値の組にする。強さと内側の縁は 1/64 刻みに丸めて比べる（見た目の差は 1 % 未満）。
2. [ ] **Step 2: 毎フレームのゴミを無くす。**
   - `T/Async/TrialClock.cs` `Step`: List を毎回作らず、持ち続けた 2 本を入れ替えて使う。補間のキーは作るときに配列にしておき、毎フレームのコピーをやめる。`_frameFns` は消す印を付けてまとめて詰める方式にする。
   - `T/Kit/Motion/ClockDriver.cs:77`、`T/Kit/Graphics/VectorGraphic.cs:268`、`T/Kit/Input/KeyStack.cs:275-277,394`、`T/Battle/Flow/BattlePlayer.cs` の `FollowFigures`、`T/Battle/Hud.cs:790`、`T/Explore/ExploreScene.cs:189`: 使い回しのリストにする。`KeyStack` はキーが押されているときだけ全キーを見る。
   - `T/Kit/Text/TextGlow.cs:104-137`: `GetComponent<CanvasGroup>()` を最初の 1 回だけにする。
   - 確かめ方: Step 0 の CSV で、止まっている画面の「1 フレームのゴミ」が 0 B に近いこと。
3. [ ] **Step 3: 探索の無限アニメで形を作り直さない（重さの本命）。**
   - `T/Kit/Graphics/PathGraphic.cs:103-112,284-321`: 線を平らにした結果を層ごとに貯め、`DashOffset` が変わったときは点線の切り分けだけをやり直す。
   - `T/Kit/Motion/CssAnimator.cs:411-415` → `T/Kit/Graphics/BoxGraphic.cs:70`: `SetShadows` は前と同じ影なら何もしない。`ocbg-explore-beckon` の影は、一番大きい形で 1 回だけ作った子の影に分け、動かすのは透明度と拡大だけにする。動きの曲線（2.4 s ease-in-out）は変えない。
   - 確かめ方: 探索で行ける部屋が 3 つ・光る道が 3 本のとき、フレームの時間が Step 0 より下がること。JS の参考画面（`$REF/shots/`）と並べて、光の広がり方が同じに見えること。
4. [ ] **Step 4: 色だけ変わる移り変わりと、文字の光。**
   - `T/Kit/Motion/Transitions.cs:447-458`: 背景色・枠の色だけが変わるときは、形を作り直さず頂点の色だけを塗り直す道を通す。
   - `T/Kit/Text/TextGlow.cs:139-214`、`CssAnimator.cs:416-424`: 光は一番強いときの形で 1 回だけ作り、動きは透明度で付ける。
5. [ ] **Step 5: 手札とスクロールの書き込みを減らす。**
   - `T/Battle/Hand.cs:511-543,1328-1378`: 札ごとに「前のフレームから動いたか」の印を持ち、動いていない札は `Paint` しない。
   - `T/Kit/Masks/FadeMask.cs:227-270`、`ScrollArea.cs:239`: 縁のぼかしが変わったときだけ作り直す。スクロール中は、縁にかかる行だけを作り直す。
   - `T/Kit/Text/Label.cs:186,214-239`: 1 回の `SetText` で TMP を組むのを 1 回にまとめる。`Ocb.Card.MeasureBox` の測った結果を (文字, 大きさ, 幅) で貯める。
6. [ ] **Step 6: 初めての文字と音の引っかかりを無くす。**
   - 文字: 各画面の入口（タイトル・継承の間・出立・探索・手記）で、その画面が出す文字を暗転の間に焼く。戦闘の `BattleScene.cs:414-415` と同じ `TrialFonts.Prewarm` を使う。1 フレームに焼く字数に上限を付け、何フレームかに分ける。
   - 音: `PresetRenderer` の作業用の配列をスレッドごとに使い回す。`SfxClipCache.Pump` は 1 フレームに 1〜2 個までにする。起動時に作る音は、最初の画面で使うものを先にし、残りは最初の画面が出てから作る。
   - 残響のつまみ（`SetReverb`）は、つまみを離してから 300 ms 後に 1 回だけ作り直す。
   - 環境音の部品（`AmbienceEngine`）は、出力先が変わったときは裏で作ってから差し替える。
7. [ ] **Step 7: 測り直して、残りを決める。**
   - Step 0 と同じ道順で測り、前後の表を計画書に足す。
   - まだ 20 fps を切るフレームがあれば、案 E（戦闘の時計の 50 ms 上限）と `GammaTarget` の描画先を 1920×1080 に固定する案を、こうだいさんに数字を添えて聞く。演出の部品の使い回し（表の 15）もここで要否を決める。

各 Step は 1 本の PR にする。Step 1 と Step 2 は小さいので並べて出せる。Step 3〜6 は Step 0 の結果で順番を入れ替えてよい。

---

## Files

| File | Operation | Notes |
| --- | --- | --- |
| `T/Kit/Dev/FrameStats.cs` | create (M) | Step 0。`ProfilerRecorder` でフレームの時間とゴミを CSV に書く。Editor だけ |
| `T/Boot/TrialBoot.cs` | edit (S) | Step 1。Editor ではフレームの上限を 60 にする |
| `T/Kit/Text/Label.cs` | edit (M) | Step 1 で同じ文字の早抜け、Step 5 で TMP の組みを 1 回に |
| `T/Kit/UiNode.cs` | edit (S) | Step 1。同じ値は書かない |
| `T/Explore/ExploreBackdrop.cs` | edit (S) | Step 1。影絵の鍵を数値にして丸める |
| `T/Async/TrialClock.cs` | edit (M) | Step 2。List の使い回し、補間のキーの配列化 |
| `T/Kit/Motion/ClockDriver.cs` | edit (S) | Step 2 |
| `T/Kit/Graphics/VectorGraphic.cs` | edit (S) | Step 2。`WatchScales` のリスト |
| `T/Kit/Input/KeyStack.cs` | edit (S) | Step 2 |
| `T/Battle/Flow/BattlePlayer.cs` | edit (S) | Step 2。`FollowFigures` の毎フレームのリストだけ |
| `T/Battle/Hud.cs` | edit (S) | Step 2。`:790` のリスト |
| `T/Explore/ExploreScene.cs` | edit (S) | Step 2。`:189` の `ToArray` |
| `T/Kit/Text/TextGlow.cs` | edit (M) | Step 2 で `GetComponent` の 1 回化、Step 4 で光を透明度で動かす |
| `T/Kit/Graphics/PathGraphic.cs` | edit (M) | Step 3。平らにした線を貯める |
| `T/Kit/Graphics/BoxGraphic.cs` | edit (S) | Step 3。同じ影なら何もしない |
| `T/Kit/Motion/CssAnimator.cs` | edit (M) | Step 3・4。影と光の動かし方 |
| `T/Explore/ExploreNodeView.cs` | edit (S) | Step 3。光る影を子に分ける |
| `T/Explore/ExploreEdgeView.cs` | edit (S) | Step 3（必要なら） |
| `T/Kit/Motion/Transitions.cs` | edit (M) | Step 4。色だけの移り変わり |
| `T/Battle/Hand.cs` | edit (M) | Step 5。動いていない札は書かない |
| `T/Kit/Masks/FadeMask.cs` / `T/Kit/Masks/ScrollArea.cs` | edit (M) | Step 5 |
| `T/Kit/Ds/Ocb.Card.cs` / `T/Kit/Text/TextMeasure.cs` | edit (S) | Step 5。測った結果を貯める |
| `T/Kit/Text/TrialFonts.cs` と各画面の入口（`T/Meta/*Scene.cs` `T/Explore/ExploreScene.cs` `T/Meta/JournalDrawer.cs`） | edit (M) | Step 6。文字の焼き込み |
| `T/Audio/Synth/PresetRenderer.cs` / `T/Audio/SfxClipCache.cs` / `T/Audio/TrialAudio.cs` / `T/Audio/AmbienceHost.cs` | edit (M) | Step 6 |
| `unity-port/Trial.Async.Tests/` `unity-port/Trial.Geometry.Tests/` ほか | edit | 各 Step の動きが変わらないことのテスト |
| `.claude/docs/vision/plans/2026-11-02-trial-frame-smoothness.md` | edit | Step 0・7 で実測の表を足す |

---

## Verification

- [ ] 各 Step で `dotnet test unity-port/UnityCorePort.slnx` が失敗 0、`npm run unity:check` が 0 errors。
- [ ] Step 0 と Step 7 の CSV を並べ、全画面で p95 のフレームの時間が 16.7 ms 以下になる（Editor、1920×1080、Deep Profile なし）。
- [ ] 止まっている画面（タイトル・探索で待っているとき・戦闘の手番待ち）で、1 フレームのゴミの平均が 1 KB 以下になる。
- [ ] 初めて入る画面で 33 ms を超えるフレームが 1 回以下になる（暗転の間を除く）。
- [ ] 見た目が変わっていない: 各 Step の前後で、同じ乱数の種・同じ時刻の画面を撮り、`$REF/shots/` の参考画面と並べて、`visual-inspect` で切り抜いて見比べる。動きの ms は既存のテスト（`BattlePlayerFlowTests` の長さの照合など）が守る。
- [ ] 外れ値の確認: 瘴気 80 % 以上の層、敵 3 体の戦闘、手札 6 枚を全部ホバーで動かすとき、手記を一番下まで速くスクロールするとき、でも p95 が 16.7 ms 以下。
- [ ] Editor で残響のつまみを動かし続けても、33 ms を超えるフレームが出ない。
