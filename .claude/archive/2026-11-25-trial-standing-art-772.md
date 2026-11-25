# Plan: 試遊版の人型に立ち絵を差し込む（敵と主人公、#772）

- **Status**: COMPLETED（敵と主人公の待機の絵が試遊版の戦闘に立つことを確かめた）
- **Created**: 2026-11-25
- **Task**: #772 試遊版の Unity 版の人型に立ち絵を差し込む
- **Project**: original-card-battle（worktree `main-772`、ブランチ `feat/main-772`）
- **計画書**: `.claude/docs/vision/plans/2026-11-25-trial-standing-art-772.md`

## Context

- 試遊版（`Assets/View/Trial/`）の人型は、全員がベクターの影絵です（`Trial/Battle/FieldArt.cs`）。`View/Trial/` には `Art/Characters` を読むコードがありません。
- こうだいさんは 2026-11-01 に「立ち絵のある敵は立ち絵を映す」と決めました（#772）。2026-11-25 に、主人公の絵も同じ作業に含めると決めました。主人公の絵は Unity リポの `Assets/Art/Characters/swordsman/chr_swordsman_idle_stand.png`（640×1536、右向き）に置いてあります。
- 主人公の絵は、頭の上に 180 px、靴底（1434 行）の下に約 100 px の余白があります。敵の絵は足が下端まで描かれています。画像の高さを人型の高さに合わせるだけでは、主人公だけが約 2 割小さく浮きます。絵ごとに「頭の頂・靴底・体の中心」を持たせ、靴底を床に合わせます。
- 絵の置き場 `Assets/Art/Characters/<id>/` は `asset-intake.md` §4（design の持ち物）が決めていて、`Resources` の下ではありません。置き場は変えません。
- やらないこと: ポーズごとの絵の差し替え（#1069）、旧い `View/Depiction` の画面の変更、`asset-intake.md` の変更。

## 検討した代替案

| 案 | 採否 | 却下理由 | 復活条件 |
| --- | --- | --- | --- |
| `Resources` に置いた棚（ScriptableObject）が絵を参照し、Editor が絵の追加のたびに棚を作り直す | ✓ | — | — |
| 絵を `Assets/Resources/Characters/` へ移し、`Resources.Load` で読む | ✗ | 置き場は design の `asset-intake.md` §4 が決めていて、Preset のフィルタと旧い棚の集め方も変わる | design が置き場を `Resources` に移すと決めたとき |
| 棚を `TrialBoot` の欄に持たせ、シーンに保存する | ✗ | 絵を足すたびにシーンの保存が要る | — |
| 主人公の余白を絵の側で詰める | ✗ | こうだいさんの決定は「靴底に合わせて吸収する」 | — |

## Scope (Touchable Paths)

- 触ってよい:
  - `unity-port/unity-project-kit/Assets/View/Trial/Battle/FieldView.cs`、`FieldArt.cs`、新規 `FigureArtShelf.cs`
  - `unity-port/unity-project-kit/Assets/View/Trial/Battle/Flow/` の新規 `ArtStand.cs`
  - 新規 `unity-port/unity-project-kit/Assets/View/Trial/Editor/FigureArtShelfBuilder.cs`
  - `unity-port/unity-project-kit/Assets/View/Trial/Kit/Graphics/ArtImage.cs`（明るさのフィルタを受ける必要があるときだけ）
  - `unity-port/Trial.Battle.Flow.Tests/`
  - `unity-port/TRIAL-PORT.md`、`.claude/CLAUDE.md`（メインが書く）
- 触らない: `View/Depiction/`（`FigureArtCollector.Collect()` を呼ぶだけ）、`BattleCore`、`.claude/docs/art_document/`、Unity リポの既存の絵

## Steps

1. [x] `ArtStand`（純 C#、`Trial.Battle.Flow`）: 絵の id ごとの「頭の頂・靴底・体の中心 x」（px）の表と、人型の高さと足の中心 x から絵を置く矩形（人型の左上原点）を出す関数。表に無い絵は、画像の上端から下端、中心は横幅の半分。`swordsman` は 180・1434・274。
2. [x] `FigureArtShelf`（ScriptableObject、`Trial.Battle`）: `id` と `idle` `act` `hit` `down` の Sprite の組の配列。`Resources/Trial/FigureArtShelf` から 1 度だけ読み、Play に入るたびに覚えを消す。
3. [x] `FigureArtShelfBuilder`（Editor）: メニュー `Tools/Trial/Collect Character Art` と、`Assets/Art/Characters/**.png` の取り込み・移動・削除のあとに棚を作り直す AssetPostprocessor。棚は `Depiction.View.FigureArtCollector.Collect()` の結果から作り、無ければ `Assets/View/Resources/Trial/FigureArtShelf.asset` を作る。
4. [x] `FieldArt.PlayerArtId = "swordsman"`。主人公は台本の `ArtId` が空なら、これを使う。
5. [x] `FieldView.Dress` に絵の id を渡す。棚に待機の絵があれば、影絵の代わりに `ArtImage.Lay` で `ArtStand` の矩形に置く。配置の幅と中心（`Width` `Cx`）は影絵の定義のまま使い、床の位置と当たりを変えない。打たれたときの光（flash）は絵の形で光らせる。体の灯り（glowbody）と前の縁の影（FRONT_EDGE）は、絵には付けない。明るさのフィルタ（`Bright`、後ろの敵の持ち上げ）は絵にも効かせる。
6. [x] dotnet のテスト: `ArtStand` の既定と `swordsman` の矩形。
7. [x] `TRIAL-PORT.md` の「Character art」と `.claude/CLAUDE.md` の #772 の行を、敵と主人公・棚の置き場・靴底合わせに直す。
8. [x] メインの Unity で同期し、棚を作り、`Trial.unity` の戦闘で主人公が絵で立つことを撮って確かめる。絵の無い敵は影絵のまま。

## Files

| File | Operation | Notes |
| --- | --- | --- |
| `Assets/View/Trial/Battle/Flow/ArtStand.cs` | create | 絵の置き方の表と計算 |
| `Assets/View/Trial/Battle/FigureArtShelf.cs` | create | 絵の棚（Resources から読む） |
| `Assets/View/Trial/Editor/FigureArtShelfBuilder.cs` | create | 棚を作り直すメニューと取り込み後の処理 |
| `Assets/View/Trial/Battle/FieldArt.cs` | edit | 主人公の絵の id |
| `Assets/View/Trial/Battle/FieldView.cs` | edit | 絵を着せる、光らせる |
| `unity-port/Trial.Battle.Flow.Tests/ArtStandTests.cs` | create | 矩形のテスト |
| `unity-port/TRIAL-PORT.md`、`.claude/CLAUDE.md` | edit | 決まりの書き直し |

## Verification

- [x] `dotnet test unity-port/Trial.Battle.Flow.Tests` が通る（216 件合格）
- [x] `npm run unity:check` が exit 0（Editor の `uloop compile` もエラー 0 件）
- [-] `npm run build`・`npm run lint`・`npm run test:run` が通る（TS を触っていないので回していません）
- [x] `Trial.unity` の戦闘で、主人公が絵で立ち、靴底が床の線に乗る（2026-11-25 にメインの Unity で撮影）
- [x] 打たれたときの光が絵の形で出る（光の層を不透明にして撮影）。絵の無い敵が影絵のままかは、コードの分岐で確かめ、画面では未確認です

## Worklog
