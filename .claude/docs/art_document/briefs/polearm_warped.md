# 長柄の歪み兵（polearm_warped）— 仕様カード

- 作成日: 2026-09-20 / 工程: C0（`visual-production-pipeline character polearm_warped`）
- 正本: `.claude/docs/enemy_document/enemy_roster_v4.md:132-146` / `.claude/docs/battle_document/battle_ui_ux_v2.md:59-60,111,156` / `unity-port/unity-project-kit/Assets/View/ArenaView.cs`
- 手順書: `docs/reports/2026-09-19-krita-ai-setup-guide.html` 4 章

## 1. この敵が何者か

階層 1〜2 の通常敵です（`enemy_roster_v4.md:132,136`）。柄の長さで相手を寄せつけず、踏み込まれたら石突きで突き放す押し引きを最初に教える役です（`enemy_roster_v4.md:134`）。HP 60 / 最大スタミナ 10 / 狙い 6〜7 ターン（`enemy_roster_v4.md:136`）。近間 / 遠間を持つのはボスと精鋭だけなので、この敵は位置を持ちません（`battle_core_v4.md` §17.3）。状態（出血・鈍足など）は使いません。

絵で伝えるべきことは 2 つだけです。**長い柄の武器を持っていること**と、**その柄の長さで相手を遠ざける構えでいること**。位置を持たない通常敵なので、立ち位置では描き分けません。性格は構えと武器の長さだけで見せます。歪み（corruption）は主役ではなく、階層 1〜2 の入口として「元は人だった兵」に見える程度に留めます。

## 2. 画面での大きさと基準点

| 項目           | 値                            | 出典                                            |
| -------------- | ----------------------------- | ----------------------------------------------- |
| 表示枠         | 140 × 300 px（比 約 1:2.14）  | `ArenaView.cs:35-36`                            |
| 基準点         | 足元の中央（Root pivot）      | `ArenaView.cs:12`                               |
| 左右の向き     | 右向きで描く。敵は `scaleX=-1` で反転して画面左（プレイヤー側）を向く | `ArenaView.cs:29`     |
| 頭上の空き     | 予兆バナーが足元から `300×ScaleY + 34 px` に出る | `ArenaView.cs:78,244`        |
| 胸の基準       | 数字は足元から `300 × 0.72 = 216 px` に湧く      | `ArenaView.cs:489`           |
| 原本の大きさ   | 1024 × 1536 の文書。取り込みは長辺 1024          | 手順書 段階 0               |

**利き手**: 反転後に武器が画面左（プレイヤー側）へ来るように描きます。原本では画面右へ穂先が向きます。

**穂先の長さの制約（要判断）**: 300 px を身長 1.7 m とすると 1 px ≈ 5.7 mm です。長柄 2.4 m は 423 px になり、縦に構えると枠 300 px を、横に構えると枠幅 140 px を大きく超えます。推奨は、柄を 70〜75°（ほぼ縦）に立てて穂先を頭の斜め後ろに置き、全高を 300 px 以内に収めることです。横幅は 130 px 以内（C1 の関門）に入ります。穂先を画面に出したい場合は表示枠を広げる実装が要るので、C1 の影絵で形を見てから決めます。

## 3. 行動と、絵が要る場面

`enemy_roster_v4.md:138-146` の 5 行動です。差分は「待機 / 行動 / 被弾 / 崩れ / 倒れ」で、体勢（構え）の差分は作りません（skill 命名節、2026-09-14 決定）。

| 行動 id        | 名前             | 絵で見せること                                     | asset_id                              |
| -------------- | ---------------- | -------------------------------------------------- | ------------------------------------- |
| `sweep`        | 薙ぎ払い         | 柄を寝かせて水平に振り抜く。主力（威力 6〜13）     | `chr.polearm_warped.act.sweep`        |
| `reach_thrust` | 穂先の突き       | 腰だめから前へ伸ばす。削り（威力 2〜6）            | `chr.polearm_warped.act.reach_thrust` |
| `shove`        | 石突きの押し込み | 柄の尻で突き放す。プレイヤーを遠間へ移す           | `chr.polearm_warped.act.shove`        |
| `reposition`   | 間合い取り直し   | 足を送って穂先の届く位置を作り直す。足の動きだけ   | `chr.polearm_warped.act.reposition`   |
| `guard_up`     | 柄で受ける       | 柄を斜めに立てて受ける（Guard 3〜8）               | `chr.polearm_warped.act.guard_up`     |

待機は `chr.polearm_warped.idle.stand`、被弾・崩れ・倒れは `chr.polearm_warped.react.*` です。ファイル名は `Assets/Art/Characters/polearm_warped/chr_polearm_warped_<category>_<label>.png`。

**C3 で作るのは待機 1 枚だけ**です。行動と被弾の差分は C6（人の加筆）以降です。

**絵柄**: 線・塗り・色・光と、瘴気の帯び方（錆）・竜の系譜の段（眷属・竜人）の描き分けは、`art_document/style-guide.md` の第 2 部（§12〜§19）に従います。影絵は同 §17 の関門を通してから C3 へ進みます。

## 4. 生成の指示文（段階 1・案出し用）

スタイルは `card-battle Animagine`、文書は 1024×1536、生成回数 4 を 2〜4 回まわして 8〜16 枚出します。

```
1other, solo, full body, standing, from side, facing right, holding long polearm diagonally, polearm shaft at steep angle, blade tip behind head, {tattered hooded robe|rusted lamellar armor|straw raincoat}, {bandaged face|cracked mask|hollow eyes}, corrupted soldier, dark miasma wisps, japanese dark fantasy, muted colors, simple background, grey background
```

ワイルドカード（`{a|b|c}`）は生成ごとに 1 つが選ばれます。履歴の画像にカーソルを乗せると選ばれた語が出るので、良かった組み合わせをコピーして固定します（手順書 段階 1）。

禁止事項: 既存の作品名・作者名を入れない。流血を描かない。購入素材や他作品の画像を参照に入れない。

## 5. 時間の予算

1 枚の生成が 1024×1024 / 28 ステップで 21 秒でした（`%APPDATA%\krita\ai_diffusion\logs\server.log` 2026-09-20 13:32）。1024×1536 は画素が 1.5 倍なので 30〜35 秒と見ます。16 枚で 8〜10 分です。C6 の加筆時間は 1 体目で実測し、残り 9 体の計画の基準にします。

## 6. 進捗

| 工程 | 状態   | 備考                                                                           |
| ---- | ------ | ------------------------------------------------------------------------------ |
| C0   | 下書き | このファイル。こうだいさんの承認待ち                                          |
| C1   | 未着手 | 影絵 SVG。穂先の長さの判断はここで決める                                      |
| C2   | 一部   | 指示文は §4 にある。台帳の行はまだ（`Docs/art/asset-ledger.csv` が未設置）     |
| C3   | 人待ち | Krita で 8〜16 枚。指示文は §4 をそのまま貼る                                 |

**人に渡したもの**: §4 の指示文。**次に人がすること**: Krita で候補を出し、2〜3 枚を Claude に渡す。

**前提の欠け（setup S0/S1 が未了）**: `.claude/docs/art_document/style-guide.md` と `Docs/art/asset-ledger.csv` がありません。主人公の見た目が未決なのでスタイルガイドは作れません。台帳は `templates/asset-ledger.csv` から置けます。C3 の候補が出るまでは止まりません。
