# ツールと前提（visual-production-pipeline の正本）

- 確認日: 2026-09-14（survey の 6 観点 + 反証役 4 + 批評役。外部の主張 265 件のうち、確認 231 / 訂正 30 / 未確認 4。結論を左右する 5 件は Claude が一次情報を直接開いて再確認した）
- 読む用のレポート: `docs/reports/2026-09-14-visual-production-survey.html`
- 価格・ライセンスは変わります。使う直前に `terms_url` を開き、台帳の `terms_checked_on` を書き換えます。

略称: `v2` = `.claude/docs/battle_document/battle_ui_ux_v2.md`、`roster` = `.claude/docs/enemy_document/enemy_roster_v4.md`、`art-plan` = `.claude/docs/vision/plans/2026-06-28-unity-migration-character-art.md`。

## 1. 既定の道筋

1. 候補は画像 API で作ります（Gemini を主、OpenAI を副）。
2. 人が CLIP STUDIO PAINT PRO で加筆し、パーツに分けます。
3. 差分スプライトと白シルエット PNG を、いまの人型の `Image` に差し替えます（段階 0）。
4. Live2D は主人公とボスの 2 体だけにします。残り 8 体は差分から始め、要るときに Unity 2D Animation に上げます。
5. UI は HTML モックアップから UGUI の C#（`BattleTheme` / `UiKit` / `UiTween`）に写し、ビルド撮影で数値を確かめます。
6. 演出は v2 に従い、Particle を使わず `Image` の手動アニメと `UiTween` で作ります（`v2:1072`）。
7. 組む順は、判断の確定 → スタイルガイドと主人公の見た目 → 長柄の歪み兵 1 体で全工程 → 残り 8 体と UI です。

## 2. 決定済みの前提

| 項目           | 内容                                                                                                                                                     | 出典                                           |
| -------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------- |
| テーマ         | 「死ぬたびに人は変わる。だが、技と記憶は次の生へ。」操作するのは一人の固定キャラクター                                                                   | `concept-v3.md:14-16`                          |
| 絵柄           | アニメ 2.5D（Live2D 本命）。モチーフは瘴気と手記（和紙・墨・筆線）                                                                                       | `v2:511`、`art-plan:19`                        |
| 画面の見た目   | A「霧と灯り」の A+。色は役割にだけ使う。omen は予兆・敵・被弾・敵の作用だけ                                                                              | `v2:535-540`、`v2:544-591`                     |
| 間合い         | 実距離（近 260 / 中 520 / 遠 820 px）と体勢（近 前傾 12°・縦 1.08 / 中 直立 / 遠 後傾 8°・縦 0.94）                                                      | `v2:157,170`、`BattleTheme.cs:85-98`           |
| 人型の枠       | 140×300、足元 pivot。敵は `scaleX=-1` で反転する                                                                                                         | `ArenaView.cs:13,29`                           |
| 人型の描き方   | Rim（warm α0.5、1.5 px ずらし）/ Fill / Flash（白）/ Ghost（α0.35）が同じ Sprite を色の乗算で使う。塗った絵には白シルエットが要る                        | `ArenaView.cs:207-212`、`ProceduralArt.cs:57`  |
| 演出の作り     | Particle を使わない。靄と火の粉は `Image` の手動アニメ、トゥイーンは `UiTween`                                                                           | `v2:562,1072`                                  |
| 書体           | Shippori Mincho / Zen Kaku Gothic New / IBM Plex Mono の .ttf を `Assets/View/Resources/Fonts/` に置き、legacy `Text` で使う。TextMeshPro は残り 20%     | `v2:1219-1221`                                 |
| 解像度         | 基準 1920×1080、`matchWidthOrHeight 0.5`                                                                                                                 | `v2:124`                                       |
| 時間予算       | 1 行動 2.0 秒以内、攻撃 1030 ms、移動 320 ms、倒れ 800 ms                                                                                                | `v2:603,614`                                   |
| Unity の組み方 | Canvas は ScreenSpaceOverlay をコードで組む。シーン YAML の手編集はしない。色は `BattleTheme.cs` だけ                                                    | `PHASE3-KICKOFF.md:173`、`BattleTheme.cs:1-2`  |
| 実プロジェクト | `C:/Users/user/Unity/RPG-by-card`、Unity 6000.5.5f1、URP 2D。2D Animation 15.1.0 / PSD Importer 14.0.3 / Aseprite Importer 5.0.3 導入済み。コミット 0 件 | `ProjectVersion.txt`、`Packages/manifest.json` |
| 凍結           | アーマー・装備・Gold、ショップ・鍛冶屋・サンクチュアリ、旧属性アイコン、ボス第 2 段階の姿、取り巻きの犬の個別の絵                                        | `concept-v3.md:208-225`、`v2:1207-1208`        |

