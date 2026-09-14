# ツールと前提（visual-production-pipeline の正本）

- 確認日: 2026-09-14（survey の 6 観点 + 反証役 4 + 批評役。外部の主張 265 件のうち、確認 231 / 訂正 30 / 未確認 4。結論を左右する 5 件は Claude が一次情報を直接開いて再確認した）
- 判断の回答: 2026-09-14（§9。戦闘の所感の反映と合わせて回答済み）
- 読む用のレポート: `docs/reports/2026-09-14-visual-production-survey.html`（回答前のスナップショット）
- 価格・ライセンスは変わります。使う直前に `terms_url` を開き、台帳の `terms_checked_on` を書き換えます。

略称: `v2` = `.claude/docs/battle_document/battle_ui_ux_v2.md`、`core` = `.claude/docs/battle_document/battle_core_v4.md`、`roster` = `.claude/docs/enemy_document/enemy_roster_v4.md`、`art-plan` = `.claude/docs/vision/plans/2026-06-28-unity-migration-character-art.md`。

## 1. 既定の道筋

1. 候補は画像 API で作ります（Gemini を主、OpenAI を副、月の上限 $30）。
2. 人が CLIP STUDIO PAINT PRO で加筆し、パーツに分けます。試走の 1 体はこうだいさんが行い、かかった時間を測ります。
3. 差分スプライトと白シルエット PNG を、いまの人型の `Image` に差し替えます（段階 0）。間合いは立ち位置と一字札で見せるので、体勢の差分は作りません。
4. Live2D は主人公とボスの 2 体だけにします。Unity は 6000.5 のまま進め、Live2D を入れる時点で Cubism SDK を半日試します。
5. UI は HTML モックアップから UGUI の C#（`BattleTheme` / `UiKit` / `UiTween`）に写し、ビルド撮影で数値を確かめます。
6. 演出では Particle System を使ってよくなりました（2026-09-14 決定、`v2:1072` の「使わない」を覆す）。Overlay の Canvas に出すため、ParticleEffectForUGUI を第一候補にします。
7. 組む順は、スタイルガイドと主人公の見た目 → 長柄の歪み兵 1 体で全工程 → 残り 8 体と UI です。

## 2. 決定済みの前提

| 項目           | 内容                                                                                                                                                     | 出典                                           |
| -------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------- |
| テーマ         | 「死ぬたびに人は変わる。だが、技と記憶は次の生へ。」操作するのは一人の固定キャラクター                                                                   | `concept-v3.md:14-16`                          |
| 主人公         | 見た目は固定 1 体。世代の違いは小物と色の差分で出す（2026-09-14 決定）                                                                                   | §9                                             |
| 絵柄           | アニメ 2.5D（Live2D は主人公とボス）。モチーフは瘴気と手記（和紙・墨・筆線）                                                                             | `v2:511`、`art-plan:19`                        |
| 画面の見た目   | A「霧と灯り」の A+。色は役割にだけ使う。omen は予兆・敵・被弾・敵の作用だけ                                                                              | `v2:535-540`、`v2:544-591`                     |
| 間合い         | キャラクターごとに近間か遠間を持つ（常設の値、素の効果なし）。近間は前へ出て、遠間は下がって立ち、足元に「近」「遠」の一字札（2026-09-14 決定）          | `core` §16.4、`v2` §10.6                       |
| 人型の枠       | 140×300、足元 pivot。敵は `scaleX=-1` で反転する                                                                                                         | `ArenaView.cs:13,29`                           |
| 人型の描き方   | Rim（warm α0.5、1.5 px ずらし）/ Fill / Flash（白）/ Ghost（α0.35）が同じ Sprite を色の乗算で使う。塗った絵には白シルエットが要る                        | `ArenaView.cs:207-212`、`ProceduralArt.cs:57`  |
| 演出の作り     | Particle System を使ってよい（2026-09-14 決定）。靄と火の粉は `Image` の手動アニメでもよい。トゥイーンは `UiTween`                                       | `v2` §10.8                                     |
| 書体           | Shippori Mincho / Zen Kaku Gothic New / IBM Plex Mono の .ttf を `Assets/View/Resources/Fonts/` に置き、legacy `Text` で使う。Yuji Syuku は使わない      | `v2:1219-1221`、§9                             |
| 背景           | 階層ごとのグラデーションと靄 2 層のまま。塗った絵にしない（2026-09-14 決定）                                                                             | `ArenaView.cs:92-105`、§9                      |
| 解像度         | 基準 1920×1080、`matchWidthOrHeight 0.5`                                                                                                                 | `v2:124`                                       |
| 時間予算       | 1 行動 2.0 秒以内、攻撃 1030 ms、移動 320 ms、倒れ 800 ms                                                                                                | `v2:603,614`                                   |
| Unity の組み方 | Canvas は ScreenSpaceOverlay をコードで組む。シーン YAML の手編集はしない。色は `BattleTheme.cs` だけ                                                    | `PHASE3-KICKOFF.md:173`、`BattleTheme.cs:1-2`  |
| 実プロジェクト | `C:/Users/user/Unity/RPG-by-card`、Unity 6000.5.5f1、URP 2D。2D Animation 15.1.0 / PSD Importer 14.0.3 / Aseprite Importer 5.0.3 導入済み。コミット 0 件 | `ProjectVersion.txt`、`Packages/manifest.json` |
| 凍結           | アーマー・装備・Gold、ショップ・鍛冶屋・サンクチュアリ、旧属性アイコン、ボス第 2 段階の姿、取り巻きの犬の個別の絵                                        | `concept-v3.md:208-225`、`v2:1207-1208`        |

