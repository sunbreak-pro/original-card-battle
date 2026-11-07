# MEMORY (chat-battle)

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `history/chat-battle.md`、要約は `README.md` の Development History。
> レーン `battle` = 戦闘プログラム（`unity-port/BattleCore/` が正）と戦闘設計書。既定の `area:` は `battle` `unity`。

## 進行中

（なし）

## 直近の完了

- battle 宛ての Issue 4 件を 4 本の PR へ ✅（2026-11-07）— #899 → PR #938、#914 → #939（#938 に積む）、#909 → #940、#910 → #941（#939 に積む）。4 本とも open。build / lint / test:run / unity:check / sources --check は exit 0。dotnet test は main と同じ 1 件（`TheProfiles_MatchTheCanonTable`、#898 待ち）だけ赤。申し送り 6 件
- battle 宛ての Issue 7 件を 7 本の PR へ ✅（2026-11-02）— #765 → PR #805、#57 → #814、#762 → #815、#764 → #816、#612 → #817、#766 → #818、#780 → #819。7 本とも open で、build / lint / test:run / unity:check / dotnet test は exit 0。申し送り 8 件
- battle 宛ての Issue 4 件を 4 本の PR へ ✅（2026-11-02）— #580 → PR #782、#724 → #783、#725 → #784、#755 → #785。4 本とも open で、build / lint / test:run / unity:check / dotnet test は exit 0。申し送り 1 件

## 予定

- 🔜 **PR #938 → #939 → #941 を積んだ順に merge、#940 は単独** — #939 と #941 は前の PR の上に積んであり、前が merge されたら差分は自分の分だけになる。#941（#910）は、メインの Unity で勝ちの結果の札の欄（札の顔の縮み・2 行の折り返し・紙の伸び・「次へ」）を見てから merge する。新しいファイル `Trial/Bridge/RaisedCards.cs` の `.meta` は Unity のリポで保存する。dotnet test の赤 1 件は #898（#934 待ち）で直る
- 🔜 **PR #805・#814〜#819 の merge 待ち** — 7 本とも origin/main から切ってある。衝突するのは #762（#815）と #766（#818）の `FloorView.cs` だけで、後に merge する側で main を取り込んで手で解く。merge の後にメインのチャットが `unity:sync` と Unity Editor で、#57 の 8 系統の撮影、#762 の Tab とホバー、#764 の語の高さ、#612 の書体の収まり、#766 の斜線の引き方、#780 の `CardGroundTests`（EditMode）を確かめる。新しいファイル（`FloorFocus.cs` `StripeSweep.cs` `BattleFonts.cs` ほか）の `.meta` は Unity のリポで保存する
- 🔜 **PR #782〜#785 の merge 待ち** — 4 本とも origin/main から切ってあり、触るファイルは重ならない。#785（#755）は、メインの Unity で EditMode の試験が通るのを確かめてから merge する。#782（#580）は、`unity:sync` で写る新しい 6 ファイルの `.meta` を Unity のリポで保存する。残りは申し送り `2026-11-02-battle-stance-gap-at-least-words.md`
- 🔜 **PR #759（#585）の merge 後の確認**（2026-11-02 に merge 済み） — merge の前後に、メインの Unity で PlayMode テスト 3 本と `Battle.unity` の 1 戦を通し、`unity:sync` で写る 6 ファイルの `.meta` を Unity のリポで保存する
- 🔜 **PR #733 / #734 / #738 / #749 / #750 の merge 後の確認** — 5 本とも merge 済み（2026-11-02 に `gh pr view` で確認）。メインのチャットが `npm run unity:sync` と Unity Editor で 2 体の画面を確かめる（部品の複製、灰のパネル、札の滑り、交差した斜線、16 px の色見本）。残りは申し送り `2026-11-01-battle-589-tab-and-omen-hover.md`、`2026-11-01-battle-floor-aim-disclosure-0-one-enemy.md`、`2026-11-01-battle-stripe-draw-in-out.md`、`2026-11-01-battle-float-labels-reach-omen.md`
- 🔜 **PR #682〜#696 の merge 待ち（12 本）** — どれも origin/main から切ってある。`battle_ui_ux_v2.md` の Status に #685 / #692 / #693 が 1 項目ずつ足すので、後の側で両方を残す。`DepictionPlayer.cs` は #676〜#678 と #688 / #695 が触る。merge の後にメインのチャットが `unity:sync`、新しいファイルの `.meta` の保存（#584 の 9 ファイル、#654 / #455 の試験）、スタンス枠のホバー（#605）、階層 4 の背景（#606）、浮かぶ数字（#455）、8 系統の撮影（#57）を確かめる
- 🔜 **PR #675〜#678 の merge 待ち** — 積んだ順（#675 → #676 → #677 → #678）に merge する。merge の後にメインのチャットが `unity:sync` と、`BattleDepiction.unity` の固定台本が床のマスに立つこと（#577 の Gate）、退き撃ちが戻らず下がること（#587）、特性のピップが一撃の後に灯ること（#588）を確かめる。#575 の残り（状態の説明の数字を BattleCore の定数から）は申し送り `2026-10-28-battle-575-status-numbers-core-constants.md`
- 🔜 **PR #435（#242）の merge 待ち** — merge の後にメインのチャットが `npm run unity:sync` と `Battle.unity` の Play で見た目を確かめる。§4〜§7 の残りは申し送り `2026-10-10-battle-visual-rest-242.md` と `2026-10-10-battle-status-panel-follow-242.md`
PR #220 / #223 / #224 / #225 / #226 は 2026-09-27 時点で merge 済み。PR #236（#209）が merge 待ち。merge の後にメインのチャットが `npm run unity:sync` と `Battle.unity` の Play（降参、敵の選び直し、壊れた保存デッキの 1 行）と PlayMode テストを回し、Console の CS8632 が 0 件かを見て、Unity リポに `Assets/Core/csc.rsp` と `Assets/Tests/csc.rsp` とその `.meta` をコミットする。