## 3. 手元の環境（2026-09-14 実測）

- ある: RTX 5060 Ti（VRAM 8 GB、うち画面表示が約 1.8 GB）、Ryzen 7 5700X、RAM 32 GB、C: の空き約 300 GiB、Python 3.12.10（Pillow 12.3.0、numpy 2.5.1）、uv、Node v24.16.0、git-lfs 3.7.1、gh、.NET SDK 10、Unity Hub 3.21.1、Unity CLI 1.0.0-beta.6（`unity mcp` を `unity-editor-mcp` として登録済み）、Playwright の chromium-1228、Chrome、Noto Sans JP / Noto Serif JP / BIZ UD 系の書体。
- 無い: Krita / CLIP STUDIO / Photoshop / GIMP / Aseprite / Blender / Live2D / Spine / Inkscape / Figma、ComfyUI と torch、PATH 上の ImageMagick と ffmpeg、DOTween、画像 API のクライアント。
- 注意: 両リポジトリで `.env` が無視されていない（`git check-ignore -v .env` が終了コード 1）。API キーを置く前に直す。RTX 50 系でローカル生成するなら CUDA 12.8 以降でビルドされた PyTorch が要る。

## 4. 工程別の採用ツール

| 役割                   | 採用                                                                                                                            | 代替・保留                                                                                                    | 費用・条件                                                                                                                                                                                                                                                                                                                                                                                                                                 | Claude が動かせるか  |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------- |
| コンセプト・影絵       | 仕様カード（md）と SVG の影絵を Playwright で撮る                                                                               | `design` スキルのキャンバス                                                                                   | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| 加筆・パーツ分け（人） | CLIP STUDIO PAINT PRO 買い切り 6,900 円（https://www.clipstudio.net/ja/purchase/）                                              | Krita（無料）、Photoshop 3,280 円/月                                                                          | Live2D が PSD の動作を保証するのは Photoshop と CSP（https://docs.live2d.com/en/cubism-editor-manual/divide-the-material/）                                                                                                                                                                                                                                                                                                                | 不可                 |
| 画像生成（主）         | Gemini `gemini-3.1-flash-image`（1K 約 $0.067/枚、Batch 半額、無料枠なし）                                                      | `gemini-3.1-flash-lite-image`（1K 約 $0.0336/枚）、`gemini-3-pro-image`（1K 約 $0.134/枚、スタイル参照 3 枚） | https://ai.google.dev/gemini-api/docs/pricing 。Google は生成物の所有権を主張せず、全画像に SynthID が入る（https://ai.google.dev/gemini-api/terms）                                                                                                                                                                                                                                                                                       | 可                   |
| 画像生成（副）         | OpenAI `gpt-image-2` / `gpt-image-2.5-sunburst` / `-flare`                                                                      | —                                                                                                             | 画像出力 $30 / 100 万トークン、Batch 半額（https://developers.openai.com/api/docs/pricing）。1 枚あたりは**未確認**（公式は計算機のみ）。API Organization Verification が要る場合がある。出力の権利は利用者に譲渡                                                                                                                                                                                                                          | 可                   |
| 画像生成（ローカル）   | 保留。8 GB で SDXL 系の速度と VRAM を測ってから                                                                                 | ComfyUI（GPL-3.0）+ Animagine XL 4.0（改変なしの RAIL++-M、商用可）                                           | torch と CUDA 12.8 以降のビルドの導入から要る                                                                                                                                                                                                                                                                                                                                                                                              | 可                   |
| 一貫性の維持           | 承認済みの絵を参照画像に渡す（flash-image は物体 10・キャラクター 4 枚まで）                                                    | LoRA（kohya_ss、Python 3.11）、IP-Adapter、controlnet-union-sdxl、DWPose（いずれも Apache 2.0）               | API 代だけ                                                                                                                                                                                                                                                                                                                                                                                                                                 | 可                   |
| 背景除去・拡大         | rembg `-m isnet-anime` か `-m birefnet-general`、拡大は Real-ESRGAN ncnn-vulkan（BSD-3）                                        | —                                                                                                             | rembg 既定の `bria-rmbg` は重みが CC BY-NC 4.0 なので使わない。isnet-anime と BiRefNet の重みのライセンスは**未確認**                                                                                                                                                                                                                                                                                                                      | 可                   |
| 白シルエット・横幅     | `scripts/silhouette.py`                                                                                                         | —                                                                                                             | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| 動かす（段階 0）       | `Image.sprite` の差し替え + `UiTween`                                                                                           | —                                                                                                             | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| 動かす（段階 1）       | Unity 2D Animation + PSD Importer（導入済み）。Sprite Skin は Sprite Renderer 前提なので、専用カメラ → RenderTexture → RawImage | —                                                                                                             | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 配線は可、リグは不可 |
| 動かす（主人公とボス） | Live2D Cubism                                                                                                                   | Spine Professional $379（取消線付き $449 併記、期間限定かは未確認）                                           | FREE は個人と年間売上 1,000 万円未満の事業者なら商用可。FREE の上限はテクスチャ 1 枚（2048 px）・パーツ 30・ArtMesh 100・パラメータ 30（https://www.live2d.com/en/cubism/comparison/）。PRO indie は税込 ¥2,288/月。SDK は小規模なら出版許諾不要。SDK の開発環境は Unity 6000.3.11f1 / 6000.0.71f1 LTS で、6000.5 は載っていない（https://github.com/Live2D/CubismUnityComponents）。Spine は過去 12 か月の収入 $500,000 以上で Enterprise | 配線は可、リグは不可 |
| VFX                    | `Image` の手動アニメ + `UiTween` の Shake / Pop / Tint                                                                          | PrimeTween（無料）はヒットストップや一括スキップが要るとき                                                    | VFX Graph はコンピュートシェーダー必須で Canvas に載らない                                                                                                                                                                                                                                                                                                                                                                                 | 可                   |
| UI デザイン            | HTML モックアップ（`docs/mockups/`）+ Artifact                                                                                  | Penpot（MPL-2.0、公式 MCP）                                                                                   | Figma は Starter の MCP 呼び出しが月 20 回なので採らない                                                                                                                                                                                                                                                                                                                                                                                   | 可                   |
| UI 実装                | UGUI を C# で組む                                                                                                               | UI Toolkit はメニュー画面で後日                                                                               | Timeline と連携できるのは UGUI                                                                                                                                                                                                                                                                                                                                                                                                             | 可                   |
| 書体                   | 上記 3 書体の .ttf（OFL）                                                                                                       | Yuji Syuku は手記の画面まで保留                                                                               | OFL はライセンス文の同梱が必要                                                                                                                                                                                                                                                                                                                                                                                                             | 可                   |
| アイコン               | game-icons.net の SVG を着色し、chromium で PNG に焼く                                                                          | Kenney（CC0）                                                                                                 | game-icons.net は CC BY 3.0 で、アイコンごとの作者表記が必須                                                                                                                                                                                                                                                                                                                                                                               | 可                   |
| Unity の操作           | Unity CLI の MCP（導入済み）。Unity 公式の Claude Code プラグイン（2026-09-09 公開、29 スキル + Unity CLI + MCP）               | CoplayDev/unity-mcp（MIT）                                                                                    | 公式プラグインと公式 MCP に Unity AI の契約が要るかは**未確認**（https://unity.com/blog/unity-plugin-for-claude-code）                                                                                                                                                                                                                                                                                                                     | Editor 起動中は可    |
| 検証                   | visual-inspect、`scripts/color_check.py`、ビルド撮影 `-captureDir -captureEvery -demoUi`                                        | Playwright の CDP で色覚模擬（Chromium のみ、Experimental）                                                   | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| 記録                   | `templates/asset-ledger.csv`（30 列）。AI の区分は IPTC Digital Source Type                                                     | —                                                                                                             | 0 円                                                                                                                                                                                                                                                                                                                                                                                                                                       | 可                   |
| バージョン管理         | RPG-by-card の LFS に `*.psb` と `*.moc3` を足す。作業ファイルは Git の外                                                       | —                                                                                                             | GitHub Free の LFS は保存 10 GiB・帯域 10 GiB、超過は従量課金                                                                                                                                                                                                                                                                                                                                                                              | 可                   |