## 3. 手元の環境（2026-09-14 実測）

- ある: RTX 5060 Ti（VRAM 8 GB、うち画面表示が約 1.8 GB）、Ryzen 7 5700X、RAM 32 GB、C: の空き約 300 GiB、Python 3.12.10（Pillow 12.3.0、numpy 2.5.1）、uv、Node v24.16.0、git-lfs 3.7.1、gh、.NET SDK 10、Unity Hub 3.21.1、Unity CLI 1.0.0-beta.6（`unity mcp` を `unity-editor-mcp` として登録済み）、Playwright の chromium-1228、Chrome、Noto Sans JP / Noto Serif JP / BIZ UD 系の書体。
- 無い: Krita / CLIP STUDIO / Photoshop / GIMP / Aseprite / Blender / Live2D / Spine / Inkscape / Figma、ComfyUI と torch、PATH 上の ImageMagick と ffmpeg、DOTween、画像 API のクライアント。
- `.env`: original-card-battle は `chore/ignore-env`（PR #21）で無視するようにした。RPG-by-card はステージ済みの `.gitignore` に足した（コミットは Unity リポの運用を決めるときに行う）。
- LFS: RPG-by-card の `.gitattributes` に `*.psb` と `*.moc3` を足した（ステージのみ）。
- RTX 50 系でローカル生成するなら、CUDA 12.8 以降でビルドされた PyTorch が要る。

## 4. 工程別の採用ツール

