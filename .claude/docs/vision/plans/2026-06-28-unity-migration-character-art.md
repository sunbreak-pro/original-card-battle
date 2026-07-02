# Plan: Unity 移行 + 2.5D アニメキャラ art（リアル感の本格化）

> **Status**: PLANNED（調査完了・方針決定待ち。**実装は別セッション**）
> **Created**: 2026-06-28
> **Task**: MEMORY.md 進行中「Unity 移行 + 2.5D アニメキャラ art — 調査・計画書」
> **Project**: `/Users/newlife/dev/apps/original-card-battle`（現状）→ Unity プロジェクト（移行後）
> **前提プラン**: `2026-06-28-battle-engine-bakeoff.md`（Web エンジン bake-off。**本移行で実質 moot 化**）
> **入力**: ユーザー方針確定（2026-06-28）— ①ゲーム本体ごと Unity 移行 / ②絵柄はアニメ・2.5D / ③個人開発・まず低コスト中心

---

## Context

### なぜこの計画か

bake-off（Web の PixiJS版 × Phaser版）の実機評価で、ユーザーから「Phaser は好印象だが解像度が低くボタンがカードに被る」「**全体的にリアル感が感じられない**」との所見。リアル感の不足はエンジン（Pixi/Phaser）の差ではなく、**キャラクターの絵そのもの（立ち絵・敵の姿）の作り込み不足**が本質。現状は絵文字・枠・仮 PNG。

そこを「実際のゲームのように」仕上げるべく、ユーザーは **ゲーム本体ごと Unity へ移行**し、**アニメ・2.5D の立ち絵**で世界観を立てる方向を選択（個人開発・低コスト前提）。本ドキュメントは、その実装を**別セッションで行うための調査結果と実行計画**。

### 重要な含意（先に確認）

- **Web 実装は作り直しになる**。引き継げるのは「ゲーム設計・バランス数値・仕様・アート」のみ。ランタイムのコード（戦闘ロジック・UI・今回の bake-off 含む）は全量 C# で再実装。
- **検証済みの「面白さの核」は無駄にならない**。間合い×スタミナの戦闘コア（`src/ui/battle-lab/core/`、純関数 reducer・テスト付き）と `constants.ts` の数値、`docs/requirements/` の Tier 設計は、そのまま C# へ移植して引き継ぐ。
- **bake-off の Pixi vs Phaser 選定は実質不要になる**。フル Unity 化するなら Web の描画エンジンは選ぶ意味がない（コアと比較表は資産として残す）。

### 判断材料 — 「Unity 移行は必須ではない」（正直な代替案）

選択は尊重するが、調査で出た事実として記録する。決定の質を上げるための材料。

- **アニメ 2.5D の立ち絵化だけが目的なら、Web のままでも実現できる**。Live2D はブラウザでも動く（`pixi-live2d-display` で PixiJS に統合可能）。「リアル感＝キャラの作り込み」は、移行せずとも今の検証済み Web 基盤の上で達成しうる。
- 一方 **フル移行のコストは重い**: 調査では個人開発で機能同等まで **6〜12 ヶ月**、Unity 習熟だけで **1〜2 ヶ月**、さらに**インディーゲーム失敗の 70%超がスコープ膨張**。すでに「遊びとして面白い」検証台がある状態でゼロから建て直すリスクは正視すべき。
- **Unity を選ぶ正当な理由**: ①将来 3D/高度演出への拡張余地、②ネイティブ（デスクトップ/モバイル）配布で WebGL のロード問題を回避、③アセット・アニメのツール群とエコシステムが厚い、④Live2D/Spine の公式ネイティブ統合。**「今の見た目を良くする」だけなら過剰、「腰を据えて本物のゲームにする」なら妥当**、という温度感。
- → **推奨スタンス**: 「立ち絵の試作（art パイプライン確立）」を**先に Web で 1 体縦に通して**手応えと品質を確かめ、その上で Unity 移行に踏み切る。art の知見はどちらでも活きるため、移行判断を遅らせるほど損が小さい（後述 Phase 0）。

### Non-goals（このプランに含めない）

実コードの実装（別セッション）／剣気・崩しなど未確定の戦闘設計／バランスの作り直し（数値は検証台のまま移植）／本格的な 3D 化／多言語・課金・オンライン。