**使わないもの**: Midjourney（規約が自動ツールでの生成を禁じる）、NoobAI 系（生成物の商用化を禁じる）、CMU OpenPose（非商用の研究目的に限る）、rembg の既定モデル `bria-rmbg`、VFX Graph、Figma MCP。

**費用の目安**: 10 体 × 体勢 3 × 候補 8 = 240 枚。`gemini-3.1-flash-image` の 1K で約 $16、Batch で約 $8 です。参照画像の入力と、差分を編集で作る分は含みません。OpenAI は 1 体目で実測します。

## 5. 必要なアセット（80% 範囲）

| id                | 階級 / 間合い / 階層 | 見た目の手がかり                 | 数（推奨の道筋）                    |
| ----------------- | -------------------- | -------------------------------- | ----------------------------------- |
| swordsman         | 主人公               | 設定なし。技は斬 / 突 / 払 / 打  | Live2D: モーション 12・パラメータ 3 |
| polearm_warped    | 通常 / 中 / 1〜2     | 長柄。薙ぎ・突き・石突き・柄受け | 差分 12                             |
| shadow_hound      | 通常 / 近 / 1〜2     | 四足、速く脆い                   | 差分 11                             |
| rusted_revenant   | 通常 / 近 / 2〜3     | 錆びた鎧、重く遅い               | 差分 11                             |
| crossbow_hunter   | 通常 / 遠 / 2〜3     | 弩と盾                           | 差分 11                             |
| mist_archer       | 通常 / 遠 / 3〜4     | 弓、靄に溶ける                   | 差分 10                             |
| twin_blade_warped | 通常 / 中 / 3〜4     | 二刀                             | 差分 11                             |
| armored_warden    | 精鋭 / 近〜中 / 3〜4 | 全身の甲冑と盾                   | 差分 12                             |
| pack_alpha        | 精鋭 / 中 / 4        | 大型の獣、表示 1.25 倍           | 差分 12                             |
| pack_hound        | 随伴 / 近            | shadow_hound の色替え            | テクスチャ 1                        |
| miasma_priest     | ボス / 中〜遠 / 5    | ローブと錫杖                     | Live2D: モーション 12・パラメータ 3 |