| 役割                   | 採用                                                                                                                                                                                         | 代替・保留                                                                                                    | 費用・条件                                                                                                                                                                                                                                                                                                                                                                                                                                 | Claude が動かせるか  |
| ---------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------- |
| コンセプト・影絵       | 仕様カード（md）と SVG の影絵を Playwright で撮る                                                                                                                                            | `design` スキルのキャンバス                                                                                   | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| 加筆・パーツ分け（人） | CLIP STUDIO PAINT PRO 買い切り 6,900 円（https://www.clipstudio.net/ja/purchase/）                                                                                                           | Krita（無料）、Photoshop 3,280 円/月                                                                          | Live2D が PSD の動作を保証するのは Photoshop と CSP（https://docs.live2d.com/en/cubism-editor-manual/divide-the-material/）                                                                                                                                                                                                                                                                                                                | 不可                 |
| 画像生成（主）         | Gemini `gemini-3.1-flash-image`（1K 約 $0.067/枚、Batch 半額、無料枠なし）。月の上限 $30                                                                                                     | `gemini-3.1-flash-lite-image`（1K 約 $0.0336/枚）、`gemini-3-pro-image`（1K 約 $0.134/枚、スタイル参照 3 枚） | https://ai.google.dev/gemini-api/docs/pricing 。Google は生成物の所有権を主張せず、全画像に SynthID が入る（https://ai.google.dev/gemini-api/terms）                                                                                                                                                                                                                                                                                       | 可                   |
| 画像生成（副）         | OpenAI `gpt-image-2` / `gpt-image-2.5-sunburst` / `-flare`                                                                                                                                   | —                                                                                                             | 画像出力 $30 / 100 万トークン、Batch 半額（https://developers.openai.com/api/docs/pricing）。1 枚あたりは**未確認**（公式は計算機のみ）。API Organization Verification が要る場合がある。出力の権利は利用者に譲渡                                                                                                                                                                                                                          | 可                   |
| 画像生成（ローカル）   | 保留。8 GB で SDXL 系の速度と VRAM を測ってから                                                                                                                                              | ComfyUI（GPL-3.0）+ Animagine XL 4.0（改変なしの RAIL++-M、商用可）                                           | torch と CUDA 12.8 以降のビルドの導入から要る                                                                                                                                                                                                                                                                                                                                                                                              | 可                   |
| 一貫性の維持           | 承認済みの絵を参照画像に渡す（flash-image は物体 10・キャラクター 4 枚まで）                                                                                                                 | LoRA（kohya_ss、Python 3.11）、IP-Adapter、controlnet-union-sdxl、DWPose（いずれも Apache 2.0）               | API 代だけ                                                                                                                                                                                                                                                                                                                                                                                                                                 | 可                   |
| 背景除去・拡大         | rembg `-m isnet-anime` か `-m birefnet-general`、拡大は Real-ESRGAN ncnn-vulkan（BSD-3）                                                                                                     | —                                                                                                             | rembg 既定の `bria-rmbg` は重みが CC BY-NC 4.0 なので使わない。isnet-anime と BiRefNet の重みのライセンスは**未確認**                                                                                                                                                                                                                                                                                                                      | 可                   |
| 白シルエット・横幅     | `scripts/silhouette.py`                                                                                                                                                                      | —                                                                                                             | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| 動かす（段階 0）       | `Image.sprite` の差し替え + `UiTween`。体勢の差分は作らず、間合いの切り替えは立ち位置の移動で見せる                                                                                          | —                                                                                                             | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| 動かす（段階 1）       | Unity 2D Animation + PSD Importer（導入済み）。Sprite Skin は Sprite Renderer 前提なので、専用カメラ → RenderTexture → RawImage                                                              | —                                                                                                             | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 配線は可、リグは不可 |
| 動かす（主人公とボス） | Live2D Cubism                                                                                                                                                                                | Spine Professional $379（取消線付き $449 併記、期間限定かは未確認）                                           | FREE は個人と年間売上 1,000 万円未満の事業者なら商用可。FREE の上限はテクスチャ 1 枚（2048 px）・パーツ 30・ArtMesh 100・パラメータ 30（https://www.live2d.com/en/cubism/comparison/）。PRO indie は税込 ¥2,288/月。SDK は小規模なら出版許諾不要。SDK の開発環境は Unity 6000.3.11f1 / 6000.0.71f1 LTS で、6000.5 は載っていない（https://github.com/Live2D/CubismUnityComponents）。Spine は過去 12 か月の収入 $500,000 以上で Enterprise | 配線は可、リグは不可 |
| VFX                    | Particle System（2026-09-14 に許可）。Overlay の Canvas には ParticleEffectForUGUI（MIT、https://github.com/mob-sakai/ParticleEffectForUGUI）で出す。`Image` の手動アニメと `UiTween` も併用 | PrimeTween（無料）はヒットストップや一括スキップが要るとき                                                    | VFX Graph はコンピュートシェーダー必須で Canvas に載らない。減速設定は `UiTween` と Particle の両方にそろえる                                                                                                                                                                                                                                                                                                                              | 可                   |
| UI デザイン            | HTML モックアップ（`docs/mockups/`）+ Artifact                                                                                                                                               | Penpot（MPL-2.0、公式 MCP）                                                                                   | Figma は Starter の MCP 呼び出しが月 20 回なので採らない                                                                                                                                                                                                                                                                                                                                                                                   | 可                   |
| UI 実装                | UGUI を C# で組む                                                                                                                                                                            | UI Toolkit はメニュー画面で後日                                                                               | Timeline と連携できるのは UGUI                                                                                                                                                                                                                                                                                                                                                                                                             | 可                   |
| 書体                   | 上記 3 書体の .ttf（OFL）                                                                                                                                                                    | —（Yuji Syuku は使わない）                                                                                    | OFL はライセンス文の同梱が必要                                                                                                                                                                                                                                                                                                                                                                                                             | 可                   |
| アイコン               | game-icons.net の SVG を着色し、chromium で PNG に焼く                                                                                                                                       | Kenney（CC0）                                                                                                 | game-icons.net は CC BY 3.0 で、アイコンごとの作者表記が必須                                                                                                                                                                                                                                                                                                                                                                               | 可                   |
| Unity の操作           | Unity CLI の MCP（導入済み）。Unity 公式の Claude Code プラグイン（2026-09-09 公開、29 スキル + Unity CLI + MCP）                                                                            | CoplayDev/unity-mcp（MIT）                                                                                    | 公式プラグインと公式 MCP に Unity AI の契約が要るかは**未確認**（https://unity.com/blog/unity-plugin-for-claude-code）                                                                                                                                                                                                                                                                                                                     | Editor 起動中は可    |
| 検証                   | visual-inspect、`scripts/color_check.py`、ビルド撮影 `-captureDir -captureEvery -demoUi`                                                                                                     | Playwright の CDP で色覚模擬（Chromium のみ、Experimental）                                                   | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| 記録                   | `templates/asset-ledger.csv`（30 列）。AI の区分は IPTC Digital Source Type                                                                                                                  | —                                                                                                             | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| バージョン管理         | 取り込む素材は RPG-by-card の LFS（`*.psb` `*.moc3` を追加済み）。作業ファイル（`.clip` / `.kra` / `.cmo3` / `.can3`）は OneDrive に置く                                                     | 容量が足りなければ C: のローカル                                                                              | GitHub Free の LFS は保存 10 GiB・帯域 10 GiB、超過は従量課金。OneDrive の容量は未確認                                                                                                                                                                                                                                                                                                                                                     | 可                   |