---

## 調査サマリ（2026-06-28 / web-researcher 3 本・出典は末尾）

### A. Unity の費用・配布（個人・低コスト観点）

| 項目           | 事実                                                                                             | 個人開発への意味                             |
| -------------- | ------------------------------------------------------------------------------------------------ | -------------------------------------------- |
| ライセンス費用 | **Unity Personal は年商 $200k（約3000万円）まで無料**（Unity 6 で旧 $100k から倍増）             | 当面**実費ゼロ**                             |
| Runtime Fee    | **2024-09 に正式撤廃**（全バージョン非適用）                                                     | 過去の騒動。今から始めるなら無視してよい     |
| スプラッシュ   | "Made with Unity" は Personal では実質消せない（WebGL で数秒表示前提）                           | 体験上の小さな妥協点                         |
| WebGL 配布     | 未最適化で数十〜数百 MB、Brotli+削減で 10〜25MB。初回ロード 10秒超になりやすい・シングルスレッド | **デスクトップ配布を主、Web は補助**が現実的 |
| 学習コスト     | C# 構文 2〜4 週間 / Unity 的思考（GameObject+Update ループ等）に慣れるまで **1〜3 ヶ月**         | 最初の山は言語でなく「考え方」の切り替え     |

### B. 2.5D アニメ表現の選択肢（Unity）

| 手法                   | コスト                                                                          | カードゲーム立ち絵での向き                                   |
| ---------------------- | ------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| **Live2D Cubism**      | エディタ無料枠あり / SDK は**年商1000万円未満は無料**（Pro エディタ ¥3,980/月） | ◎ 元イラストを変形＝塗りを生かした立ち絵。ダーク FT と相性◎  |
| **Spine**              | 買い切り $69（Essential）〜$369 / 年商$500k未満は可                             | ○ 骨格アニメで「ゲーム的」な動き・物理揺れ。Unity 連携が厚い |
| **Unity 2D Animation** | **無料**（標準付属、PSD Importer/IK）                                           | △ シンプルな立ち絵なら十分。品質は Spine 未満                |
| **3D+トゥーン/VRoid**  | VRoid 無料                                                                      | △ 顔の向き変化に強いが「イラスト感」が出にくい               |

→ **ダークファンタジーのアニメ立ち絵は Live2D が本命**。最安・最速で試すなら「パーツ分割スプライト差し替え」。

### C. アニメ art 制作パイプライン（AI + 低コスト）

- **アニメ AI モデル（2026）**: AnimagineXL 4.0 / Illustrious XL / Pony Diffusion XL（事実上の標準）/ NovelAI V4（クラウドで最も手軽）。**同一キャラの別ポーズ・別表情**は IP-Adapter（参照画像1枚・最速）か キャラ専用 LoRA（10〜30枚学習・最も安定）。
- **商用利用（重要）**: SD 系（Illustrious/Pony, CreativeML Open RAIL-M）は**商用 OK・上限/ロイヤリティなし**。NovelAI もプラン内 OK。**落とし穴**: ①AI のみ生成物は著作権が**無保護**になりうる（日米とも「人間の創作的寄与」要）、②特定作家スタイルの顕著な再現は類似性侵害リスク、③**Steam は AI コンテンツの開示義務**、④投資 DD で IP 由来が曖昧だと詰まる。**対処**: 生成物を**人力で加筆修正**（著作権主張の根拠化）／プロンプト・生成ログを保管／リリース時に正直に開示。
- **静止画→動く立ち絵**: Live2D 無料枠（パラメータ/パーツ30・ArtMesh100）で頭の動き・まばたき・口パク・髪揺れは可能。1体の習得込み数日〜1週間、中級で **10〜20時間/体**。
- **外注相場**: Fiverr 基本立ち絵 **$30〜100** / フルボディ商用 **$150〜400**、Live2D 化 $200〜1500。VRoid（無料 3D→2D 書き出し）も選択肢だが 3D 感が残る。

### D. Web（TS/React）→ Unity/C# 移植