出典は `roster:132-313` と `v2:1194-1209` です。`v2:1209` の「モデル 10 + モーション 90 + パラメータ 14 = 120」は全員を同じ方式で作る場合の数です。

- **共通の要件**: 透過 PNG、右向き、足元 pivot、枠 140×300 の比率。差分ごとに白シルエット。
- **表示の最大**: 群れの長で 300 × 1.08 × 1.25 = 405 px（1080p）、4K で 810 px。
- **テクスチャ予算**: 生成は 1024×1536 で原本を保存し、取り込みは長辺 1024・BC7 にします。差分 90 枚で約 60 MiB です（計算値）。
- **VFX**: 系統素材 7 と状態アイコン 12（`v2:661-683,816-829`）。
- **UI 部品**: カード 230×250、投入帯 54 px × 4、予測札 240×170、詳細ポップ 560×280、スタンス枠 134×96、状態チップ 104×26、予兆バナー 440×64、休憩 768×432、結果 922×626、手記ドロワー（`v2:163,1057,1217`）。
- **クレジット**: 正本に画面がありません。game-icons.net の作者一覧と OFL の本文を載せる画面が要ります。

## 6. 読みやすさ（`scripts/color_check.py` の計算値）

- 階層 1 の背景上端 `#1d3340` に対して、omen 4.32:1、boss 4.28:1、whiff 3.68:1 で、本文の 4.5:1 を下回ります。これらの色を背景に直接置く文字に使わず、下に panel の帯を敷く規則を v2 に提案します。
- 役割色の組で ΔE 20 を下回るのは 4 組です。warm と amber は正常色覚でも 12.3 です。omen と amber は 2 型色覚で 13.3、warm と omen は 2 型色覚で 18.7、whiff と boss は 3 型色覚で 19.0 です。
- 狙い帯の「的中」と「半減」は塗りの色だけで分かれます（`ArenaView.cs:361-363`）。形か模様の差を足す案を v2 に上げます。
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