**使わないもの**: Midjourney（規約が自動ツールでの生成を禁じる）、NoobAI 系（生成物の商用化を禁じる）、CMU OpenPose（非商用の研究目的に限る）、rembg の既定モデル `bria-rmbg`、VFX Graph、Figma MCP、Yuji Syuku。

**費用の目安**: 基本の立ち姿 10 体 × 候補 8 = 80 枚です。`gemini-3.1-flash-image` の 1K で約 $5.4、Batch で約 $2.7 です。差分 66 枚を編集で作る分と参照画像の入力は含みません。月の上限は $30 です。OpenAI は 1 体目で実測します。

## 5. 必要なアセット（80% 範囲）

| id                | 階級 / 相手の間合い / 階層 | 見た目の手がかり                 | 数（推奨の道筋）                                         |
| ----------------- | -------------------------- | -------------------------------- | -------------------------------------------------------- |
| swordsman         | 主人公                     | 設定なし。技は斬 / 突 / 払 / 打  | Live2D（モーション数は体勢の分を外して v2.1 で数え直す） |
| polearm_warped    | 通常 / 階層 1〜2           | 長柄。薙ぎ・突き・石突き・柄受け | 差分 9                                                   |
| shadow_hound      | 通常 / 階層 1〜2           | 四足、速く脆い                   | 差分 8                                                   |
| rusted_revenant   | 通常 / 階層 2〜3           | 錆びた鎧、重く遅い               | 差分 8                                                   |
| crossbow_hunter   | 通常 / 階層 2〜3           | 弩と盾                           | 差分 8                                                   |
| mist_archer       | 通常 / 階層 3〜4           | 弓、靄に溶ける                   | 差分 7                                                   |
| twin_blade_warped | 通常 / 階層 3〜4           | 二刀                             | 差分 8                                                   |
| armored_warden    | 精鋭 / 階層 3〜4           | 全身の甲冑と盾                   | 差分 9                                                   |
| pack_alpha        | 精鋭 / 階層 4              | 大型の獣、表示 1.25 倍           | 差分 9                                                   |
| pack_hound        | 随伴                       | shadow_hound の色替え            | テクスチャ 1                                             |
| miasma_priest     | ボス / 階層 5              | ローブと錫杖                     | Live2D（同上）                                           |