- **フル書き直し前提**。Web 版は移行中**並走**させ比較基準にする。ハイブリッド（ReactUnity 等）は個人だと二重メンテで非推奨。
- **純ロジックの移植は機械的**: `(State, Action) => State` の純 reducer は C# に素直に移せる。Unity App UI に Redux 実装あり。純 C# クラスに分離すれば **Unity 無しで `dotnet test` 高速実行**も可能。**「ロジックと描画を分離してあると移植が楽」**＝本プロジェクトは検証台で既にこの分離が出来ている（`core/` が純ロジック）ので**移植適性が高い**。
- **アーキ対応**: React Context/useReducer → Unity（AppUI Redux Store か手書き reducer）。カード UI は **UGUI**（アニメ豊富）と UI Toolkit（データバインド）を使い分け。Chickensoft の **View/Logic/Data 3層**が React と自然対応。
- **最大リスクはスコープ膨張**。移行フェーズは**「Web 版と同等動作」だけ**に定義してロックする。

---

## 推奨方針（総合提案）

個人開発・低コスト・アニメ2.5D を踏まえた、私からの推し筋。

1. **ランタイム**: Unity 6（Personal・実費ゼロ）。**配布はデスクトップ（itch.io）を主**、WebGL は将来オプション（ロード問題が重いため後回し）。
2. **2.5D 表現**: まず**スプライト・パーツ差し替え**で最小に立ち絵を動かし、主要キャラだけ **Live2D 無料枠**で本格化（塗りを生かせてダーク FT 向き）。動きの派手さが要れば Spine を検討。
3. **art 制作**: **AI 生成（Illustrious / AnimagineXL、商用 OK の RAIL-M）+ 人力仕上げ**を主軸。キャラ一貫性は IP-Adapter→必要なら LoRA。**生成ログ保管・Steam 開示**を最初からルール化。「顔」になる主人公・ボスだけ **Fiverr 外注（$30〜400）**で底上げも可。
4. **移植順**: **検証済み戦闘コア（`core/` reducer + 数値）を最初に C# 移植**（機械的・headless テストで担保）→ カード/戦闘 UI（UGUI）→ art パイプラインを**1体だけ縦に通す**→ 全敵・全カードへ横展開。
5. **規律**: 移行は **Web パリティ（同等動作）に限定**。剣気・崩し等の新要素や 3D 化はこのフェーズに混ぜない（失敗の 7 割がスコープ膨張）。

---

## Steps（別セッションの実行ロードマップ）

### Phase 0 — 移行前の安全確認（小さく試す）

- [ ] 0a. **art パイプラインを Web 上で 1 体だけ縦に通す**（移行コストゼロで品質を体感）: AI でアニメ立ち絵を 1 体生成 → 人力仕上げ → Live2D 無料版で頭の動き/まばたきを付与 → 既存 Web（Pixi/prototype）に仮表示。これで「狙うリアル感」が出るか・制作1体の手間を実測。
- [ ] 0b. **Unity スパイク**: Unity 6 を入れ、空プロジェクトで「カード1枚を UGUI で表示→クリックでめくるアニメ」「Live2D サンプルを 1 体動かす」だけ作る。学習コストと手応えを測る。
- [ ] 0c. **0a/0b の結果で最終意思決定**: このまま Web で 2.5D 化に留めるか / Unity フル移行に踏み切るか（判断材料は本プラン Context 参照）。**移行する場合のみ Phase 1 以降へ**。

### Phase 1 — コア移植（ロジック先行・描画なし）

- [ ] 1. 新規 Unity プロジェクト作成（2D テンプレート、URP 任意）。リポジトリ/ブランチ戦略を決める（Web 版は並走保全）。
- [ ] 2. **戦闘コアを C# 移植**: `core/`（combat/cards/enemy/battleReducer/types/constants/viewModel）を純 C# クラスへ。`(State, Action) => State` を維持。数値は `constants` をそのまま。
- [ ] 3. **移植したコアのテスト**: 検証台の 47 件相当を Unity Test Framework（または headless `dotnet test`）へ移植し green。**Web 版と同一入力で同一出力**を突き合わせる（移植の正しさの証明）。

### Phase 2 — 戦闘 UI（Web パリティ）

