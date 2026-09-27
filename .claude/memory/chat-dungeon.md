# MEMORY (chat-dungeon)

> 進行中 / 直近の完了 / 予定の正本。**手動編集せず task-tracker スキル経由で更新**。完了タスクの詳細は `history/chat-dungeon.md`、要約は `README.md` の Development History。
> レーン `dungeon` = 探索プログラム（刻限 / 瘴気 / ノード）と探索側の設計書。既定の `area:` は `dungeon`。

## 進行中

（なし）

## 直近の完了

- 探索の画面を Unity に作る（#101）✅（2026-09-27）— `dungeon_exploration_v4.md` に §6「探索の画面」を足し、純 C# の組み立て役（`Assets/View/Exploration/Script/`）と仮の見た目の UGUI 画面、戦闘と共有する手記のドロワー（`Assets/View/Journal/`）を作った。コアに「この生を終える」（`RunPhase.Survived`）を足した。戦闘は #99 まで仮の札。手記のタブの食い違いは #234 に回し、`battle_ui_ux_v2.md` の 3 つに従った。PR #238（open、merge は人の手番）
- 層ごとの戦闘の場のマス数と開始の間合い 3（#168）✅（2026-09-23）— `seven_layers_v4.md` §2.4 に、通常戦闘 6 / 6 / 6 / 7 / 7 / 7 / 8、精鋭は層の値 + 1、階層ボス 8 の表を置いた。深い層ほど広くする向きと、5 マスを最小値に残すことはこうだいさんが決めた。遠間の履を間合いの履（`maai_no_kutsu`、開始のマス 2 → 1）へ読み替え、`DungeonContent` の C# も合わせた。PR #173（merged）
- 探索要素の選別（#95）✅（2026-09-21）— `danjeon_document/dungeon_exploration_v4.md` を新設し、80% 版に残す要素を選んだ（残す 13 / 後回し 10 / 捨てる 6）。刻限 10 回の内訳を 踏破 5 + 余白 5 に配分し、最小の 1 階層をノード 6（入口戦闘 → 休息 / 情報収集 → 通常戦闘 / 精鋭 → 階層ボス）で定義した。旧 2 文書は旧ループの記録として残し、冒頭から v4 を指すようにした。`src/` と `unity-port/` には触っていない。PR #118（merged）

## 予定

- 🔜 **#99 探索と戦闘を繋ぐ** — `BattleInit` へ HP / スタミナ / 最大スタミナと、層と戦闘の種類で決まる場のマス数（`seven_layers_v4.md` §2.4）を渡し、結果を持ち帰る。マス数は `SevenLayers.cs` にまだ無い
- 🔜 **§12-8 ボスの適応を battle レーンへ回す** — `concept_v3_open_items.md` §3-B の案（観測カウンタ 3 つ、閾値 3 で構えが 1 段変わる）が採用されたら、`battle_core_v4.md` 宛の Issue を切る