出典は `roster:132-313` と `v2:1194-1209` です。差分の数は、旧表から体勢 3 枚を外した値です（合計 66 枚）。敵ごとの得意な間合い（旧 近 / 中 / 遠）は、v4.2 で近間 / 遠間に読み替えます。

- **共通の要件**: 透過 PNG、右向き、足元 pivot、枠 140×300 の比率。差分ごとに白シルエット。体勢の差分は作らない。
- **表示の最大**: 群れの長で 300 × 1.08 × 1.25 = 405 px（1080p）、4K で 810 px。立ち位置で見せると縦の縮尺 1.08 が不要になる見込みで、そのときは 375 px / 750 px。
- **テクスチャ予算**: 生成は 1024×1536 で原本を保存し、取り込みは長辺 1024・BC7 にします。差分 66 枚で約 44 MiB です（計算値、白シルエットは別）。
- **VFX**: 系統素材 7 と状態アイコン 12（`v2:661-683,816-829`）。Particle で作る系統は、素材の代わりにパーティクルの設定を持つ。
- **UI 部品**: カード 230×250、スタンス枠 134×96、状態チップ 104×26、予兆バナー 440×64、休憩 768×432、結果 922×626、手記ドロワー（`v2:163,1057,1217`）。投入帯と予測札は廃止（`v2` §10.4）。相手の選び方の部品はデモの後に決める。
- **クレジット**: v2 の残り 20% に画面を足す（2026-09-14 決定）。game-icons.net の作者一覧と OFL の本文を載せる。

## 6. 読みやすさ（`scripts/color_check.py` の計算値）

- 階層 1 の背景上端 `#1d3340` に対して、omen 4.32:1、boss 4.28:1、whiff 3.68:1 で、本文の 4.5:1 を下回ります。これらの色を背景に直接置く文字に使わず、下に panel の帯を敷く規則を v2 に提案します。
- 役割色の組で ΔE 20 を下回るのは 4 組です。warm と amber は正常色覚でも 12.3 です。omen と amber は 2 型色覚で 13.3、warm と omen は 2 型色覚で 18.7、whiff と boss は 3 型色覚で 19.0 です。
- 投入の色（投入帯と amber の投入の役割）は外してから登録します。近間 / 遠間は新しい色を足さず、立ち位置と一字札で見せます（2026-09-14 決定）。
- ΔE 20 は規格ではありません。プロジェクトで決める仮の値です。

## 7. 法務とプラットフォーム

- **Steam**: ゲームに同梱してプレイヤーが目にする AI 生成物は Pre-Generated として開示します。開発効率化ツールは対象の中心ではありません（https://partner.steamgames.com/doc/gettingstarted/contentsurvey）。マーケティング素材が対象かは未確認です。
- **日本**: 文化庁「AIと著作権に関する考え方について」（2024-03-15）は、人が創作的に加筆した部分に著作物性を認めます。試行回数や選択だけでは寄与になりません。侵害には類似性と依拠性の両方が要ります。「チェックリスト＆ガイダンス」（2024-07-31）は、類似の確認と生成過程の保存を求めます。
- **米国**: プロンプトだけでは著作者になりません（2025-01-29）。
- **購入素材**: Unity Asset Store の素材は、同意なく AI の学習に使えません。参照画像にも入れません。
- **外注**: 著作権法 61 条 2 項により、27 条・28 条を特掲しないと譲渡されたと推定されません。契約に 27 条・28 条の特掲、著作者人格権の不行使特約、AI 利用の申告を入れます。
- **開示文の草案**: Some enemy and character illustrations were generated with AI image models and then repainted, split into parts and animated by the developer. UI, card text and game systems were designed by the developer.