- [ ] 4. 状態管理: AppUI Redux Store か手書き reducer ストアでコアを駆動。View/Logic/Data の 3 層構成。
- [ ] 5. 戦闘画面を UGUI で再現: 間合いトラック / 両者パネル（HP・スタミナ・疲労・ガード）/ 手札 / ログ / 結果。`viewModel` の導出をそのまま利用。
- [ ] 6. 入力・演出: カードプレイ/ターン終了/リスタート、被弾ダメージポップ・移動トゥイーン（カードアニメは UGUI Animator がボトルネックになりやすい点に留意）。

### Phase 3 — キャラ art パイプライン確立（1 体縦通し）

- [ ] 7. 主人公 or 看板ボス 1 体で **生成→仕上げ→Live2D リグ→Unity 表示**を最後まで通し、手順を `docs/` に文書化（再現可能な型を作る）。
- [ ] 8. art 規約を定義: 解像度・命名・ライセンス台帳（モデル/プロンプト/権利）・Steam 開示用メモのフォーマット。

### Phase 4 — 横展開

- [ ] 9. 全カード・全敵（既存 `imagePath` 設計を活用）へ art を横展開。優先度は「顔になるキャラ」から。
- [ ] 10. デプロイ: デスクトップ（itch.io）ビルド。WebGL は最適化前提で任意。

---

## Files（別セッションで作成。本セッションは本ドキュメントのみ）

| File                                                                    | Operation              | Notes                                  |
| ----------------------------------------------------------------------- | ---------------------- | -------------------------------------- |
| `.claude/docs/vision/plans/2026-06-28-unity-migration-character-art.md` | Create（本セッション） | 本ドキュメント                         |
| 新 Unity プロジェクト（別リポ or `/unity` サブツリー）                  | Create（別セッション） | Phase 1〜                              |
| `src/ui/battle-lab/core/` → C# 移植先                                   | Port                   | 純ロジックの引き継ぎ元（変更せず参照） |
| `.claude/docs/` 配下 art 規約・移植記録                                 | Create                 | Phase 3 で文書化                       |

---

## リスクと対策

| リスク                             | 対策                                                                                   |
| ---------------------------------- | -------------------------------------------------------------------------------------- |
| **スコープ膨張**（失敗の7割超）    | 移行を「Web パリティのみ」に固定。新戦闘要素・3D・課金は別フェーズへ追い出す           |
| 学習コスト（Unity 思考に1〜3ヶ月） | Phase 0 スパイクで早期に体感。コア移植（機械的・テスト有）から入り成功体験を先に作る   |
| WebGL が重い                       | デスクトップ配布を主とし WebGL は後回し                                                |
| AI art の著作権・開示              | 人力仕上げで保護根拠化／生成ログ・ライセンス台帳を最初から／Steam 等で正直に開示       |
| 「移行したが面白さが落ちる」       | コアは数値ごと移植し、Web 版と入出力一致を突き合わせて担保。Web 版を並走保全し随時比較 |
| そもそも移行が過剰投資の可能性     | Phase 0c で「Web のまま 2.5D 化」に引き返せる分岐を明示的に置く                        |

## コスト試算（低コスト経路）

- ソフト実費: Unity Personal ¥0 ＋ Live2D 無料枠 ¥0 ＋ AI（ローカル GPU）ほぼ ¥0。**MVP はソフト実費ほぼゼロが可能**。
- 任意の現金コスト: NovelAI 等のクラウド生成（月額小）／看板キャラの Fiverr 外注（$30〜400/体）／Live2D Pro エディタ（¥3,980/月、無料枠で足りなければ）。
- **本当のコストは時間**: 機能同等の移行で個人 6〜12 ヶ月＋習熟 1〜2 ヶ月。

## 未決事項（次セッション冒頭で決める）

1. Phase 0 の結果次第で **Web 維持 / Unity 移行**の最終確定。
2. 2.5D 手法の本命（**Live2D** 推奨だが、Spine / パーツ差し替えとの最終選択）。
3. art 制作の主軸（**AI+人力** 推奨だが、外注比率）。
4. 配布ターゲット（デスクトップ主・WebGL 後回しでよいか）。
5. Unity プロジェクトの置き場所（別リポか、現リポ内サブツリーか）。

---

## 出典（2026-06-28 調査）

