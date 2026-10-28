# MEMORY (chat-battle)

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `history/chat-battle.md`、要約は `README.md` の Development History。
> レーン `battle` = 戦闘プログラム（`unity-port/BattleCore/` が正）と戦闘設計書。既定の `area:` は `battle` `unity`。

## 進行中

（なし）

## 直近の完了

- Unity 移植の戦闘まわりの残り 4 件を 4 本の積んだ PR へ ✅（2026-10-28）— #588 → PR #675、#587 → PR #676、#577 → PR #677、#575 → PR #678（`Refs`。状態の説明の数字は BattleCore の定数待ちで申し送り）。4 本とも open で、dotnet test / build / lint / test:run は exit 0。`unity:check` は Unity のプロジェクトにだけある Trial の 7 アセンブリで origin/main から exit 1。#589 の 2 本目と 3 本目は台本の型が 2 通りあるため未着手（申し送り）
- battle 宛ての Issue 4 件を 4 本の PR へ ✅（2026-10-21）— #646 → PR #658、#615 → PR #664、#643 → PR #665、#589 の 1 本目（台本の型・Bridge・数値パネルの並べ方）→ PR #667（Issue は開いたまま）。4 本とも open で、build / lint / test:run / dotnet test は exit 0
- battle 宛ての Issue 20 件を 20 本の PR へ ✅（2026-10-10）— #364 / #403 / #334 / #59 / #58 / #304 / #311 / #357 / #389 / #30 / #323 / #356 / #361 / #405 / #213 は同じ設計書を触るので 1 本の鎖に積み（PR #420〜#434）、#235 / #302 / #335 / #394 は main から（PR #416〜#419）。19 本は 2026-10-10 に merge 済み。#242（戦闘画面の作り直し）は PR #435 が open。申し送り 14 件を `.claude/comm/handoff/` に置いた

## 予定

- 🔜 **#589 の残り 2 本（main の判断待ち）** — 画面の部品（敵ごとの人型・予兆の札・数値パネル・スタンス枠、`Cue.Unit` の読み分け、2 体目以降の倒れの拍）と、斜線の向きと色見本。#667 の `EnemyFrame` と trial の作業場所（`feat/main-trial-port-20261022`、未 push）の `Enemies`（`UnitFrame`）・`Omens`・`FloorEnemy` のどちらに寄せるかが決まってから。申し送り `2026-10-28-battle-589-two-multi-enemy-models.md`
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