## 8. 台帳の列（30 列）

`asset_id, file, kind, subject, variant, made_by, tool, model_or_source, model_snapshot, model_license, terms_url, terms_checked_on, prompt_file, workflow_file, seed, reference_inputs, postprocess, human_edit, human_minutes, reviewer, similarity_check, commercial_ok, attribution, iptc_digital_source_type, watermark, disclosure, cost_usd, source_file, date, notes`

`human_edit` と `human_minutes` と選定の理由（`notes`）が、人の創作的寄与の記録になります。置き場は Unity プロジェクトの `Docs/art/asset-ledger.csv` を既定にします。

## 9. 判断（2026-09-14 に回答済み）

| #   | 判断                    | 回答                                                                                         |
| --- | ----------------------- | -------------------------------------------------------------------------------------------- |
| 1   | Unity の版              | 6000.5 のまま進める。Live2D を入れる時点で Cubism SDK を半日試し、動かなければ 6000.3 LTS へ |
| 2   | 立ち絵の描画方式        | 段階 0 は `Image.sprite` の差し替え + 白シルエット。体勢の差分は作らない                     |
| 3   | Live2D の範囲           | 主人公とボスだけ                                                                             |
| 4   | 試走の 1 体             | 長柄の歪み兵（スタイルと主人公の見た目を先に決める）                                         |
| 5   | 主人公の世代差          | 固定 1 体。小物と色の差分                                                                    |
| 6   | Particle                | 全面的に使ってよい（`v2:1072` を覆す）                                                       |
| 7   | Yuji Syuku              | 使わない                                                                                     |
| 8   | 生成サイズ              | 候補 8 枚、生成 1024×1536、取り込み長辺 1024・BC7                                            |
| 9   | 作業ファイルの置き場    | OneDrive（容量が足りなければ C: のローカル）                                                 |
| 10  | `.env` と LFS の修正    | Claude が入れた（original-card-battle は PR #21、RPG-by-card はステージのみ）                |
| 11  | 背景                    | グラデーションと靄のまま                                                                     |
| 12  | 色トークン              | 投入の色を外してから登録。近間 / 遠間は新しい色を足さず、形で見せる                          |
| 13  | 配置の許容値            | モックアップの作り直し後に ±8 px                                                             |
| 14  | 生成の主経路            | クラウド主（Gemini 主・OpenAI 副）、月の上限 $30                                             |
| 15  | 加筆・パーツ分け・リグ  | 試走の 1 体はこうだいさんが行い、時間を測ってから残りを決める                                |
| 16  | クレジットと art の範囲 | クレジット画面を v2 の残り 20% に足す。art は別のフェーズとして開く                          |
| 17  | 体勢差分の軸            | 作らない（間合いは立ち位置と一字札で見せる）                                                 |

## 10. 未確認

- OpenAI `gpt-image-2` 系の 1 枚あたりの価格（公式はトークン単価と計算機のみ）と、透過出力の扱い（版で違う）。
- isnet-anime / BiRefNet の重みのライセンス、Illustrious XL の版ごとの正本ライセンス。
- Unity 公式 Claude Code プラグインと公式 MCP に Unity AI の契約が要るか。
- Canvas Shader Graph が Overlay の Canvas で動くか。Live2D を uGUI に直接出せるか。Sprite Resolver が UGUI の `Image` を切り替えられるか。
- ParticleEffectForUGUI が Unity 6000.5 と URP 2D で動くか。
- Steam の AI 開示がマーケティング素材に及ぶか。Live2D SDK に表記の義務があるか。
- 1 体あたりの人の工数（個人ブログの初回 Live2D は約 30 時間、うち 20 時間がイラストとパーツ分け）。
- OneDrive の容量とプラン。
- RPG-by-card の URP が manifest の 17.6.0 と lock の 17.5.0 のどちらで解決されるか。