`human_edit` と `human_minutes` と選定の理由（`notes`）が、人の創作的寄与の記録になります。置き場は Unity プロジェクトの `Docs/art/asset-ledger.csv` を既定にします（S0 で見直す）。

## 9. 未決（S0 で決める）と推奨

| 判断                              | 推奨                                                                                                                |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| Unity の版                        | 段階 0〜1 は 6000.5 のまま。Live2D を入れる時点で 6000.5 に Cubism SDK を半日試し、動かなければ 6000.3 LTS へ       |
| 立ち絵の描画方式                  | 段階 0 は `Image.sprite` の差し替え + 白シルエット。段階 1 から RenderTexture → RawImage                            |
| Live2D の範囲                     | 主人公とボスの 2 体だけ                                                                                             |
| 最初に全工程を通す 1 体           | スタイルと主人公の見た目を先に決め、全工程の試走は長柄の歪み兵（コアに入っている唯一の敵）                          |
| 主人公の見た目を世代で変えるか    | 固定 1 体。世代差は小物と色の差分にとどめる                                                                         |
| Particle を使わない決定を覆すか   | 覆さない                                                                                                            |
| Yuji Syuku                        | 手記の画面を作るまで保留し、Shippori Mincho で代える                                                                |
| 生成サイズとテクスチャ予算        | 生成 1024×1536、取り込み長辺 1024・BC7                                                                              |
| 画像と作業ファイルの置き場        | 素材は RPG-by-card の `Assets/Art/`、作業ファイルは Git の外（置き場は OneDrive の容量を見て決める）                |
| `.env` と `.gitattributes` の修正 | Claude が差分を作り、コミットはこうだいさんの指示で                                                                 |
| 背景を塗った絵にするか            | 当面はグラデーションと靄のまま                                                                                      |
| 色のトークンと閾値                | mockup-v4 だけにある色と v4.1 のトークンを v2 §4.5 / §4.8 に登録してから `BattleTheme.cs` へ。ΔE 閾値は 20 を仮置き |
| UI の配置の許容値                 | 基準 1920×1080 で ±8 px を初期値に                                                                                  |
| 生成の主経路                      | クラウド API を主に、月上限 $30 から。ローカルは実測後                                                              |
| 加筆・リグを自分でするか外注か    | 試走の 1 体は自分で行い、`human_minutes` を測る                                                                     |
| クレジット画面と Phase 3 の範囲   | クレジットを v2 の残り 20% に足す。art を別フェーズとして開く                                                       |

## 10. 未確認

- OpenAI `gpt-image-2` 系の 1 枚あたりの価格（公式はトークン単価と計算機のみ）と、透過出力の扱い（版で違う）。
- isnet-anime / BiRefNet の重みのライセンス、Illustrious XL の版ごとの正本ライセンス。
- Unity 公式 Claude Code プラグインと公式 MCP に Unity AI の契約が要るか。
- Canvas Shader Graph が Overlay の Canvas で動くか。Live2D を uGUI に直接出せるか。Sprite Resolver が UGUI の `Image` を切り替えられるか。
- Steam の AI 開示がマーケティング素材に及ぶか。Live2D SDK に表記の義務があるか。
- 1 体あたりの人の工数（個人ブログの初回 Live2D は約 30 時間、うち 20 時間がイラストとパーツ分け）。
- RPG-by-card の URP が manifest の 17.6.0 と lock の 17.5.0 のどちらで解決されるか。
