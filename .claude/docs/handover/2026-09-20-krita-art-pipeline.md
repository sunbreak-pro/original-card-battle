# Krita での立ち絵づくり（引き継ぎ書）

- 更新日: 2026-09-20 / 対象ブランチ: `feat/depiction-card-readability`（ドキュメントのみ。実装は別チャット）
- 正本: `docs/reports/2026-09-19-krita-ai-setup-guide.html`（手順書）/ `.claude/skills/visual-production-pipeline/references/tools-and-prerequisites.md` §11・§12 / `docs/reports/2026-09-19-visual-production-survey.html`（0 円化の調査）

## いまどこ

Krita AI Diffusion 1.53.0 の導入と設定が終わり、1024×1024 の試し生成に成功しました（28 ステップで約 16 秒、VRAM 5.3 / 8.1 GB）。モデルは Animagine XL 4.0 opt で、スタイル `card-battle Animagine`（`architecture: sdxl`）を作成済みです。まだ 1 枚も本番の候補を出していません。次は長柄の歪み兵（`polearm_warped`）の外見の案出しです。

## 確認コマンド

| 目的                 | コマンド                                                                | 期待                                                                        |
| -------------------- | ----------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| サーバーの起動       | `curl -s http://127.0.0.1:8188/system_stats`                            | JSON が返る。返らなければ Krita を起動してもらう                            |
| モデルの一覧         | `curl -s http://127.0.0.1:8188/object_info/CheckpointLoaderSimple`      | `animagine-xl-4.0-opt.safetensors` がある                                   |
| スタイルの設定       | `type "%APPDATA%\krita\ai_diffusion\styles\anime-illustrious.json"`     | `name` = card-battle Animagine、`architecture` = sdxl、`sampler_steps` = 28 |
| 直近の落ちと生成時間 | `tail -40 "%APPDATA%\krita\ai_diffusion\logs\server.log"`               | `Fatal Python error` が増えていない                                         |
| VRAM の空き          | `nvidia-smi --query-gpu=memory.used,memory.total --format=csv,noheader` | 生成前で 4 GB 以上空いている                                                |
| 生成済みの素材       | `ls "%APPDATA%\krita\ai_diffusion\ComfyUI\ComfyUI\output"`              | 前回からの追加があるか（人が生成を続けていた場合）                          |

## 次の一手

1024×1536 の新しい文書で、スタイル `card-battle Animagine`、生成回数 4 で、長柄の歪み兵の候補を 8〜16 枚出してもらいます。指示文は手順書 4 章の段階 1 のワイルドカード版をそのまま使います。出た候補 2〜3 枚を Claude が受け取り、ゲーム画面の高さ 300 px で形が見分けられるかを確かめて、絞り込みの指示を作ります。

## 決まっていること

- 生成は Local Managed Server（ローカル 0 円）。画像 API とクラウドは使わない。Gemini の画像モデルに無料枠が無いため。
- モデルは Animagine XL 4.0 opt（OpenRAIL++-M、商用可）。ポーズ差分は Flux 2 Klein 4B（Apache-2.0）の編集機能。
- Illustrious / NoobAI 系の部品（`novaAnimeXL`、`noob-*`）は製品の素材に使わない。fair-ai-public-license が商用を制限するため。スタイルの Base model は `sdxl` 固定。
- 「Remove Content」と Custom の Fill「Inpaint」は使わない。MAT（CC BY-NC 4.0）を呼ぶため。Live の結果と Pose の From image も製品用には使わない（Hyper-SD と YOLO-NAS のライセンスが未確認）。
- 立ち絵は 1024×1536、右向き、足元中央が基準、背景は生成時は灰色で後から抜く。体勢（構え）の差分は作らない。
- 記録は Interface の Dump Workflow が出す `workflow.json`（モデル・seed・指示文）と、人の加筆内容・時間を台帳に写す。

## 判断待ち

- **主人公の見た目**（性別・年齢・装束・武器）が未決。推奨は、長柄の歪み兵の候補を見て絵柄の傾向を決めてから主人公に進むこと。敵のほうが外しても捨てやすい。
- **0 円化の 4 件**（CSP を買ったか / 生成の主経路をローカルに差し戻すか / ローカルの実測を先にするか / ボスを 2D Animation にするか）。詳細は `docs/reports/2026-09-19-visual-production-survey.html` の「次に判断が要る点」。
- **Comfy Desktop 1.0.47** を残すか消すか。残す場合も生成サーバーと同時起動しない。

## 落とし穴

- **大きなキャンバスでサーバーが落ちる。** 2480×3508 の文書で生成すると、拡大処理の VAE encode で `Fatal Python error: Aborted`。1024×1536 までにし、Performance の Maximum pixel count を 1 MP にする。
- **スタイルを複製すると Base model が複製元のまま。** `Anime ★` から複製すると `architecture: illu` を引き継ぐ。Checkpoint configuration (advanced) で SDXL に直す。
- **Krita 本体の「設定 ▸ スタイル」は別物。** あれは Qt の見た目（Fusion）。プラグインの設定は AI画像生成パネル右端の歯車から開く。
- **インストール先は `%APPDATA%\krita\ai_diffusion\ComfyUI`。** 手順書の `C:\ai\krita-server` ではない。モデルを足すときはこちらへ置く。
- **Refine の前に Apply を忘れると白くなる。** 生成結果は確定しないとキャンバスに載らない。