- 🔜 **#221 / #222 の結果待ち（cards レーン）** — 疲労を一律 −1 のままにするか、燐刃の手記を直すか
- 🔜 **#196 / #197 の結果待ち（cards レーン）** — 敵の一覧が直ったら、`Enemies.cs` の値と、デモ側の回避（見守りなしの打ち手は間合い 2 以上で下がらない、ランダムのデッキは詰める札を必ず含む、敵の鈍足は 2）を外せるか見直す
- 🔜 **デモで入れた読み方を全量版へ（#48 / #49）** — 集中の近似、疲労は回復 −1 固定（#221）、被弾の範囲など。威圧・見切りの減り方は #207（PR #225）で §5 に書いた。PR #195 本文の「デモで決めた読み方」が一覧
- 🔜 **小さな残り（Issue 未作成）** — `Combat.ComputeRawPower` の倍率の引数が 2 つのまま（S13 では 1 つで足りる）。デモの終了画面を出す途中の例外で押せるものが残らない（`OnBattleFinished`）。デモの終了画面は `battle_ui_ux_v2.md` §2.5（全勝で結果画面なし、操作は「手記へ」1 つ）と違う作り。降参ボタンは右上の手記の角を借りている。`unity-port/.gitignore:90` の `*.rsp` が追跡済みの `csc.rsp` にも当たり、ripgrep の検索に出ない（`!unity-project-kit/Assets/Core/csc.rsp` を足せば直る）
- 🔜 **#163 画面の設計書を間合い N へ** — 床のマス・届くマスの光・狙うマス。一字札と予兆の一字の整理
- 🔜 **#74 の Unity 側の手作業（メイン）** — 先に #134（v3 の View 雛形 7 ファイルと `BattleTheme.cs` の 2 関数）と、Unity リポに残る v3 のコア 8 ファイルの削除。その後 `Battle.unity` を作り、PlayMode テストを回す。手順は PR #153 の本文
- 🔜 **#75 / #76 / #77 演出 3 本 → #78 計測** — 牽制を Guard 持ちの敵へ出すと公称 1990 ms で、1 行動 2.0 秒を実測で超える見込み（#78 にコメント済み）。状態の拍（`CueKind.StatusChange`）は型だけあり、再生は #77
- 🔜 **#80 振り返りへ回すもの** — 長柄は開始スタミナ 10 だと休みが起きず、構えの Guard 3 が毎ターン付く。単属性ムーブ 6 種は特性が語彙の外で縦切りに入れられなかった（R14 / R15 未検証）。Guard に上限が無く 1 ターンで 33 まで積める
- 🔜 **§19.9 の本文改訂の残り** — §2.3（手薄）は #116、§5.1 / §17.6 F4 / §8 / §10 / §11 は #206（PR #223）で反映。残りは習熟の S5〜S8（§17.8 の型替えの閾値など）と、cards レーンの 2 文書。`SOURCES.md` の表の下の一文はメインのチャットに直してもらう
- 🔜 **試験台（#53）** — §19 の基準 10〜14 に加え、強弱 4 段の閾値 9 / 16 / 24 と、敵に同時に付く状態の種類を測る