- Unity Runtime Fee 撤廃: https://unity.com/blog/unity-is-canceling-the-runtime-fee ／ 価格: https://unity.com/products/pricing-updates ／ 2026 現況: https://www.strayspark.studio/blog/unity-engine-2026-state-comeback-runtime-fee-aftermath
- Live2D SDK ライセンス: https://www.live2d.com/en/sdk/license/ ／ 料金 2026: https://kudos.tv/blogs/stream-blog/live2d ／ コミッション相場: https://animarts.studio/blog/live2d-commission-cost-2026-complete-price-breakdown
- Spine 購入: https://esotericsoftware.com/spine-purchase ／ Live2D vs Spine: https://en.artner.jp/news/blogspinelive2d
- Unity 2D Animation vs Spine: https://retrostylegames.com/blog/unity-2d-animation-vs-spine/ ／ WebGL 性能: https://docs.unity3d.com/6000.4/Documentation/Manual/webgl-performance.html
- アニメ AI モデル比較: https://apatero.com/blog/best-ai-waifu-generators-consistent-anime-2026 ／ https://www.aiphotogenerator.net/blog/2026/02/best-stable-diffusion-models-2026
- CreativeML Open RAIL-M: https://huggingface.co/spaces/CompVis/stable-diffusion-license/raw/main/license.txt ／ NovelAI 規約: https://novelai.net/terms
- AI アセットの IP・開示: https://blog.promise.legal/ai-generated-assets-game-ip-disclosure/ ／ 商用利用: https://getimg.ai/blog/can-ai-generated-images-and-videos-be-used-commercially ／ 日本法 2026: https://globallawexperts.com/generative-ai-copyright-japan-2026/ ／ 文化庁: https://www.bunka.go.jp/english/policy/copyright/pdf/94055801_01.pdf
- VRoid Studio: https://vroid.com/en/studio ／ Fiverr アニメ料金: https://www.fiverr.com/resources/guides/costs/anime-illustrator
- Unity AppUI Redux: https://docs.unity3d.com/Packages/com.unity.dt.app-ui@0.3/manual/state-management.html ／ 外部高速テスト: https://gamedev.center/run-unity-tests-faster-dotnet/ ／ Chickensoft アーキ: https://chickensoft.games/blog/game-architecture ／ UGUI vs UI Toolkit: https://medium.com/@studio.angry.shark/unity-ui-toolkit-vs-ugui-2025-developer-guide-8407312c91ed ／ スコープ膨張統計: https://www.wayline.io/blog/scope-creep-indie-games-avoiding-development-hell

> **調査の確度**: 全 3 本とも confidence medium。Live2D の JPY しきい値・AI モデル個別ライセンス（版差）・個人移行工数の実測値は、実際に使う時点で公式ページ/モデルカードを再確認すること。

---

## 次セッション キックオフプロンプト（コピペ用）

```
Unity 移行 + 2.5D アニメキャラ art を進める。計画書は
.claude/docs/vision/plans/2026-06-28-unity-migration-character-art.md（これが実装契約）。

まず Phase 0（移行前の安全確認）から始める。本格移行の前に小さく試して引き返せる分岐を残す:
- 0a: art パイプラインを Web 上で 1 体だけ縦に通す（AI生成→人力仕上げ→Live2D無料版で
  頭の動き/まばたき→既存 prototype に仮表示）。狙うリアル感が出るか・1体の手間を実測。
- 0b: Unity 6 スパイク（カード1枚 UGUI でめくる + Live2D サンプル1体を動かす）。学習コスト実測。
- 0c: 0a/0b の結果で「Web のまま 2.5D 化」か「Unity フル移行」かを最終決定。移行する場合のみ Phase 1 へ。

厳守: スコープは「Web パリティ（同等動作）」に固定（新戦闘要素・3D・課金は混ぜない。
個人ゲーム失敗の7割がスコープ膨張）。検証済み戦闘コア（src/ui/battle-lab/core/ の純reducer
＋ constants 数値 ＋ 47テスト）と設計書は C# へ移植して引き継ぐ。AI art は商用OKモデル
（Illustrious/AnimagineXL, RAIL-M）+ 人力仕上げ、生成ログ保管・Steam開示を最初からルール化。
```

---

## 決定記録（Phase 0c 後に追記）

> 最終方針（Web 維持 / Unity 移行）: （未定 — Phase 0 スパイク後）
> 2.5D 手法の本命: （未定 — Live2D 推奨）
> art 主軸: （未定 — AI+人力 推奨）
