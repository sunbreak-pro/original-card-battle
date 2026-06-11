# Gap Analysis — Concept v2 vs 現実装・設計書群

> **Status**: DRAFT（ユーザーレビュー待ち）
> **Created**: 2026-06-11
> **入力**: `.claude/docs/vision/concept-v2.md`（APPROVED 2026-06-11）
> **手法**: 10領域並列調査 + 完全性/制約整合クリティーク2体（エージェント計12体・実コード/設計書を Read/Grep で実測）
> **次工程**: 本書レビュー → 要件定義（`docs/requirements/` Tier 1-3）→ 設計書更新 → 実装プラン分割

---

## 0. エグゼクティブサマリ

- ギャップ総数 **130件**（10領域）+ クリティークによる**横断ギャップ 8件**（§3）と**修正事項 15件**（§4）
- **最重要発見（TD-0）**: 現 main HEAD は `npx tsc --noEmit -p tsconfig.app.json` が **17エラーでコンパイル不能**（メインセッションで再実行し確認済み）。`BattleScreen.tsx` / `GuildBattleScreen.tsx` の `BattleCanvas` 二重 import と、両画面が import する `BattlePixiState` / `PixiEffectCommand` / `PIXI_EFFECT_TYPE` が `pixiTypes.ts` に存在しない。PixiJS Phase 1 マージ後の PR #8 / #1 マージで型定義が先祖返りした可能性が高い。**全ギャップ着手の前提を壊すため最優先返済**
- 併発の環境 regression: main の `.gitignore` が Python テンプレートに置き換わり `node_modules/` が無視されない（PR #12 報告済み、TD-0 と同時に返済推奨）
- 工数の重心は「戦闘コア3システム新設（BAT、L級5件）」「設計書群の改訂（DOC、25件）」「ステージ制移行（DUN）」。ただし設計書改訂は実装領域と二重起票されており、実態工数は見かけより小さい（§4-8）
- 不可侵コード（`deck.ts` / `deckReducer.ts`）に抵触する提案は**ゼロ**（制約整合クリティークがフィールドアクセスまで実測確認）

## 1. 集計表

| 領域                                | 件数      | 工数内訳          | 種別内訳                                  |
| ----------------------------------- | --------- | ----------------- | ----------------------------------------- |
| battle-core（戦闘コア）             | 15        | L×5 M×8 S×2       | new×4 modify×8 replace×1 keep×2           |
| cards-mastery（カード・熟練度）     | 11        | L×1 M×5 S×5       | new×1 modify×5 replace×2 keep×3           |
| enemy-ai-info（敵・AI・情報非開示） | 11        | L×2 M×7 S×2       | new×2 modify×6 replace×2 keep×1           |
| dungeon-structure（ダンジョン構造） | 14        | L×2 M×8 S×4       | new×4 modify×7 replace×1 remove×1 keep×1  |
| journal-knowledge（手記・知識）     | 11        | M×7 S×4           | new×2 modify×8 keep×1                     |
| camp-economy（キャンプ・経済）      | 11        | M×4 S×7           | new×1 modify×7 replace×1 keep×2           |
| irreversibility（不可逆性）         | 12        | L×1 M×6 S×5       | new×1 modify×9 replace×1 keep×1           |
| pixijs-integration（PixiJS 統合）   | 13        | L×2 M×6 S×5       | new×5 modify×4 replace×3 keep×1           |
| tech-debt（技術的負債）             | 7 (+TD-0) | M×2 S×5           | new×3 modify×3 remove×1                   |
| design-docs（設計書群）             | 25        | XL×1 L×3 M×12 S×9 | new×5 modify×11 replace×5 remove×2 keep×2 |
| **計**                              | **130**   |                   |                                           |

> 工数目安: S=半日以内 / M=1-2セッション / L=3-5セッション / XL=それ以上。
> 注意: §4 のオーナー統合を適用すると実効件数は 130 より 15〜20 件程度減る（重複起票の解消）。

## 2. 最優先: TD-0（新設・検証済み）

**TD-0: main のコンパイル不能解消** — `new` / `S` / **Phase 0 の先頭**

- **現状**: tsc 17エラー（メインセッションで実測再現済み）。内訳: `BattleScreen.tsx:3,18` と `GuildBattleScreen.tsx:7,17` の `BattleCanvas` 二重 import（TS2300）/ 両画面 + `PixiEffectBridge.ts` が参照する `BattlePixiState` `PixiEffectCommand` `PIXI_EFFECT_TYPE` が `pixiTypes.ts` に不在（TS2305。実体は `BattlePixiProps` のみ）/ `pixiFoundation.test.ts:3` が存在しない export `PixiEffectBridge` を参照
- **要求**: `npm run build` が通る状態の回復。呼び出し側（両画面）は既に新 API 前提のコードに書き換わっているため、**型定義側を追随させる修復**（PIX-3 の前段）が正攻法
- **影響**: `src/ui/pixi/types/pixiTypes.ts`, `src/ui/html/battleHtml/BattleScreen.tsx`, `src/ui/html/campsHtml/Guild/GuildBattleScreen.tsx`, `src/ui/pixi/battle/PixiEffectBridge.ts`, `src/ui/pixi/__tests__/pixiFoundation.test.ts`
- **併発**: `.gitignore` の Node 用復元（Python テンプレートに置換されている regression）も同一 Phase 0 コミット群で返済

## 3. 横断ギャップ（クリティークで追加・X 系）

どの領域にも起票されていなかったギャップ。完全性クリティークと制約整合クリティークの指摘を統合済み。

| ID  | タイトル                                                                                        | 工数 | 要点                                                                                                                                                                                                                                                                                                         |
| --- | ----------------------------------------------------------------------------------------------- | ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| X-1 | 魔術師の移行期間中の扱い + キャラ選択対応                                                       | M    | 剣士先行の帰結を明示決定: 選択不可 / 旧コア class-gate 並走 / 壊れたまま許容 のいずれか。`CharacterClassData.ts:159,179` の `isAvailable` と既存魔術師セーブの Continue 経路（`CharacterSelect.tsx:53-75`）の扱い、エネルギー置換時に魔術師カード40枚の cost が意味を失う問題を含む                          |
| X-2 | ギルド昇格試験（GuildBattleScreen / 試験敵5体）の新コア適合                                     | M    | `GuildBattleScreen.tsx:73` の `useBattle(1, [examEnemy])` depth=1 固定が DUN-3 で壊れる。`GuildEnemyData.ts` の試験敵5体が新属性付与の母集合から漏れ。B3 推奨等級（CAMP-5）は昇格等級が前提のため凍結不可・適合必須                                                                                          |
| X-3 | DOM 戦闘 HUD（スタミナゲージ・間合いトラック・体勢の常時表示 + 手札のプレイ不可フィードバック） | L    | BAT-2/5/6 は状態導入のみ、PIX-5/7 は Pixi 演出のみで、`PlayerFrame.tsx` / `BattleScreen.tsx` の DOM HUD が宙に浮いていた。モックアップ（`mockups/2026-06-11-battle-ui-mockup.html`）が設計の先行資産。「なぜ今使えないか」表示（範囲外グレーアウト / スタミナ不足）も含む。体験の柱1はこれが無いと成立しない |
| X-4 | 新コアの導入・学習手段（GAME_TIPS 全面改訂 + 初回導入の要否決定）                               | M    | `GameTipsData.ts:14-71` は旧コアルールの英語解説（「3 energy」「熟練度 +20/40/100%」— 後者は現実装とも乖離した誤記）。間合い・体勢・スタミナ・観察を学ぶ手段が皆無では合格基準2の「詰みなし」を満たせない                                                                                                    |
| X-5 | 画面横断のライト幻想化監査（テーマ色 + 英語ダーク文言）                                         | M    | テーマ色 SSOT `src/ui/css/core/variables.css` はダーク基調のまま。`CharacterSelect.tsx:146-148` の「journey into the abyss」、`DefeatScreen.tsx:61` の「GAME OVER」等が棚卸し対象外。UI text Japanese 規約違反の解消も同一パスで実施                                                                         |
| X-6 | 二重難易度の整理（既存 Difficulty vs B3 ステージ等級）                                          | S    | `LIVES_BY_DIFFICULTY`（`playerUtils.ts:15-26`）が選択 UI なしの宙吊り状態。(a) ライフ数の独立軸として残す / (b) ステージ等級に一本化 を決定。IRR-2 が依存するため先に確定                                                                                                                                    |
| X-7 | 合格基準の監査・検証手段の整備                                                                  | S-M  | 「設計書と実装の差分ゼロ」の監査チェック表 + 「2ステージ通しプレイ」の確認項目（クラッシュ・詰み・バランス崩壊の判定基準）を `docs/requirements/` 配下に新設。現状どのギャップも「何をどう確認したら合格か」を定義していない                                                                                 |
| X-8 | Phase 0（着手前負債返済）の境界確定                                                             | S    | 調査で新発見された未配線（CAMP-4 噂効果未適用 / CAMP-9 / TD-7 / JNL-3 / IRR-1 / IRR-6 / DUN-13）を Phase 0 に含めるか再設計に吸収するかの線引き。**推奨**: 旧仕様のまま直す価値があるのは TD-0/TD-1/TD-2/TD-7(b) のみ、残りは再設計ギャップに吸収と明文化                                                    |

## 4. クリティーク修正事項（オーナー統合・誤り訂正）

実装プラン化の前に適用する修正。**二重起票の統合は状態単一所有原則の防衛でもある**（別々に実装すると観察状態などの所有者が2箇所に生まれる）。

1. **TD-0 新設**（§2）。tech-debt 領域の負債リスト（001 / rollup / テスト）にも未計上だった
2. **PIX-3 の現状記述は旧い**: 呼び出し側は既に新 API 前提に書き換わっており、「新規拡張」ではなく「壊れた契約の修復」から始まる（TD-0 直後に実施）
3. **観察システムは journal-knowledge をオーナーに一本化**: ENM-3≈JNL-1（状態モデル）、ENM-4≈JNL-2（戦闘配線）、ENM-5≈JNL-8（EnemyFrame 段階開示・同一ファイル同一変更）、ENM-6≈JNL-3+4（図鑑）。観察レベル状態の所有は **JournalContext**。ENM 側は「観察レベル参照 API の消費者（表示判定）」に再定義
4. **噂の敵情報ヒント化は3領域で三重計上** (ENM-7≈JNL-6≈CAMP-3): 1ギャップに統合。購入・有効状態は **GuildContext**、敵知識への初期ヒント書き込みは **JournalContext の公開 API** 経由（Provider 階層上 Journal が Guild より上位、`App.tsx:112-115` で実装可能と確認済み）。実施順: CAMP-4 → 統合ギャップ → DUN-13
5. **回復経済（A2）は camp-economy をオーナーに一本化**: IRR-10 は CAMP-1/2/10 と同一ファイル群の重複。数値の確定は DOC-19（設計書）、実装は CAMP-1（宿の有料回復）/ CAMP-2（ポーション再価格）/ CAMP-10（rest ノード）に分割保持、IRR-10 は「A2 整合の監査」に縮小
6. **手記の永続化ライフサイクルは JNL-5 に統合**（IRR-5 のフルリセット論点を含む）。実施順: JNL-5 仕様確定 → 観察状態実装 → IRR-7 スキーマ確定
7. **敵プールのステージ化は ENM-8 に一本化**（DUN-8 は DUN-1 のデータ定義側に縮小。両者とも `enemyAI.ts:177-183` の同一ハードコードを触っていた）
8. **設計書改訂は DOC-\* を正本に片寄せ**: BAT-1≈DOC-2/3、CRD-9≈DOC-6、ENM-10≈DOC-13、DUN-14≈DOC-9-12、JNL-9≈DOC-15/16 が二重起票で、合計工数が実態の約2倍に見えていた。実装領域側は「DOC-x 完了が前提」の依存に縮小
9. **Depth 型移行ポリシーの一本化**: CRD-11 の「不可侵 shim が Depth を re-export」は誤り（実測で `deck.ts` / `deckReducer.ts` は Card 型の id/cardTypeId しかアクセスせず、不可侵制約は実質かからない）。**推奨**: CRD-11 の段階移行（マッピング層）を共通の第一歩とし、union 完全排除は DUN-3 の完了条件として直列化
10. **BAT-13 ⨯ TD-6 の正面衝突を解消**: BAT-13（オーケストレーターの renderHook 統合テスト新設）と TD-6（同モジュールを「書かない」negative list 指定）は両立しない。**一本化案**: BAT-13 を「現行戦闘の最小スモーク統合テスト1〜2本（初期化→カード実行→敵フェーズ→決着の通し）+ 生き残る純関数層」に再スコープし、網羅的な現行フロー回帰テストは書かない
11. **BAT-12（合成境界整理）は M→L に引き上げ**: 1006行のフックをテスト安全網ゼロで触ることになるため。BAT-13 再スコープ版のスモークテストを前提条件として依存に明記
12. **PIX-10 は $0 制約抵触リスク**: プラン書が中核指定する TexturePacker は有償。無料 OSS（free-tex-packer 等）または pixi.js v8 Assets API での個別テクスチャ遅延ロードへ差し替え
13. **試験敵5体（GuildEnemyData）の母集合漏れ**: ENM-1/ENM-9/DOC-13 のスコープに追加（X-2 と連動）
14. **CAMP-6 の GameTipsData は過小評価**: トーン調整でなく全文書き直しが必要 → X-4 へ分離
15. **IRR-7 の影響ファイル漏れ**: `CharacterSelect.tsx:91-133` が初期 SaveData リテラルをハードコード生成しており、SaveData v2.0 変更時に必ず追随。**CAMP-5 ⨯ DUN-6** の推奨等級表示はデータソースを StageDefinition（DUN-1）に単一定義し表示コンポーネントを共有（実施順: DUN-1 → DUN-6 → CAMP-5）

## 5. 実施順序（クリティカルパス）

手戻り防止のための依存順。**SaveData v2.0（IRR-7）は状態形が出揃う Phase 4 まで遅延させるのが最重要**（先にやるとスキーマ手戻り）。

```
Phase 0  負債返済（着手前・concept §5 決定）
  TD-0 コンパイル不能解消 + .gitignore 復元（最優先）
  → TD-1 (known-issue 001) / TD-2 (rollup) / TD-3-5
  → BAT-13 再スコープ版スモークテスト（X-8 の境界線で打ち止め）

Phase 1  設計書（設計書駆動: 実装に先行）
  DOC-1 core.md / concept-v2 統合 → DOC-25 要件 Tier 分け
  → DOC-2 新戦闘コア仕様書（間合い・体勢・スタミナの数値の正。
     BAT-2〜9 / CRD-1〜5 / ENM-1〜2 の全スキーマがここに依存）

Phase 2  戦闘コア（剣士先行）
  BAT-11 魔術師隔離 → BAT-12 合成境界整理（L、スモークテスト前提）
  → BAT-2 間合い / BAT-5 体勢 / BAT-6 スタミナ → BAT-3/4/7/9
  → X-3 DOM 戦闘 HUD（モックアップ準拠）

Phase 3  ステージ・データ
  DUN-1 StageDefinition（ENM-8 / CAMP-11 / JNL-7 / IRR-9 / DUN-6 / CAMP-5 が全依存）
  → CRD-3 剣士カード再定義 / ENM-9 敵再定義（+X-2 試験敵5体）
  → 観察システム（JNL 正本）/ 噂ヒント統合

Phase 4  セーブ・不可逆性
  IRR-7 SaveData v2.0（IRR-2/8/9・JNL-5・DUN-3・X-1 の決定が出揃ってから1回で設計）

Phase 5  演出・仕上げ
  PIX-6 スプライト移行 → PIX-5 位置移動演出 / PIX-7
  → X-4 学習手段 / X-5 ライト幻想化監査 / X-7 合格基準監査の実施
```

## 6. ユーザー判断が必要な論点（要件定義の入力）

| #   | 論点                                    | 選択肢                                                                        | 関連          |
| --- | --------------------------------------- | ----------------------------------------------------------------------------- | ------------- |
| 1   | 既存カードエネルギーとスタミナの関係    | 置換（スタミナに一本化）/ 二重コスト併存 / エネルギー廃止+スタミナ&剣気再設計 | BAT-6, CRD-2  |
| 2   | 魔術師の移行期間中の扱い                | キャラ選択から外す / 旧コアを class-gate で並走 / 壊れたまま許容              | X-1, BAT-11   |
| 3   | 手記はフルリセット（ライフ0）でも残すか | 残す（A1 を最大解釈）/ 消す（完全な死）/ 一部残す（観察Lvのみ等）             | JNL-5, IRR-5  |
| 4   | 既存 Difficulty（ライフ数）の処遇       | 独立軸として残す / ステージ等級に一本化                                       | X-6, IRR-2    |
| 5   | 昇格試験敵5体                           | 新コア適合（推奨: B3 等級が依存）/ 旧コア凍結                                 | X-2           |
| 6   | Depth 型の最終形                        | 段階移行→最終排除（推奨）/ 互換維持で残置                                     | CRD-11, DUN-3 |

---

## 7. 領域別ギャップ詳細（全130件）

> 以下は調査エージェントの構造化出力を機械整形したもの。`current` の file:line は調査時点の実測。§4 のオーナー統合適用前の生データであることに注意（統合先は §4 参照）。
### 7.1 戦闘コア（battle-core / 15件）

**現状サマリ**: 戦闘コアは useBattleOrchestrator（src/domain/battles/managements/useBattleOrchestrator.ts、実測1006行）が useBattleState / useBattlePhase / useCardExecution / executeCharacterManage / useSwordEnergy / useElementalChain を合成する構造。管理状態は HP/AP/Guard/エネルギー/バフのみ（useBattleState.ts:29-41）で、コンセプト主柱の「間合い・体勢・スタミナ」に当たる状態は src 全体に存在しない（grep 実測。"stance" は CardTag の分類名、"stagger" は行動不能デバフのみ）。行動順は速度差による連続フェーズ制（calculators/phaseCalculation.ts:69-158）、ダメージはバフ補正→クリ→防御→Guard→AP→HP 配分（calculators/damageCalculation.ts:39-145）で距離概念なし。カードエネルギーは毎フェーズ全回復（executeCharacterManage.ts:135）であり「重い行動ほど消費・戦闘終了でリセット」のスタミナとは別物、AP は装備耐久で戦闘間持ち越し（battle_logic.md §5.1）なのでこれも別物。魔術師の共鳴は orchestrator と cardExecution に直接埋め込まれ（useBattleOrchestrator.ts:230, 352-389 / useCardExecution.ts:564-620）、known-issue 001（resonance debuff lag）が未解決。設計書は battle_logic.md Ver4.0 / buff_debuff_system.md Ver5.0 で三要素を一切含まず、フック統合テストも 0 件（テストは純関数4ファイルのみ）。

#### BAT-1: 戦闘設計書の新コア版改訂（間合い・体勢・スタミナ仕様の先行定義） — `replace` / M

- **現状**: battle_logic.md は Ver 4.0 で、エネルギー/速度フェーズ制/ハイブリッド防御/バフのみを規定。間合い・体勢・スタミナの記述ゼロ（.claude/docs/battle_document/battle_logic.md:1-163）
- **要求**: concept §5 合格基準(1)「設計書と実装の差分ゼロ」+ §7 手順4「設計書更新が実装に先行」。距離段階・移動コスト・体勢ゲージ・スタミナ消費表など三要素の数値仕様を新設計書として先に確定する
- **影響ファイル**: `.claude/docs/battle_document/battle_logic.md`, `.claude/docs/battle_document/buff_debuff_system.md`
- **設計書**: .claude/docs/battle_document/battle_logic.md（Ver 5.0 へ全面改訂） / .claude/docs/battle_document/range_stance_stamina_spec.md（新規）
- **リスク**: 設計書を後追いにすると合格基準(1)が構造的に満たせなくなる。必ず実装前に確定させる

#### BAT-2: 間合い（距離）状態の導入 — `new` / M

- **現状**: 距離状態が存在しない。PlayerState は hp/ap/guard/energy/buffs のみ（src/domain/battles/managements/useBattleState.ts:29-41）、EnemyBattleState 生成にも距離なし（src/domain/battles/logic/enemyStateLogic.ts）。grep で src/domain/battles に distance/range 該当ゼロ
- **要求**: concept §1 主柱「間合い」・§2 体験の柱1「間合いの読み合い」。プレイヤー×敵ごとの距離状態と初期距離・更新 API を追加。複数敵対応のため EnemyBattleState 側に距離を持たせるのが状態単一所有原則に整合
- **影響ファイル**: `src/types/characterTypes.ts`, `src/types/battleTypes.ts`, `src/domain/battles/managements/useBattleState.ts`, `src/domain/battles/logic/enemyStateLogic.ts`, `src/constants/battleConstants.ts`
- **設計書**: .claude/docs/battle_document/range_stance_stamina_spec.md（新規）

#### BAT-3: カード型への間合い属性追加とプレイ可否判定の統合 — `modify` / M

- **現状**: Card 型に間合い概念なし（src/types/cardTypes.ts:41-140）。canPlayCard はエネルギー・フェーズ・剣気のみ判定（src/domain/battles/managements/useCardExecution.ts:203-208、src/domain/cards/state/card.ts の canPlayCardCheck）
- **要求**: concept §1「カード＝実際に取れる行動」。カードに有効間合い（validRange）と距離変更効果（接近/離脱）を定義し、canPlayCard / getCardEffectPreview（useCardExecution.ts:214-279）に間合い判定を組み込む
- **影響ファイル**: `src/types/cardTypes.ts`, `src/domain/cards/state/card.ts`, `src/domain/battles/managements/useCardExecution.ts`, `src/constants/data/cards/swordsmanCards.ts`
- **設計書**: .claude/docs/battle_document/range_stance_stamina_spec.md（新規） / .claude/docs/card_document/（カード領域と数値整合）

#### BAT-4: カード実行・ダメージ計算への距離反映 — `modify` / M

- **現状**: calculateDamage はバフ補正・クリティカル・貫通のみで位置関係を見ない（src/domain/battles/calculators/damageCalculation.ts:39-89）。executeCard も距離変更を扱わない（src/domain/battles/managements/useCardExecution.ts:285-768）
- **要求**: concept §1 主柱「間合いの駆け引き」。距離適性によるダメージ補正（最適間合いボーナス/不適ペナルティ）と、カードプレイ時の距離変更実行（executeCard 内で距離 setter 呼び出し）を追加
- **影響ファイル**: `src/domain/battles/calculators/damageCalculation.ts`, `src/domain/battles/managements/useCardExecution.ts`, `src/types/battleTypes.ts`
- **設計書**: .claude/docs/battle_document/battle_logic.md（ダメージ計算フロー §6 改訂）

#### BAT-5: 体勢（スタンス/崩し）システムの新設 — `new` / L

- **現状**: 体勢状態は存在しない。近い概念は行動不能デバフ stagger（src/constants/data/battles/buffData.ts:74「よろめき」）と CardTag の stance（src/types/cardTypes.ts:27、半永続効果カードの分類名）のみで、駆け引き対象のゲージではない
- **要求**: concept §1 主柱「体勢」・§3 B1「環境が体勢に影響」。体勢値（または段階）を PlayerState/EnemyBattleState に持ち、攻撃・ガード・移動で増減、崩れたら隙（被ダメ増/行動不能）が生じるロジックを新設。崩し成立の結果として既存 stagger を付与する等、既存デバフとの接続設計を含む
- **影響ファイル**: `src/domain/battles/managements/useBattleState.ts`, `src/types/characterTypes.ts`, `src/types/battleTypes.ts`, `src/domain/battles/logic/postureLogic.ts（新規）`, `src/domain/battles/calculators/damageCalculation.ts`, `src/constants/data/battles/buffData.ts`
- **設計書**: .claude/docs/battle_document/range_stance_stamina_spec.md（新規）
- **リスク**: stagger デバフと役割が重複したまま実装すると行動不能が二重判定になる。導入時に canAct（buffCalculation.ts:158-162）との統合を必須にする

#### BAT-6: スタミナシステムの新設と既存エネルギーの再定義 — `new` / L

- **現状**: 行動リソースはカードエネルギーのみで毎フェーズ maxEnergy に全回復（src/domain/battles/managements/executeCharacterManage.ts:135、useBattleState.ts:185-190）。「重い行動ほど消費・ターンを跨いで持続・戦闘終了でリセット」のプールは存在しない。AP は装備耐久で戦闘間持ち越し（battle_logic.md §5.1、onApDamage 経由で装備に分配 useBattleOrchestrator.ts:113-116）でありスタミナとは別概念
- **要求**: concept §1 優先1内包「スタミナ＝戦闘内リソース。重い行動ほど消費、戦闘終了でリセット」。スタミナ状態+消費/回復ロジックを追加し、既存ターン制エネルギーとの関係（置換 or 二重コスト）を要件定義で確定して実装。AP（装備耐久）は不可逆性の資産として独立維持
- **影響ファイル**: `src/domain/battles/managements/useBattleState.ts`, `src/types/cardTypes.ts`, `src/domain/battles/managements/useCardExecution.ts`, `src/domain/battles/managements/executeCharacterManage.ts`, `src/constants/battleConstants.ts`
- **設計書**: .claude/docs/battle_document/range_stance_stamina_spec.md（新規） / .claude/docs/battle_document/battle_logic.md（§2 エネルギーシステム改訂）
- **リスク**: エネルギー・スタミナ・剣気の3リソース併存は認知負荷が高い。置換か統合かの設計判断を実装前に固めないと手戻りが大きい

#### BAT-7: 剣気システムの新コア適合（スタミナとの役割分担） — `modify` / M

- **現状**: 剣気は 0-10 蓄積で flat ダメージ加算+出血確率、ターン開始+1、戦闘間リセット（src/domain/characters/player/logic/swordEnergySystem.ts:1-120、useBattleOrchestrator.ts:850 resetForNextEnemy）。「行動の重さ」とは無関係に蓄積する
- **要求**: concept §4「まず剣士のみで新戦闘コアを再設計・完成」。剣気は流用資産としつつ、スタミナ（消耗・コスト側）と剣気（攻勢の蓄積・リターン側）の役割を重複なく再定義し、間合い・体勢との相互作用（例: 崩し成立時の剣気ボーナス）を新設計書に従い実装
- **影響ファイル**: `src/domain/characters/player/logic/swordEnergySystem.ts`, `src/domain/battles/managements/useClassAbility.ts`, `src/domain/battles/managements/useCardExecution.ts`
- **設計書**: .claude/docs/battle_document/range_stance_stamina_spec.md（新規）

#### BAT-8: フェーズキューへの環境コンテキスト導入 — `modify` / M

- **現状**: フェーズ順序は速度差のみで決定（src/domain/battles/calculators/phaseCalculation.ts:69-76 連続フェーズ閾値、115-158 キュー生成）。useBattlePhase は速度乱数+平均回帰のみ管理（useBattlePhase.ts:82-111）。ステージ環境の入力経路が存在しない
- **要求**: concept §3 B1「環境（視界・足場・広さ）が間合い・体勢に影響」。useBattleOrchestrator の引数（useBattleOrchestrator.ts:118-124）に BattleEnvironment（環境修飾子）を追加し、初期距離・移動コスト・速度補正等へ適用する入り口を作る。ステージデータ自体の定義はダンジョン領域の責務
- **影響ファイル**: `src/domain/battles/managements/useBattleOrchestrator.ts`, `src/domain/battles/managements/useBattlePhase.ts`, `src/domain/battles/calculators/phaseCalculation.ts`, `src/types/battleTypes.ts`
- **設計書**: .claude/docs/battle_document/battle_logic.md / .claude/docs/danjeon_document/（ステージ側と整合）

#### BAT-9: 敵フェーズ実行の間合い・体勢・スタミナ対応 — `modify` / L

- **現状**: EnemyAction は baseDamage/hitCount/guardGain 等のみで間合い要件なし（src/types/characterTypes.ts:143-156）。executeEnemyPhase は距離・体勢と無関係に攻撃を適用（src/domain/battles/managements/executeCharacterManage.ts:172-314、execution/enemyPhaseExecution.ts の calculateEnemyAttackDamage）
- **要求**: concept §1「戦闘が間合い・体勢・スタミナを持つ駆け引き」は敵側も同一ルールが前提。EnemyAction に間合い要件・移動行動を追加し、敵フェーズ実行パイプラインで距離判定・距離変更・体勢への作用を処理する。AI の行動選択ロジック改修は enemy 領域の責務
- **影響ファイル**: `src/types/characterTypes.ts`, `src/domain/battles/managements/executeCharacterManage.ts`, `src/domain/battles/execution/enemyPhaseExecution.ts`
- **設計書**: .claude/docs/battle_document/range_stance_stamina_spec.md（新規） / .claude/docs/enemy_document/（敵領域と整合）

#### BAT-10: バフ・デバフ体系の新コア棚卸し — `modify` / M

- **現状**: 42種のバフ・デバフ（src/types/battleTypes.ts:21-67、src/constants/data/battles/buffData.ts）。速度系 haste/slow/stall がフェーズキューに影響（calculators/speedCalculation.ts:26-49）、行動不能系 stun/freeze/stagger は canAct で判定（calculators/buffCalculation.ts:158-162）。間合い・体勢・スタミナと干渉する系統が未整理
- **要求**: 主柱導入に伴い、stagger↔体勢崩し、slow/stall↔スタミナ消耗、momentum/focus 等の役割を再マッピングし、各バフの維持/再定義/凍結を確定する。buff_debuff_system.md Ver 6.0 への改訂とセットで実施（concept §5 合格基準1）
- **影響ファイル**: `src/constants/data/battles/buffData.ts`, `src/types/battleTypes.ts`, `src/domain/battles/calculators/buffCalculation.ts`, `src/domain/battles/logic/buffLogic.ts`
- **設計書**: .claude/docs/battle_document/buff_debuff_system.md（Ver 6.0 改訂）

#### BAT-11: 魔術師コード（useElementalChain/共鳴）のプラグイン隔離 — `modify` / L

- **現状**: orchestrator がクラス不問で useElementalChain を呼び（src/domain/battles/managements/useBattleOrchestrator.ts:230）、共鳴のダメージ補正・効果取得を cardSetters に直接埋め込み（同 352-389）、useCardExecution に共鳴効果ブロックがハードコード（src/domain/battles/managements/useCardExecution.ts:564-620）。known-issue 001（resonance debuff の 1-card-lag、.claude/docs/known-issues/001-resonance-debuff-card-lag.md）も未解決
- **要求**: concept §4「まず剣士のみで新戦闘コアを完成、魔術師（属性共鳴）は後追い適合」。ClassAbilitySystem インターフェース（src/domain/characters/player/classAbility/classAbilitySystem.ts）経由の差し替え可能な構造に寄せ、mage 固有分岐を共通実行路から除去。known-issue 001 は concept §5 の負債返済として隔離前に解消
- **影響ファイル**: `src/domain/battles/managements/useBattleOrchestrator.ts`, `src/domain/battles/managements/useCardExecution.ts`, `src/domain/battles/managements/useElementalChain.ts`, `src/domain/battles/managements/useClassAbility.ts`, `src/domain/characters/player/classAbility/classAbilitySystem.ts`
- **設計書**: .claude/docs/battle_document/element_system_spec.md（後追い適合方針を追記）
- **リスク**: React hooks のルール上 useElementalChain の呼び出し自体は無条件のまま残る。隔離は「呼び出し」ではなく「実行路への注入点」を抽象化する設計にする

#### BAT-12: useBattleOrchestrator の合成境界整理（再設計前リファクタ） — `modify` / M

- **現状**: useBattleOrchestrator は実測 1006 行（CLAUDE.md 記載の ~877 行から肥大）で、状態合成・フェーズ進行・アニメ制御・後方互換 return を一手に持つ（src/domain/battles/managements/useBattleOrchestrator.ts:118-1006）
- **要求**: concept §4 方針B「コア再設計・資産流用」の土台として、新規3システム用フック（useRange/usePosture/useStamina 等）を差し込める合成境界を先に整理する。状態は各フックが単一所有し orchestrator は配線に徹する形へ
- **影響ファイル**: `src/domain/battles/managements/useBattleOrchestrator.ts`, `src/domain/battles/managements/battleFlowManage.ts`

#### BAT-13: 戦闘フック統合テストの整備（着手前の負債返済） — `new` / L

- **現状**: src/domain/battles のテストは純関数4ファイルのみ（logic/__tests__/buffLogic.test.ts、phaseLogic.test.ts、bleedDamage.test.ts、calculators/__tests__/damageCalculation.test.ts）。useBattleOrchestrator / useBattleState / useCardExecution を対象にしたテストは 0 件（grep 実測）
- **要求**: concept §5「技術的負債: フック統合テスト不足を再設計着手前に解消」。renderHook ベースで現行戦闘フロー（初期化→プレイヤーフェーズ→カード実行→敵フェーズ→決着）の回帰テストを先に張り、コア再設計の安全網にする
- **影響ファイル**: `src/domain/battles/managements/__tests__/（新規ディレクトリ）`, `src/domain/battles/execution/__tests__/（新規）`
- **リスク**: アニメーション待ち（setTimeout 多用、useCardExecution.ts:442 等）が統合テストを不安定にする。fake timers 前提の設計が必要

#### BAT-14: ハイブリッド防御と AP 持ち越しの維持 — `keep` / S

- **現状**: Guard→AP→HP 配分とアーマーブレイク（src/domain/battles/calculators/damageCalculation.ts:91-145）、AP=装備耐久の戦闘間持ち越し（battle_logic.md §5.1、onApDamage で装備耐久へ分配 useBattleOrchestrator.ts:113-116）が実装済み
- **要求**: concept §1 副柱「結果は不可逆に残る」に既に合致（装備消耗の不可逆性）。新コアでもそのまま流用する。間合い補正（BAT-4）は配分の前段に入るため applyDamageAllocation 自体は不変
- **影響ファイル**: `src/domain/battles/calculators/damageCalculation.ts`
- **設計書**: .claude/docs/battle_document/battle_logic.md（§5 は維持と明記）

#### BAT-15: AoE コードパスの凍結維持 — `keep` / S

- **現状**: targetAll カードの AoE 実行パスが実装済み（src/domain/battles/managements/useCardExecution.ts:370-449、cardSetters の getAliveEnemies/updateEnemyByIndex useBattleOrchestrator.ts:391-437）
- **要求**: concept §5「AoE は凍結（廃棄しない）」。新コア対応（距離×AoE の交差仕様）は行わず、コードはそのまま維持。新カードデータで targetAll を使わない運用とし、再設計書に凍結対象と明記
- **影響ファイル**: `src/domain/battles/managements/useCardExecution.ts`
- **設計書**: .claude/docs/battle_document/battle_logic.md（凍結対象リストに追記）

### 7.2 カード・熟練度（cards-mastery / 11件）

**現状サマリ**: カード型は src/types/cardTypes.ts:41-131 の単一 Card インターフェースで、コストはエナジー(cost)のみ。間合い・体勢・スタミナに相当するフィールドは存在しない（CardTag の "stance" は半永続バフの意味で体勢とは別物）。剣士カードは src/constants/data/cards/swordsmanCards.ts に60枚（基本37+タレント8+派生15）、魔術師は mageCards.ts に40枚。熟練度は src/domain/cards/state/masteryManager.ts が cardTypeId→useCount の純関数 Map で管理し、派生は cardDerivationRegistry.ts（15件）、タレントは talentCardRegistry.ts（8件）の ID ベースのデータ駆動レジストリで、いずれもカードの中身（効果）に依存しない構造のため新戦闘コアへそのまま流用可能。不可侵の deck.ts / deckReducter.ts は shim（src/domain/cards/type/cardType.ts）経由で Card 型のみを import し、フィールドアクセスは id/cardTypeId だけ（deck.ts:93-99）なので、optional フィールド追加による型拡張は不可侵制約に抵触しない。一方、設計書 .claude/docs/card_document/SWORDSMAN_CARDS_40.md は実装と既に大幅乖離しており（設計書では攻撃カードが剣気+1獲得、実装では剣気消費）、コンセプトの合格基準「設計書と実装の差分ゼロ」に向け刷新が必須。

#### CRD-1: Card 型へのリアル戦闘属性追加（スタミナコスト・間合い条件・間合い変化・体勢影響） — `modify` / M

- **現状**: src/types/cardTypes.ts:41-131 の Card インターフェースはコストがエナジー(cost: number, 57行)のみで、スタミナ・間合い（近/中/遠）・体勢に関するフィールドが一切ない。CardTag の "stance"（cardTypes.ts:22-27）は「半永続バフカード」の意味で、コンセプトの体勢（戦闘状態）とは別概念
- **要求**: concept-v2 §1 主柱「戦闘が間合い・体勢・スタミナを持つ駆け引き」「カード＝実際に取れる行動」「重い行動ほどスタミナ消費」に由来。カード型に staminaCost / 使用可能間合い / 使用後の間合い変化 / 体勢への影響を optional フィールドとして追加する必要がある
- **影響ファイル**: `src/types/cardTypes.ts`, `src/domain/cards/type/cardType.ts`, `src/constants/cardConstants.ts`, `src/ui/html/cardHtml/CardComponent.tsx`, `src/ui/html/cardHtml/CardModalDisplay.tsx`
- **設計書**: .claude/docs/card_document/CARD_SCHEMA_REALISM.md（新規: 新カードスキーマ仕様） / .claude/docs/battle_document/battle_logic.md
- **リスク**: エナジー(cost)・剣気(swordEnergy)・スタミナの3リソース併存は複雑化の懸念。cost をスタミナに置き換えるか併存させるかは新戦闘設計書で先に決める必要がある。shim（cardType.ts:5-9）の既存 re-export は不可侵ファイルの import 元なので削除・改名不可、追加のみ可

#### CRD-2: プレイ可否判定（canPlayCard / cardPlayLogic）のスタミナ・間合い・体勢チェック拡張 — `modify` / M

- **現状**: src/domain/cards/state/card.ts:34-53 の canPlayCard はエナジー残量と剣気残量のみ判定。src/domain/cards/state/cardPlayLogic.ts:20-63 も剣気の消費/獲得処理のみ
- **要求**: concept-v2 §1 主柱に由来。スタミナ残量チェック、現在の間合いとカードの間合い条件の照合、体勢条件の判定を追加し、「今この距離・この体勢・この疲労度で取れる行動か」をカード単位で判定できるようにする
- **影響ファイル**: `src/domain/cards/state/card.ts`, `src/domain/cards/state/cardPlayLogic.ts`, `src/domain/battles/managements/useCardExecution.ts`
- **設計書**: .claude/docs/card_document/CARD_SCHEMA_REALISM.md（新規） / .claude/docs/battle_document/battle_logic.md
- **リスク**: canPlayCard のシグネチャ変更は呼び出し側（battle 領域の useCardExecution / BattleScreen）に波及する。battle 領域のスタミナ/間合い状態管理の設計が先行依存

#### CRD-3: 剣士カード全60枚の再定義（スタミナ・間合い・体勢値の付与と再バランス） — `replace` / L

- **現状**: src/constants/data/cards/swordsmanCards.ts:3-1050 に60枚（基本カード37枚 sw_001〜026/035/037/039〜047、タレント8枚 sw_027〜034、派生15枚 *_d）。全カードがエナジーコスト+剣気経済（攻撃=剣気消費、スキル/ガード=剣気獲得）前提で設計されており、間合い・体勢・スタミナの概念がゼロ。sw_034d は baseDamage 400 など倍率インフレ型の数値設計
- **要求**: concept-v2 §1「カード＝実際に取れる行動」+ §4「まず剣士のみで新戦闘コアを完成」に由来。全60枚にスタミナコスト・使用可能間合い・体勢影響を設定し、新戦闘コアの「実際に取れる行動」として意味づけと数値を再設計する
- **影響ファイル**: `src/constants/data/cards/swordsmanCards.ts`
- **設計書**: .claude/docs/card_document/SWORDSMAN_CARDS_40.md（置き換え: 新剣士カード一覧設計書）
- **リスク**: 60枚同時のバランス設計は手戻りが大きい。初期デッキ15枚分（CRD-5）を先に完成→通し検証→残りを段階投入する分割を推奨。CRD-9 の新設計書が先行依存（設計書駆動）

#### CRD-4: 間合い操作・体勢操作カードの新設（接近/後退/構え直し等） — `new` / M

- **現状**: swordsmanCards.ts の全60枚に移動・距離操作・体勢立て直しに相当するカードが存在しない（grep でも該当フィールド・効果なし）。距離を変える手段が存在しないため「間合いの読み合い」が成立しない
- **要求**: concept-v2 体験の柱1「間合いの読み合い — 距離・体勢・スタミナを踏まえた手札の駆け引き」に由来。間合いを詰める/離す/体勢を立て直すカードがプレイヤーの能動的な選択肢として必要
- **影響ファイル**: `src/constants/data/cards/swordsmanCards.ts`, `src/constants/data/battles/initialDeckConfig.ts`, `src/constants/cardConstants.ts`
- **設計書**: .claude/docs/card_document/SWORDSMAN_CARDS_40.md（置き換え版に含める）
- **リスク**: 新カードカテゴリ（移動系）には CardTag の追加（例: "move"）が必要になる可能性があり、CARD_TAG_LABEL_MAP / COLOR_MAP（cardConstants.ts:115-128）と UI 表示にも波及

#### CRD-5: 剣士初期デッキ15枚の再構成 — `modify` / S

- **現状**: src/constants/data/battles/initialDeckConfig.ts:13-26 の剣士初期デッキは攻撃6+スキル6+ガード3の15枚で、剣気の獲得→消費サイクル前提の構成。間合い・体勢・スタミナの駆け引きを学べる構成ではない
- **要求**: concept-v2 §1 主柱 + 体験の柱1 に由来。初期デッキだけで「間合いを取る→隙を作る→重い一撃」の基本ループが成立する構成（移動カード・軽攻撃・重攻撃・体勢ケアのバランス）に変更する
- **影響ファイル**: `src/constants/data/battles/initialDeckConfig.ts`, `src/constants/data/characters/CharacterClassData.ts`
- **設計書**: .claude/docs/card_document/SWORDSMAN_CARDS_40.md（置き換え版に初期デッキ構成を明記）
- **リスク**: CRD-3/CRD-4 のカード定義が先行依存。初期デッキは新戦闘コアの最初の通し検証単位になるため、ここを最優先で固めると手戻りが減る

#### CRD-6: 熟練度コア（masteryManager / cardMasteryStore）の流用確認と回帰テスト — `keep` / S

- **現状**: src/domain/cards/state/masteryManager.ts:3-59 は cardTypeId→useCount の純関数 Map で、カードの効果内容に一切依存しない。永続化は PlayerContext.tsx:94-95 の cardMasteryStore。バトル中の同期は syncDeckMastery（masteryManager.ts:48-59）、加算は usePlayerBattle.ts:25 経由
- **要求**: concept-v2 体験の柱3「成長の手応え — 既存の熟練度→派生システムを活用」に由来。構造はそのまま流用可能（カード ID が維持される限り無改修で動く）
- **影響ファイル**: `src/domain/cards/state/masteryManager.ts`, `src/contexts/PlayerContext.tsx`, `src/domain/characters/player/hooks/usePlayerBattle.ts`
- **設計書**: .claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md
- **リスク**: loadFromSaveData（PlayerContext.tsx:406-414）はデッキを starterDeck から再構築しており、熟練度のセーブ復元が不完全な可能性。A1「手記は死を越える」（知識・成長の永続）との整合のため、熟練度のラン間/死亡時の永続仕様を新設計書で明文化し検証が必要

#### CRD-7: 派生・タレントレジストリの構造維持と中身の再バランス — `keep` / S

- **現状**: src/constants/data/cards/cardDerivationRegistry.ts:30-50（派生15エントリ）と talentCardRegistry.ts:24-35（タレント8エントリ）は parentCardTypeId→derivedCardTypeId+requiredMastery の ID ベースデータ駆動。アンロック判定は src/domain/cards/logic/cardDerivation.ts / talentCardUnlock.ts / useDerivationCheck.ts で完結
- **要求**: concept-v2 体験の柱3 に由来。レジストリ構造とアンロックロジックは無改修で流用可（gapType は構造=keep）。ただし CRD-3 のカード再定義後、派生先カードの効果値とアンロック閾値（Lv1〜4）を新バランスで見直す
- **影響ファイル**: `src/constants/data/cards/cardDerivationRegistry.ts`, `src/constants/data/cards/talentCardRegistry.ts`, `src/domain/cards/logic/cardDerivation.ts`, `src/domain/cards/logic/talentCardUnlock.ts`
- **設計書**: .claude/docs/card_document/SWORDSMAN_CARDS_40.md（置き換え版に派生・タレント表を統合）
- **リスク**: カード ID（sw_xxx）を再定義時に変えるとレジストリ全体が壊れる。ID は維持し効果のみ差し替える方針を推奨

#### CRD-8: 熟練度ボーナスの意味再定義（単純ダメージ倍率からリアル性整合の上達表現へ） — `modify` / M

- **現状**: src/constants/cardConstants.ts:35-41 の MASTERY_BONUSES は Lv0:1.0x→Lv4:2.5x の単純ダメージ倍率で、src/domain/cards/state/card.ts:4-10 calculateEffectivePower が baseDamage に乗算（gemLevel×0.5 も加算）
- **要求**: concept-v2 §1 主柱と体験の柱3 の整合に由来。倍率インフレは「生身の探索者・一手のミスが命取り」のコアファンタジーと衝突し得るため、上達=スタミナ効率向上・体勢崩れにくさ等の「技術の積み上げ」表現への再定義を検討する（数値は新設計書で確定）
- **影響ファイル**: `src/constants/cardConstants.ts`, `src/domain/cards/state/card.ts`
- **設計書**: .claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md / .claude/docs/battle_document/battle_logic.md
- **リスク**: MASTERY_BONUSES は shim（cardType.ts:8）経由で不可侵ファイル互換のため export 維持が必要（中身の値変更は可、削除は不可）。戦闘ダメージ計算（battle 領域 damageCalculation）と二重管理にならないよう責務境界を明確化

#### CRD-9: card_document 設計書群の刷新（新スキーマ+剣士カード一覧を先行作成） — `replace` / M

- **現状**: 設計書と実装が既に大幅乖離: SWORDSMAN_CARDS_40.md:45-92 では攻撃カードが剣気+1を「獲得」する設計だが、実装 swordsmanCards.ts:11 等では攻撃カードが剣気を「消費」する真逆の仕様。威力数値も不一致（設計書 Power 5 vs 実装 baseDamage 8 等）。NEW_CHARACTER_SYSTEM_DESIGN.md:36-56 には凍結対象の Title System 記載が残存
- **要求**: concept-v2 §5 合格基準「設計書と実装の差分ゼロ（新設計書を先に更新し、監査で差分ゼロ確認）」に由来。新カードスキーマ仕様書と剣士カード全量の新設計書を実装に先行して作成する（設計書駆動）
- **影響ファイル**: `.claude/docs/card_document/SWORDSMAN_CARDS_40.md`, `.claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md`
- **設計書**: .claude/docs/card_document/CARD_SCHEMA_REALISM.md（新規） / .claude/docs/card_document/SWORDSMAN_CARDS_40.md（置き換え） / .claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md（更新）
- **リスク**: CRD-1〜5 全ての先行依存。ここを飛ばして実装に入ると合格基準(1)を満たせない。Title System 等の凍結項目は設計書から分離（削除でなくバックログへ移動）

#### CRD-10: 魔術師カード40枚の凍結維持と互換規約の策定 — `keep` / S

- **現状**: src/constants/data/cards/mageCards.ts に40枚（666行）。剣気に相当する属性共鳴（elementalChainBonus, cardTypes.ts:118）前提で、派生レジストリは空（cardDerivationRegistry.ts:57-60）、タレントも空（talentCardRegistry.ts:41）
- **要求**: concept-v2 §4「魔術師（属性共鳴）は後追いで適合」に由来。第一段階では再定義せず凍結維持。ただし新フィールド（staminaCost 等）未定義のカードでも戦闘が起動できるデフォルト値規約（例: staminaCost 未定義=0、間合い未定義=全間合い可）を CRD-1 のスキーマ仕様に含める
- **影響ファイル**: `src/constants/data/cards/mageCards.ts`
- **設計書**: .claude/docs/card_document/CARD_SCHEMA_REALISM.md（新規: 互換デフォルト規約を明記） / .claude/docs/card_document/MAGE_CARDS_40.md
- **リスク**: 新プレイ可否判定（CRD-2）がスタミナ/間合いを必須化すると魔術師選択時にプレイ不能になる。キャラ選択画面で魔術師を一時的に無効化するか、互換デフォルトで動かすかの判断が必要（battle/UI 領域と要調整）

#### CRD-11: カード型内の Depth 依存定数のステージ制移行対応 — `modify` / S

- **現状**: src/types/cardTypes.ts:17-18 の Depth = 1|2|3|4|5 と DepthCurveType、src/constants/cardConstants.ts:17-23 の MAGIC_MULTIPLIERS（Depth キー）は「1ダンジョン5深度」前提。shim（src/domain/cards/type/cardType.ts:5,8）が不可侵ファイル互換のため Depth / MAGIC_MULTIPLIERS を re-export している
- **要求**: concept-v2 §3 B1「複数ダンジョン（ステージ）×テーマ別の複数階層」+ B3「難易度=歪みの濃さ（推奨等級）」に由来。Depth 型を直接削除せず、ステージ/等級→既存 Depth 値へのマッピング層を設けるか、新等級型を別途定義して段階移行する
- **影響ファイル**: `src/types/cardTypes.ts`, `src/constants/cardConstants.ts`, `src/domain/cards/type/cardType.ts`
- **設計書**: .claude/docs/card_document/CARD_SCHEMA_REALISM.md（新規）
- **リスク**: Depth 型は shim 経由で不可侵ファイルの import 互換に含まれるため削除・改名不可（export 維持必須）。dungeon 領域のステージ再設計と境界が重なるため、型の所有権（どちらの領域が新等級型を定義するか）を実装プラン側で調整

### 7.3 敵・AI・情報非開示（enemy-ai-info / 11件）

**現状サマリ**: 敵データは src/constants/data/characters/enemy/enemyDepth1.ts〜enemyDepth5.ts に深度別 8 体 × 5 深度 = 40 体（各深度: 通常 7 + ボス 1。タスク記載の50体ではなく実数は40体）が定義され、single/double/three/boss の遭遇パターン（DepthEnemyData）で構成される。AI は src/domain/characters/enemy/logic/enemyAI.ts:102-142 の determineEnemyAction が hp/maxHp/ターン番号/確率のみで行動を選び、間合い・体勢・スタミナの概念は型（src/types/characterTypes.ts:143-193 の EnemyAction/EnemyDefinition）にも判断ロジックにも一切ない。情報開示は現状ほぼ全開示で、src/ui/html/battleHtml/EnemyFrame.tsx:135-259 が初見から日本語名・HP実数値・Guard・エナジーを表示し、次行動の意図（intent）アイコン + ホバーで技名・ダメージ数値・デバフ名まで見せる（resolveEnemyAction の V-ENM-02 キャッシュでプレビュー=実行が保証済み）。Journal 図鑑（src/ui/html/journalHtml/pages/MemoriesPage.tsx:243-325）は発見済み/未遭遇の二値開示だが、JournalContext.tsx:59 の discoverEnemy が戦闘コードから一度も呼ばれておらず図鑑発見が機能していない。観察レベル・遭遇回数のような状態は存在せず、保存先候補の DiscoveryState（journalTypes.ts:30、localStorage 永続）は string[] の二値。ギルドの噂（src/constants/data/camps/RumorData.ts、campTypes.ts:183-198）はエリート率/割引等のバフ購入のみで敵情報ヒント型の噂はない。設計書は .claude/docs/enemy_document/ に深度別データベース 6 ファイル（計約4700行）があり、2 ステージ制・観察開示・間合いパラメータはすべて未記載。

#### ENM-1: EnemyAction/EnemyDefinition 型に間合い・体勢・スタミナ属性を追加 — `modify` / M

- **現状**: src/types/characterTypes.ts:143-156 の EnemyAction は name/type/baseDamage/element/applyDebuffs/guardGain/hitCount/displayIcon/priority/energyCost のみ。EnemyDefinition（同 :169-193）も baseMaxHp/baseMaxAp/baseSpeed/actEnergy/aiPatterns のみで、距離・体勢・スタミナに関わるフィールドは皆無
- **要求**: 主柱「戦闘が間合い・体勢・スタミナを持つ駆け引き」（concept-v2 §1 優先1）。敵の行動に射程・接近/後退・体勢への作用・スタミナコスト等の属性、敵定義に間合い/体勢関連の基礎値が必要
- **影響ファイル**: `src/types/characterTypes.ts`, `src/domain/battles/logic/enemyStateLogic.ts`, `src/domain/characters/enemy/logic/enemyAI.ts`
- **設計書**: .claude/docs/enemy_document/depth1_enemy_database.md / .claude/docs/battle_document/
- **リスク**: 型変更は enemyDepth1-5.ts 全40体の aiPatterns と EnemyFrame/enemyPhaseExecution に波及する。新戦闘コア（battle 領域）の型設計が先に確定しないと手戻りする

#### ENM-2: enemyAI.ts の行動決定に間合い・体勢の状況入力を追加 — `modify` / L

- **現状**: src/domain/characters/enemy/logic/enemyAI.ts:102-142 determineEnemyAction(enemy, currentHp, maxHp, turnNumber) は自分の HP とターン番号だけで判断。resolveEnemyAction のキャッシュキー（同 :59-67）も enemyId|hp|maxHp|turn|callIndex 固定。エナジー消費ループは enemyActionExecution.ts:21-69
- **要求**: 主柱（concept-v2 §1）と体験の柱1「間合いの読み合い」（§2）。敵 AI が現在の間合い・自他の体勢・スタミナ残量を入力に取り、接近/後退/構え直しを含む行動を選ぶ必要がある。キャッシュキーも新状態を含めて再設計
- **影響ファイル**: `src/domain/characters/enemy/logic/enemyAI.ts`, `src/domain/characters/enemy/logic/enemyActionExecution.ts`, `src/domain/characters/enemy/logic/__tests__/enemyAI.test.ts`, `src/domain/battles/execution/enemyPhaseExecution.ts`
- **設計書**: .claude/docs/enemy_document/boss_system_redesign.md / .claude/docs/battle_document/
- **リスク**: V-ENM-02（プレビュー=実行の一致保証）を壊さないこと。キャッシュキーに間合い/体勢を含めないと preview と execute がズレる脆弱性が再発する

#### ENM-3: 観察システムの状態設計（観察レベル・遭遇回数）と永続化 — `new` / M

- **現状**: 観察・遭遇回数を保持する状態はどこにも存在しない（src 全体 grep で observ 系ヒットなし）。最も近い DiscoveryState は src/types/journalTypes.ts:30-37 で cards/enemies/equipment/events の string[]（発見済み二値）のみ。JournalContext.tsx:92-108 で localStorage キー journal_discovery に永続化
- **要求**: 副柱「敵の情報は観察によってのみ得られる」+ 世界設定 A1「手記は死を越える」（concept-v2 §1, §3）。敵ごとの観察レベル（遭遇回数・撃破数・被弾技などから昇格）を持つ EnemyKnowledge 状態を JournalContext 配下に新設し、localStorage 永続（死んでも引き継ぐ）にする
- **影響ファイル**: `src/types/journalTypes.ts`, `src/contexts/JournalContext.tsx`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: 既存 journal_discovery の保存形式を変えるためマイグレーション（string[] → レコード型）が必要。状態単一所有の原則上、所有者は JournalContext に固定すること

#### ENM-4: discoverEnemy の戦闘接続（現状未配線）と遭遇カウント記録 — `modify` / S

- **現状**: JournalContext.tsx:59,216 に discoverEnemy が定義されているが、呼び出し元は src 全体で JournalContext 自身のみ（grep 確認済み）。BattleScreen.tsx / VictoryScreen.tsx / useBattleOrchestrator から一切呼ばれず、図鑑の敵は永久に「未遭遇」のまま
- **要求**: 体験の柱2「観察と学習」（concept-v2 §2）の土台。戦闘開始時に遭遇記録、戦闘終了時に観察レベル更新を呼ぶ配線が必要。ENM-3 の前提となる最小修正
- **影響ファイル**: `src/ui/html/battleHtml/BattleScreen.tsx`, `src/contexts/JournalContext.tsx`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: 既存バグの修正に相当するため、ENM-3 着手前に単独で先行実施可能

#### ENM-5: EnemyFrame の段階的情報開示（観察レベル連動） — `modify` / M

- **現状**: src/ui/html/battleHtml/EnemyFrame.tsx が初見から全開示: 日本語名（:135）、HP 実数値 hp/maxHp（:238-240）、Guard 値（:198-210）、エナジー値（:245-259）、次行動の意図アイコン（:141）+ ホバーで技名・ダメージ数値・hitCount・デバフ名（:143-174）。開示を制御する仕組みはない
- **要求**: 副柱「敵ステータスは初見では非開示。観察・経験で判明」（concept-v2 §1 優先3）。観察レベルに応じて 名前のみ → HP バーのみ（数値非表示）→ 意図タイプ → 具体数値、のような段階開示に変更。意図表示の仕組み自体（resolveEnemyAction + tooltip）は流用
- **影響ファイル**: `src/ui/html/battleHtml/EnemyFrame.tsx`, `src/ui/css/pages/battle/battle-enemy.css`, `src/ui/css/battle/EnemyActionPreview.css`
- **設計書**: .claude/docs/enemy_document/depth1_enemy_database.md / .claude/docs/ui_ux_design_guide.md
- **リスク**: PixiJS Phase 2-4 で戦闘演出が Pixi 側（src/ui/pixi/battle/layers/CharacterLayer.tsx）へ移る計画のため、HTML 側に作り込みすぎると二重実装になる。開示判定ロジックは UI 非依存の純関数に切り出すこと

#### ENM-6: Journal 敵図鑑の段階開示対応（二値→観察レベル） — `modify` / M

- **現状**: src/ui/html/journalHtml/pages/MemoriesPage.tsx:243-325 EnemiesSection は discovered なら HP/SPD/説明を全表示、未発見なら「未遭遇」の二値。行動パターンや弱点の表示欄はない
- **要求**: 世界設定 A1「観察した敵情報は手記（Journal）に蓄積」+ 体験の柱2（concept-v2 §2-3）。観察レベルに応じて 名前→基礎ステータス→行動パターン→弱点 と開示段階を増やす図鑑表示。次の遭遇で活かせる情報源にする
- **影響ファイル**: `src/ui/html/journalHtml/pages/MemoriesPage.tsx`, `src/constants/data/camps/EnemyEncyclopediaData.ts`, `src/constants/data/journal/EnemyEncyclopediaData.ts`, `src/ui/css/journal/Memories.css`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: EnemyEncyclopediaData.ts の ENEMIES_BY_DEPTH（Record<Depth, EnemyDefinition[]>）が深度 1-5 前提のため、ENM-8 のステージ再編と同時期に行うと差し戻しが減る

#### ENM-7: ギルドの噂に敵情報ヒント型を追加 — `modify` / M

- **現状**: src/types/campTypes.ts:183-187 RumorEffect は elite_rate / shop_discount / treasure_rate / start_bonus の4種のみ。src/constants/data/camps/RumorData.ts の噂は全て一時バフ購入で、敵情報を与える噂は存在しない
- **要求**: 世界設定 B2「ギルドの噂が敵情報の初期ヒント」（concept-v2 §3）。RumorEffect に enemy_info 型（対象敵の観察レベルを初期付与する等）を追加し、ステージ別の敵ヒント噂データを用意
- **影響ファイル**: `src/types/campTypes.ts`, `src/constants/data/camps/RumorData.ts`, `src/contexts/GuildContext.tsx`, `src/ui/html/campsHtml/Guild/RumorsTab.tsx`
- **設計書**: .claude/docs/camp_document/
- **リスク**: 観察レベルの状態所有者は JournalContext（ENM-3）なので、GuildContext から直接書き換えず公開 API 経由で更新すること（状態単一所有）

#### ENM-8: 敵選択ロジックの 5 深度固定 → ステージ ID 制への置換 — `replace` / M

- **現状**: src/domain/characters/enemy/logic/enemyAI.ts:173-215 selectRandomEnemy(depth, encounterSize) が Record<number, DepthEnemyData> で深度 1-5 をハードコード。呼び出し元 src/ui/html/battleHtml/BattleScreen.tsx:236-238 も depth を渡す。図鑑側 src/constants/data/camps/EnemyEncyclopediaData.ts も Record<Depth, ...> で 5 深度前提
- **要求**: 世界設定 B1「環境ルール付き複数ステージ」+ B3「どのステージからでも開始可」（concept-v2 §3 ステージ構成）。深度番号ではなくステージ ID をキーに敵プールを引く構造へ置換（まず 2 ステージ）
- **影響ファイル**: `src/domain/characters/enemy/logic/enemyAI.ts`, `src/ui/html/battleHtml/BattleScreen.tsx`, `src/constants/data/camps/EnemyEncyclopediaData.ts`, `src/types/characterTypes.ts`
- **設計書**: .claude/docs/enemy_document/boss_system_redesign.md / .claude/docs/danjeon_document/
- **リスク**: dungeon 領域のステージ定義（GameStateContext の depth / battleConfig）と型を共有するため、dungeon 側のステージ ID 設計確定が前提

#### ENM-9: 敵データ 40 体の 2 ステージ再配分 + 新コア対応の行動再定義 + トーン調整 — `replace` / L

- **現状**: src/constants/data/characters/enemy/enemyDepth1.ts〜enemyDepth5.ts に 8 体 × 5 深度 = 40 体（各ファイル約 200 行）。aiPatterns は現行戦闘コア（baseDamage/energyCost/デバフ）前提。命名・説明はダーク寄り（例: enemyDepth1.ts:8 「腐肉が露出した痩せこけた黒い野犬」、深度テーマ = 腐食/狂乱/混沌/虚無/深淵）
- **要求**: ステージ構成「開発中はまず 2 ステージ（翠苔の森殿=初級 / 霧の水郷=中級）」（concept-v2 §3）+ 方針 B「資産流用」（§4）+ トーン「ライト幻想寄りに調整」（§2）。既存 40 体から 2 ステージ分を選抜・再配分し、ENM-1 の新属性（間合い・体勢・スタミナ）で aiPatterns を再定義、名称・説明文をライト幻想トーンへ調整（A3 不採用のため敵デザイン自体は現状ベース維持）
- **影響ファイル**: `src/constants/data/characters/enemy/enemyDepth1.ts`, `src/constants/data/characters/enemy/enemyDepth2.ts`, `src/constants/data/characters/enemy/enemyDepth3.ts`, `src/constants/data/characters/enemy/enemyDepth4.ts`, `src/constants/data/characters/enemy/enemyDepth5.ts`
- **設計書**: .claude/docs/enemy_document/depth1_enemy_database.md / .claude/docs/enemy_document/depth2_enemy_database.md
- **リスク**: 設計書駆動の原則上、ENM-10 の新ステージ敵データベース設計書を先に書き、数値はそちらを正とすること。未使用になる深度 3-5 相当のデータは削除でなく凍結（バックログ同様）が安全

#### ENM-10: enemy_document の再編（ステージ別データベース + 観察開示仕様の新設） — `new` / M

- **現状**: .claude/docs/enemy_document/ は depth1〜4_enemy_database.md + depth5_boss_database.md + boss_system_redesign.md の深度別 6 ファイル（計 4727 行、Ver 4.0 基準）。間合い・体勢・スタミナのパラメータ仕様、観察レベル別の開示内容（何レベルで何が見えるか）の記載は一切ない
- **要求**: 合格基準「設計書と実装の差分ゼロ（新設計書を先に更新）」（concept-v2 §5）。2 ステージ別の敵データベース + 敵ごとの観察開示テーブル + 新コア行動パラメータを設計書として先行整備
- **影響ファイル**: `.claude/docs/enemy_document/`
- **設計書**: .claude/docs/enemy_document/depth1_enemy_database.md / .claude/docs/enemy_document/depth2_enemy_database.md / .claude/docs/enemy_document/depth3_enemy_database.md / .claude/docs/enemy_document/depth4_enemy_database.md / .claude/docs/enemy_document/depth5_boss_database.md / .claude/docs/enemy_document/boss_system_redesign.md
- **リスク**: 旧深度別ファイルは削除せず Ver 表記で凍結し、新ステージ設計書と並存させると監査（差分ゼロ確認）が楽になる

#### ENM-11: 意図表示・プレビュー一致基盤と敵状態生成の流用 — `keep` / S

- **現状**: 意図表示は既に存在: EnemyFrame.tsx:111-117 が resolveEnemyAction で次行動を表示し、enemyAI.ts:57-100 の解決キャッシュ（V-ENM-02 修正）でプレビュー=実行が保証済み。previewEnemyActions（enemyActionExecution.ts:98-134）、enemyStateLogic.ts の createEnemyStateFromDefinition、enemyUtils.ts、遭遇パターン構造（EncounterPattern/EncounterSize）、テスト enemyAI.test.ts も健在
- **要求**: 方針 B「資産流用」（concept-v2 §4）。意図表示の仕組み・プレビュー一致保証・敵インスタンス生成・遭遇パターン構造は新コアでもそのまま骨格として流用する（ENM-2/ENM-5 はこの上に載せる改修）
- **影響ファイル**: `src/domain/characters/enemy/logic/enemyAI.ts`, `src/domain/characters/enemy/logic/enemyActionExecution.ts`, `src/domain/battles/logic/enemyStateLogic.ts`, `src/domain/characters/enemy/logic/enemyUtils.ts`, `src/domain/characters/enemy/logic/__tests__/enemyAI.test.ts`
- **リスク**: キャッシュキー（enemyAI.ts:59-67）だけは ENM-2 で必ず拡張すること。キー不変のまま間合い・体勢を入力に足すと V-ENM-02 と同型の不一致バグが再発する

### 7.4 ダンジョン構造（dungeon-structure / 14件）

**現状サマリ**: 現行のダンジョンは「1ダンジョン×5深度×5フロア」構成で、深度は src/types/cardTypes.ts:17 の `Depth = 1|2|3|4|5` 型に固定され、深度名・推奨レベルは src/constants/dungeonConstants.ts:54-90 と src/domain/dungeon/depth/depthManager.ts:7-13 に定義されている。マップ生成は src/domain/dungeon/logic/dungeonLogic.ts の純関数群が担い、全深度共通の単一設定 DEFAULT_MAP_CONFIG（7行・12ノード、src/constants/dungeonConstants.ts:97-104）で生成される設計のため、関数自体は config 駆動で流用可能。一方、フロア数 5 は src/contexts/DungeonRunContext.tsx:124 と src/ui/html/dungeonHtml/NodeMap.tsx:223,259,389 にハードコードされている。深度選択 UI は DungeonGate 内の DepthSelector（src/ui/html/dungeonHtml/preparations/DepthSelector.tsx:17、ラベルに「テスト用」と明記）で、推奨等級の掲示はない。敵は深度キーのデータ（src/constants/data/characters/enemy/enemyDepth1-5.ts）を src/domain/characters/enemy/logic/enemyAI.ts:177-183 のハードコード Record で引いており、報酬は敵種別のみ依存（soulSystem.ts:185）で深度スケーリングは魔術師カードの MAGIC_MULTIPLIERS（src/constants/cardConstants.ts:17-23）程度。帰還システムは設計書（.claude/docs/danjeon_document/return_system_design.md）があるが実装は ExplorationScreen.tsx:41-47 のプレースホルダのみで、テレポートストーン消費も帰還ルートも未実装。環境ルールに使える既存フックとして src/types/battleTypes.ts:19 に BuffOwner 'environment' が存在する。

#### DUN-1: ステージ定義型（StageDefinition）の新設 — `new` / M

- **現状**: ステージという概念が存在しない。深度情報は DepthDisplayInfo（src/types/dungeonTypes.ts:66-72）と DEPTH_DISPLAY_INFO（src/constants/dungeonConstants.ts:54-90）に分散し、名称・推奨レベルのみ。環境ルール・階層数・敵プール参照を持つ構造体はない
- **要求**: concept-v2 §3 ステージ構成「複数ダンジョン（ステージ）×テーマ別の複数階層」と B1「環境ルール付きの歪みの土地」、B3「難易度=歪みの濃さ（推奨等級）」を1つのデータ型で表現する。StageDefinition（id / 名称 / テーマ / 推奨等級 / 階層数 / 階層別 MapGenerationConfig / 環境ルール / 敵プール参照 / 背景アセット）を新設
- **影響ファイル**: `src/types/dungeonTypes.ts`, `src/types/index.ts`, `src/constants/data/stages/（新規ディレクトリ）`
- **設計書**: .claude/docs/danjeon_document/stage_system_design.md（新規）
- **リスク**: Depth 型の置き換え（DUN-3）と設計を揃えないと型移行が二度手間になる。先に設計書を作る（設計書駆動）

#### DUN-2: 2ステージ分のステージデータ作成（初級/中級、ライト幻想トーン） — `new` / M

- **現状**: 深度テーマは「腐食/狂乱/混沌/虚無/深淵」のダーク寄り名称（src/domain/dungeon/depth/depthManager.ts:7-13、src/constants/dungeonConstants.ts:54-90）。背景画像は深度キー（src/constants/uiConstants.ts:54-58、深度5は深度4画像を流用）
- **要求**: concept-v2 §3 ステージ構成「開発中はまず2ステージ（候補例: 翠苔の森殿=初級 / 霧の水郷=中級）」+ §2 トーン「ライト幻想寄りに調整」。StageDefinition 形式で2ステージのデータ（名称・推奨等級・階層構成・環境ルール・敵プール割当・背景）を作成。$0 制約のため背景は既存アセット流用
- **影響ファイル**: `src/constants/data/stages/（新規）`, `src/constants/uiConstants.ts`
- **設計書**: .claude/docs/danjeon_document/stage_system_design.md（新規）
- **リスク**: 敵データ流用の割当（どの旧深度の敵をどのステージに置くか）はバランス調整を伴う。enemy 領域と要調整

#### DUN-3: Depth 型（1|2|3|4|5）から stageId + 階層番号モデルへの置き換え — `replace` / L

- **現状**: src/types/cardTypes.ts:17 で `Depth = 1 | 2 | 3 | 4 | 5` が定義され、dungeonTypes.ts:14 / campTypes.ts 経由で再エクスポート。GameStateContext.tsx:26,60,110 が gameState.depth を保持、DungeonRun.selectedDepth（dungeonTypes.ts:48）、useBattleOrchestrator.ts:119 の depth 引数、BattleScreen.tsx:69-74 など広範囲が依存
- **要求**: concept-v2 §3 ステージ構成（現行「1ダンジョン×5深度×5フロア」からの置換）。ラン状態と画面ルーティングの基準を depth から (stageId, floorNumber) に移行し、Depth union 型への依存を排除する
- **影響ファイル**: `src/types/cardTypes.ts`, `src/types/dungeonTypes.ts`, `src/types/campTypes.ts`, `src/contexts/GameStateContext.tsx`, `src/domain/dungeon/depth/depthManager.ts`, `src/constants/dungeonConstants.ts`, `src/ui/html/battleHtml/BattleScreen.tsx`, `src/domain/battles/managements/useBattleOrchestrator.ts`
- **設計書**: .claude/docs/danjeon_document/stage_system_design.md（新規）
- **リスク**: Depth はカード（DepthCurveType）・魔石・闇市（refreshDarkMarketOnBossDefeat の depth 引数 BattleScreen.tsx:443-446）にも波及する横断変更。段階移行（Depth を stageId のエイリアスとして残す中間段階）を検討

#### DUN-4: DungeonRunContext のステージ対応（フロア数ハードコード除去） — `modify` / M

- **現状**: advanceToNextFloor が `prev.floorNumber >= 5` で打ち止め（src/contexts/DungeonRunContext.tsx:124）。initializeDungeonRun は depth のみ受け取り常に DEFAULT_MAP_CONFIG で生成（src/domain/dungeon/logic/dungeonLogic.ts:283-293）。DungeonRun 型に stage 情報がない（src/types/dungeonTypes.ts:46-54）
- **要求**: concept-v2 §3「ステージ内複数階層」。DungeonRun を stageId 基準に変更し、総階層数と階層別マップ設定を StageDefinition から取得する。initializeRun(stageId) → generateFloorMap(stage.floorConfigs[n]) の流れに変更
- **影響ファイル**: `src/contexts/DungeonRunContext.tsx`, `src/domain/dungeon/logic/dungeonLogic.ts`, `src/types/dungeonTypes.ts`
- **設計書**: .claude/docs/danjeon_document/stage_system_design.md（新規）
- **リスク**: ラン状態はリロードで消える（永続化なし）。不可逆性（副柱）強化で永続化を入れるなら別途検討

#### DUN-5: マップ生成純関数群（dungeonLogic.ts）の流用 — `keep` / S

- **現状**: generateFloorMap / selectNode / completeNode / getNodesByRow / getConnectionLines は MapGenerationConfig 駆動の純関数で、ステージ固有値を持たない（src/domain/dungeon/logic/dungeonLogic.ts:126-167 ほか）。テストも __tests__ 同居パターンで存在
- **要求**: concept-v2 §4 方針 B「資産流用」。ノードマップ生成ロジックはステージ制でもそのまま流用可。呼び出し側（DUN-4）がステージの階層別 config を渡すだけでよい
- **影響ファイル**: `src/domain/dungeon/logic/dungeonLogic.ts`, `src/domain/dungeon/logic/nodeEventLogic.ts`
- **リスク**: DungeonFloor.depth フィールド（dungeonTypes.ts:39）の型変更（DUN-3）に伴うシグネチャ微修正のみ発生

#### DUN-6: ステージ選択画面の新設（推奨等級掲示・どこからでも開始可） — `new` / M

- **現状**: DungeonGate 内の DepthSelector が深度1-5の数字ボタンのみで、ラベルは「深度選択 (テスト用)」（src/ui/html/dungeonHtml/preparations/DepthSelector.tsx:17-19）。DEPTH_DISPLAY_INFO.recommendedLevel は定義済みだが UI 未表示（src/constants/dungeonConstants.ts:60-88）
- **要求**: concept-v2 B3「難易度=歪みの濃さ。ギルドが各ステージの推奨等級を掲示。どこからでも入れるが難易度が違う」+ §3「ステージ選択=難易度選択」。ステージカード型の選択 UI（テーマ名・歪み等級・環境ルールの予告・推奨等級）を DungeonGate に新設し DepthSelector を置き換える
- **影響ファイル**: `src/ui/html/dungeonHtml/preparations/DepthSelector.tsx（置換）`, `src/ui/html/dungeonHtml/DungeonGate.tsx`, `src/ui/html/dungeonHtml/DungeonGate.css`
- **設計書**: .claude/docs/danjeon_document/dungeon_exploration_ui_design_v3.0.md / .claude/docs/danjeon_document/stage_system_design.md（新規）
- **リスク**: 推奨等級の表示元をギルド（B2 宿場町）側に置くか DungeonGate に置くかはキャンプ領域と要調整

#### DUN-7: B1 環境ルールのステージ定義保持と戦闘への受け渡し配管 — `new` / M

- **現状**: 環境という概念はステージ/ダンジョン側に存在しない。戦闘側には BuffOwner = 'player' | 'enemy' | 'environment'（src/types/battleTypes.ts:19）と buffLogic.ts:19 の appliedBy デフォルト 'environment' という未活用フックのみ。startBattle の config（GameStateContext.tsx）には enemyIds / backgroundType / enemyType しかない
- **要求**: concept-v2 B1「環境（視界・足場・広さ）が間合い・体勢に影響」。StageDefinition に環境ルール（視界/足場/広さのパラメータ）を持たせ、startBattle の battleConfig 経由で useBattleOrchestrator へ渡す配管を作る。間合い・体勢への実効果の実装は battle-core 領域、本ギャップは定義と受け渡し I/F まで
- **影響ファイル**: `src/types/dungeonTypes.ts`, `src/types/battleTypes.ts`, `src/contexts/GameStateContext.tsx`, `src/ui/html/dungeonHtml/NodeMap.tsx`, `src/ui/html/battleHtml/BattleScreen.tsx`
- **設計書**: .claude/docs/danjeon_document/stage_system_design.md（新規） / .claude/docs/battle_document/（新戦闘コア設計書側と整合）
- **リスク**: 環境ルールのパラメータ仕様は新戦闘コア（間合い・体勢・スタミナ）の設計確定が前提。battle-core 領域との順序依存が強い

#### DUN-8: 敵プールのステージ別化（深度ハードコードの解消） — `modify` / M

- **現状**: selectRandomEnemy が深度→敵データの Record をハードコード（src/domain/characters/enemy/logic/enemyAI.ts:177-183）。敵データは src/constants/data/characters/enemy/enemyDepth1-5.ts に深度キーで定義され、図鑑も ENEMIES_BY_DEPTH（src/constants/data/camps/EnemyEncyclopediaData.ts:18）で深度参照
- **要求**: concept-v2 §3 ステージ構成 + §4「敵デザインは現状ベースを維持（A3 不採用）・データ定義は流用」。敵定義データ自体は流用し、ステージ定義が敵プール（encounter patterns）を参照する構造に変更。enemyAI.ts のハードコード Record を廃止しステージ経由の引き当てにする
- **影響ファイル**: `src/domain/characters/enemy/logic/enemyAI.ts`, `src/constants/data/characters/enemy/enemyDepth1.ts`, `src/constants/data/characters/enemy/enemyDepth2.ts`, `src/constants/data/characters/enemy/enemyDepth3.ts`, `src/constants/data/characters/enemy/enemyDepth4.ts`, `src/constants/data/characters/enemy/enemyDepth5.ts`, `src/constants/data/camps/EnemyEncyclopediaData.ts`
- **設計書**: .claude/docs/enemy_document/（ステージ別プール表の追記） / .claude/docs/danjeon_document/stage_system_design.md（新規）
- **リスク**: 敵 id が depth1_hound 等の深度プレフィックス。図鑑（Journal A1 手記は死を越える）の表示キーにも波及するため journal 領域と要調整

#### DUN-9: 報酬・スケーリングのステージ等級（歪みの濃さ）駆動化 — `modify` / M

- **現状**: ソウル価値は敵種別のみ依存（src/domain/camps/logic/soulSystem.ts:185 getSoulValue、BattleScreen.tsx:396-397）。魔術師カードの威力倍率は深度キーの MAGIC_MULTIPLIERS（src/constants/cardConstants.ts:17-23、1/2/4/8/16倍）。ノードイベント報酬（nodeEventLogic.ts）は深度・ステージと無関係の固定値
- **要求**: concept-v2 B3「難易度=歪みの濃さ」。報酬・スケーリングの基準を深度番号からステージの歪み等級に付け替える。「リスクが深いほど報酬が増える」設計（return_system_design.md §1.1）をステージ等級で再表現する
- **影響ファイル**: `src/constants/cardConstants.ts`, `src/domain/camps/logic/soulSystem.ts`, `src/domain/dungeon/logic/nodeEventLogic.ts`, `src/ui/html/battleHtml/BattleScreen.tsx`
- **設計書**: .claude/docs/danjeon_document/stage_system_design.md（新規） / .claude/docs/card_document/（MAGIC_MULTIPLIERS の正の置き場）
- **リスク**: MAGIC_MULTIPLIERS は魔術師スケーリングの根幹。剣士先行方針（concept-v2 §4）なら剣士分のみ先行し魔術師は後追い適合で凍結可

#### DUN-10: 帰還システムの実装（設計書あり・実装はプレースホルダ） — `modify` / L

- **現状**: ExplorationScreen.tsx:41-47 で teleportStoneCount = 0 の固定値と handleReturnConfirm の TODO コメントのみ。ReturnConfirmModal.tsx は UI だけ存在し常に使用不可。設計書 .claude/docs/danjeon_document/return_system_design.md にあるテレポートストーン消費・帰還ルート（遭遇率逓減式）・深度5帰還禁止はすべて未実装
- **要求**: concept-v2 §1 副柱「結果は不可逆に残る」+ 合格基準「設計書と実装の差分ゼロ」。ステージ制を前提に帰還設計を再定義（旧「深度5=深淵の帰還禁止」はステージ属性へ置換）し、テレポートストーン消費と帰還確定処理を実装する
- **影響ファイル**: `src/ui/html/dungeonHtml/ExplorationScreen.tsx`, `src/ui/html/dungeonHtml/exploration/ReturnConfirmModal.tsx`, `src/contexts/DungeonRunContext.tsx`, `src/domain/dungeon/hooks/useExplorationItemUsage.ts`
- **設計書**: .claude/docs/danjeon_document/return_system_design.md（ステージ制前提に改訂）
- **リスク**: 帰還ルート（戦闘しながら戻る）はマップ構造の逆走を要し新戦闘コアと絡む。テレポートストーンのみ先行実装し帰還ルートは Tier 2 に分離するのが安全

#### DUN-11: NodeMap / ExplorationScreen UI のステージ表示対応 — `modify` / S

- **現状**: NodeMap.tsx:223 で `floorNumber >= 5` 判定、:259 で「Floor {n} / 5」表示、:389 で「All 5 floors conquered」文言、:258 で深度バッジ（DEPTH_DISPLAY_INFO.japaneseName）。戦闘背景は深度キーの DEPTH_BACKGROUND_IMAGES（src/constants/uiConstants.ts:54-58）
- **要求**: concept-v2 §3 ステージ構成。フロア表示・クリア判定・バッジ・背景をステージ定義（総階層数・ステージ名・背景アセット）から導出する表示へ変更
- **影響ファイル**: `src/ui/html/dungeonHtml/NodeMap.tsx`, `src/ui/html/dungeonHtml/ExplorationScreen.tsx`, `src/constants/uiConstants.ts`, `src/ui/html/battleHtml/BattleScreen.tsx`
- **設計書**: .claude/docs/danjeon_document/dungeon_exploration_ui_design_v3.0.md
- **リスク**: BattleScreen.tsx:519 の「{depth}-{encounterCount+1}」表記も同時に直さないと表示不整合が残る

#### DUN-12: depthManager.ts の廃止（ステージテーマへ統合） — `remove` / S

- **現状**: src/domain/dungeon/depth/depthManager.ts は深度名テーブル（腐食〜深淵）と neutralTheme のみ。テーマは既に全深度共通の neutralTheme に統一済み（:36-42）で、getDepthInfo の実利用も薄い（NodeMap.tsx:11、BattleScreen.tsx:21 は neutralTheme のみ import）
- **要求**: concept-v2 §3 ステージ構成 + §2 トーン調整。深度名テーブルはステージ定義（DUN-1/DUN-2）に吸収し、テーマカラーは StageDefinition のテーマ属性へ移管。本ファイルは不要化
- **影響ファイル**: `src/domain/dungeon/depth/depthManager.ts`, `src/ui/html/dungeonHtml/NodeMap.tsx`, `src/ui/html/battleHtml/BattleScreen.tsx`
- **リスク**: なし（import 2箇所の差し替えのみ）

#### DUN-13: ギルド噂効果のマップ生成接続の再定義（B2 境界） — `modify` / S

- **現状**: RumorData.ts の elite_rate / treasure_rate 効果は RumorsTab.tsx:41-45 で表示されるのみで、マップ生成には未接続（DungeonRunContext.tsx:76 initializeRun は常に DEFAULT_MAP_CONFIG、generateFloorMap に噂補正を渡す経路がない）。実装と見た目が乖離した既存の未配線
- **要求**: concept-v2 B2「ギルドの噂が敵情報の初期ヒント」。噂は出現率バフから敵情報ヒント（A1 手記/Journal 連携）へ役割が再定義されるため、現行の率系噂効果は (a) ステージ別 MapGenerationConfig への補正として配線する か (b) 不要化して情報ヒント型へ置換する かを要件定義で確定する
- **影響ファイル**: `src/constants/data/camps/RumorData.ts`, `src/contexts/DungeonRunContext.tsx`, `src/ui/html/campsHtml/Guild/RumorsTab.tsx`
- **設計書**: .claude/docs/camp_document/（噂仕様の改訂） / .claude/docs/danjeon_document/stage_system_design.md（新規）
- **リスク**: 噂の再定義はキャンプ領域・Journal 領域と三者にまたがる。本領域では「マップ生成への接続有無」だけを決めればよい

#### DUN-14: ダンジョン設計書群のステージ制への全面改訂（設計書先行） — `modify` / M

- **現状**: 設計書は .claude/docs/danjeon_document/（ディレクトリ名が danjeon の typo）に2文書。dungeon_exploration_ui_design_v3.0.md §2 が DEFAULT_MAP_CONFIG（全深度共通・7行固定）を source of truth と明記、return_system_design.md は深度5=深淵特例など5深度前提で記述
- **要求**: concept-v2 §5 合格基準「設計書と実装の差分ゼロ（新設計書を先に更新し、監査で差分ゼロ確認）」。実装着手前にステージシステム設計書（ステージ定義スキーマ・2ステージの数値・環境ルール・帰還仕様）を新規作成し、既存2文書をステージ制前提に改訂する
- **影響ファイル**: `.claude/docs/danjeon_document/stage_system_design.md（新規）`, `.claude/docs/danjeon_document/dungeon_exploration_ui_design_v3.0.md`, `.claude/docs/danjeon_document/return_system_design.md`, `.claude/docs/INDEX.md`
- **設計書**: .claude/docs/danjeon_document/stage_system_design.md（新規） / .claude/docs/danjeon_document/dungeon_exploration_ui_design_v3.0.md / .claude/docs/danjeon_document/return_system_design.md
- **リスク**: danjeon_document の typo ディレクトリ名は INDEX.md・スキル参照と整合を取って改名するか温存するかを先に決める。設計書駆動の原則上、本ギャップが他の全 DUN ギャップの前提

### 7.5 手記・知識（journal-knowledge / 11件）

**現状サマリ**: 手記（Journal）は `src/contexts/JournalContext.tsx` が UI 状態・メモ・発見記録を管理し、独自の localStorage キー（journal_notes / journal_discovery、JournalContext.tsx:28-31）で永続化している。発見記録は ID 配列のみ（`src/types/journalTypes.ts:30-35`）で、実際に配線されているのは discoverCard だけ（`src/contexts/PlayerContext.tsx:278,381`）。discoverEnemy / discoverEquipment / discoverEvent はリポジトリ全体で一度も呼ばれておらず、敵図鑑は常に「未遭遇」のまま。図鑑データ側は `src/constants/data/camps/EnemyEncyclopediaData.ts:52-61` が isEncountered: true / timesDefeated: 0 をハードコードし、観察レベルや行動パターン知識の概念がない。死亡処理（`src/domain/battles/logic/deathHandler.ts`）はインベントリ・探索資源のみ消去し、ゲームオーバー（`src/ui/html/battleHtml/BattleScreen.tsx:486`）も saveManager.deleteSave() のみのため、手記データは偶然「死を越えて」残るが、SaveData（`src/types/saveTypes.ts:51-58`）に手記が含まれずライフサイクルは未定義。ギルドの噂（`src/constants/data/camps/RumorData.ts`）は全てレート系バフで敵情報ヒント型が存在せず、B2 の接続点がない。設計書 `journal_document/journal_system_implementation_plan.md` は敵のパターン・弱点・撃破数の記録を謳うが実装と乖離している。

#### JNL-1: 敵観察記録のデータモデル新設（観察レベル付き敵知識） — `new` / M

- **現状**: 発見記録は敵 ID の配列のみ（src/types/journalTypes.ts:30-35 DiscoveryState.enemies: string[]）。図鑑エントリ型 EnemyEncyclopediaEntry（src/types/campTypes.ts:393-398）に isEncountered / timesDefeated はあるが常に固定値（src/constants/data/camps/EnemyEncyclopediaData.ts:58-59 で isEncountered: true / timesDefeated: 0 ハードコード）。観察の深度・行動パターン知識・弱点知識を表すモデルが存在しない
- **要求**: concept-v2 §1 副柱3「敵の情報は観察によってのみ得られる」+ §2 体験の柱2「観察と学習」+ §3 A1「観察した敵情報が手記に蓄積」に基づき、敵ごとの観察レベル（未遭遇→遭遇→観察→熟知など段階定義）と、段階ごとに開示される知識（HP/速度/AIパターン/弱点、新戦闘コアの間合い・体勢・スタミナ傾向）を持つデータモデルをドメイン層（例: src/domain/journal/）に新設する。撃破数・遭遇数のカウントも含める
- **影響ファイル**: `src/types/journalTypes.ts`, `src/types/campTypes.ts`, `src/domain/journal/ (新規)`, `src/constants/data/camps/EnemyEncyclopediaData.ts`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: 観察レベルの段階定義は新戦闘コア（間合い・体勢・スタミナ）の情報構造が決まらないと確定できない。battle 領域の再設計と並行で型だけ先行させると手戻りの可能性

#### JNL-2: 戦闘イベントから観察記録への配線（discoverEnemy 呼び出し + 遭遇/撃破/行動観察の記録） — `modify` / M

- **現状**: discoverEnemy は JournalContext.tsx:216 に定義されているがリポジトリ全体で呼び出し箇所ゼロ（grep で確認、discoverCard のみ src/contexts/PlayerContext.tsx:278,381 で使用）。戦闘開始・敵行動・撃破のどのタイミングでも手記への記録が発生しない
- **要求**: concept-v2 §3 A1「観察した敵情報は手記に蓄積」に基づき、戦闘開始時（遭遇記録）・敵の行動実行時（パターン観察）・撃破時（撃破数加算）に JNL-1 の観察記録を更新する接続を BattleScreen / useBattleOrchestrator 側に追加する。JournalProvider は Provider 階層で battle より上位のため hook 経由で接続可能
- **影響ファイル**: `src/ui/html/battleHtml/BattleScreen.tsx`, `src/domain/battles/managements/useBattleOrchestrator.ts`, `src/contexts/JournalContext.tsx`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md / .claude/docs/battle_document/battle_logic.md
- **リスク**: useBattleOrchestrator は約877行で再設計対象のため、旧オーケストレーターに配線せず新戦闘コア設計に組み込むのが安全（実装順序を battle 領域の後にする）

#### JNL-3: 図鑑データの isEncountered ハードコード除去と discovery 連動 — `modify` / S

- **現状**: src/constants/data/camps/EnemyEncyclopediaData.ts:52-61 の createEnemyEncyclopediaEntries が全敵に isEncountered: true / timesDefeated: 0 / firstEncounteredDate: undefined を固定で返す（コメント「All enemies are visible in encyclopedia」）。journal 側の re-export（src/constants/data/journal/EnemyEncyclopediaData.ts）も同関数を公開
- **要求**: concept-v2 §1 副柱3（観察によってのみ情報を得る）に基づき、図鑑エントリ生成を JNL-1 の観察記録（JournalContext の discovery / 観察レベル）から導出する純関数に改修し、固定値を除去する
- **影響ファイル**: `src/constants/data/camps/EnemyEncyclopediaData.ts`, `src/constants/data/journal/EnemyEncyclopediaData.ts`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: ギルド/図書館系 UI が同データを使っている場合は表示が変わる（camp 領域と表示仕様の整合確認が必要）

#### JNL-4: MemoriesPage 敵セクションの段階開示 UI — `modify` / M

- **現状**: src/ui/html/journalHtml/pages/MemoriesPage.tsx:243-325 の EnemiesSection は発見済みなら HP/SPD/説明を全開示、未発見なら「未遭遇」の二値表示のみ。AIパターン・弱点・間合い等の表示枠がない。探索中も ExplorationJournalTab.tsx:51-55 経由で同コンポーネントを再利用
- **要求**: concept-v2 §2 体験の柱2「観察と学習 — 未知の敵を観察して情報を積み上げ、次の遭遇で活かす」に基づき、観察レベルに応じた段階開示 UI（例: 遭遇=名前のみ → 観察=HP帯・速度 → 熟知=行動パターン・弱点・間合い/体勢/スタミナ傾向）へ改修する。噂由来の初期ヒント（JNL-6）の表示枠も設ける
- **影響ファイル**: `src/ui/html/journalHtml/pages/MemoriesPage.tsx`, `src/ui/css/journal/Memories.css`, `src/ui/html/dungeonHtml/exploration/ExplorationJournalTab.tsx`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: 表示する知識項目は新戦闘コアの確定後でないと最終形にできない（JNL-1 と同じ依存）。CSS スコープ規約（.memories-page 配下）順守

#### JNL-5: 手記の永続化ライフサイクル定義（死亡・ゲームオーバー・ニューゲーム）とセーブ統合 — `modify` / M

- **現状**: 手記は SaveData（src/types/saveTypes.ts:51-58）に含まれず、独立 localStorage キー journal_notes / journal_discovery（src/contexts/JournalContext.tsx:28-31）で保存。死亡処理（src/domain/battles/logic/deathHandler.ts:38-100）はインベントリ・装備・探索資源のみ消去で手記は無傷（偶然 A1 と整合）。ゲームオーバー時（src/ui/html/battleHtml/BattleScreen.tsx:484-488）とセーブ削除時（src/ui/html/characterSelectHtml/CharacterSelect.tsx:77-80、SaveLoadUI.tsx:96）も saveManager.deleteSave() のみで手記が残るが、これは設計されていない挙動。exportSave/importSave（src/domain/save/logic/saveManager.ts:141-169）は SAVE_KEY のみ対象で、バックアップ復元では手記＝知識が失われる
- **要求**: concept-v2 §3 A1「探索者が死んでも手記＝知識だけは次に引き継がれる」を意図された仕様として確定する: (1) 死亡時=残す、ゲームオーバー（Life=0 の全リセット）時=残すか消すかを設計書で明文化、(2) ニューゲーム/セーブ削除時の手記の扱いを定義、(3) export/import に手記データを含める（SaveData への統合 or 独立キーの正式仕様化 + バージョン/マイグレーション方針）
- **影響ファイル**: `src/contexts/JournalContext.tsx`, `src/types/saveTypes.ts`, `src/domain/save/logic/saveManager.ts`, `src/constants/saveConstants.ts`, `src/ui/html/battleHtml/BattleScreen.tsx`, `src/ui/html/characterSelectHtml/CharacterSelect.tsx`, `src/ui/html/componentsHtml/SettingsPanels/SaveLoadUI.tsx`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: SaveData に統合する場合は SAVE_VERSION（現 1.1.0、saveConstants.ts:8）のマイグレーション追加が必要。独立キー方式を維持する場合は export/import の二重管理に注意

#### JNL-6: ギルドの噂→手記初期ヒントの接続（敵情報ヒント型 Rumor 新設） — `new` / M

- **現状**: 噂は8種すべてレート/割引/開始ボーナス型（src/constants/data/camps/RumorData.ts:10-99、effect type は elite_rate / shop_discount / treasure_rate / start_bonus のみ）。敵情報に関する効果型が存在しない。GuildContext の activeRumors は useState のみで永続化なし（src/contexts/GuildContext.tsx:69-105、localStorage 不使用を grep で確認）。噂と手記の間に接続コードはゼロ
- **要求**: concept-v2 §3 B2「ギルドの噂が敵情報の初期ヒント」に基づき、(1) 敵情報ヒント型の Rumor effect（例: type: "enemy_intel", enemyId, intelLevel）を campTypes / RumorData に追加、(2) 噂の購入時に対象敵の観察記録へ初期ヒント（観察レベルの底上げ or 「噂」フラグ付き知識）を書き込む接続を GuildContext→Journal 側に実装、(3) ヒントは手記に永続記録されるため噂の duration 失効と独立させる
- **影響ファイル**: `src/constants/data/camps/RumorData.ts`, `src/types/campTypes.ts`, `src/contexts/GuildContext.tsx`, `src/contexts/JournalContext.tsx`, `src/ui/html/campsHtml/Guild/RumorsTab.tsx`
- **設計書**: .claude/docs/camp_document/guild_design.md / .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: Provider 階層は GuildProvider が最下層・JournalProvider が上位なので hook 参照方向は問題なし。ただし噂の在庫・価格はステージ制移行（B3 の推奨等級）と連動するため camp 領域との調整が要る。activeRumors 自体が非永続（リロードで消える）な点は別途 camp 領域の課題

#### JNL-7: 敵図鑑のステージ制対応（depth 分類→ステージ分類） — `modify` / M

- **現状**: 図鑑は enemyDepth1〜5 を ENEMIES_BY_DEPTH（src/constants/data/camps/EnemyEncyclopediaData.ts:8-24）で深度別に束ね、getEnemiesByDepth(depth: Depth) で参照。MemoriesPage の敵一覧も全深度フラット表示（MemoriesPage.tsx:244 getAllEnemies()）
- **要求**: concept-v2 §3 B1/B3「複数ダンジョン（ステージ）×テーマ別階層、ギルドが推奨等級を掲示」に基づき、図鑑の分類軸を深度からステージへ変更し、ステージ別タブ/フィルタと推奨等級（歪みの濃さ）の表示を図鑑エントリに反映する。まず2ステージ分
- **影響ファイル**: `src/constants/data/camps/EnemyEncyclopediaData.ts`, `src/constants/data/journal/EnemyEncyclopediaData.ts`, `src/ui/html/journalHtml/pages/MemoriesPage.tsx`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md / .claude/docs/enemy_document/ (敵データのステージ再編に追従)
- **リスク**: dungeon 領域のステージ構造（敵データの再編成）が先に確定する必要がある。依存順: dungeon 領域→本ギャップ

#### JNL-8: 戦闘 UI の敵情報マスキング（観察レベル連動） — `modify` / M

- **現状**: EnemyFrame が初見の敵でも正確な HP 数値を常時表示（src/ui/html/battleHtml/EnemyFrame.tsx:236-239 で {state.hp}/{state.maxHp} を直接描画）。観察状態による表示制御は一切ない
- **要求**: concept-v2 §1 副柱3「敵ステータスは初見では非開示。観察・経験で判明」に基づき、戦闘 UI の敵情報（HP 数値・速度等）を手記の観察レベルでゲートする（例: 未知=バーのみ/数値「???」、観察済み=数値開示）。手記領域は観察レベルの参照 API を提供し、表示判定ロジックを供給する
- **影響ファイル**: `src/ui/html/battleHtml/EnemyFrame.tsx`, `src/ui/html/battleHtml/BattleScreen.tsx`, `src/domain/journal/ (観察レベル参照 API)`
- **設計書**: .claude/docs/battle_document/battle_logic.md / .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: 戦闘 UI は battle 領域の再設計（新戦闘コア + PixiJS Phase 2-4）と重なるため、担当境界の調整が必要。本領域の責務は「観察レベルの提供」まで、表示実装は新戦闘 UI 側に委ねるのが妥当

#### JNL-9: journal_document 設計書の A1 対応改訂（V3.0→V4.0） — `modify` / S

- **現状**: .claude/docs/journal_document/journal_system_implementation_plan.md（V3.0、2026-02-05）は §3.2 で「Monsters: Encountered enemy stats, patterns, weaknesses」「Kill count and drop rates」を謳うが、観察レベルの段階モデル・死亡時の知識引き継ぎルール・噂ヒントの記載がない。さらに実装と乖離（設計書は Set/Map ベースの EncyclopediaData・PlayerNote の title/tags・セーブスロット3-5を記載、実装は ID 配列・title なしメモ20件上限・単一セーブスロット）
- **要求**: concept-v2 §5 合格基準「設計書と実装の差分ゼロ（新設計書を先に更新）」+ §3 A1/B2 に基づき、設計書を先行改訂する: 観察レベル定義、戦闘イベント→記録のトリガー仕様、死亡/ゲームオーバー/ニューゲーム時の手記ライフサイクル、噂ヒント仕様、現実装との乖離解消（実装に寄せるか設計に寄せるか明記）
- **影響ファイル**: `.claude/docs/journal_document/journal_system_implementation_plan.md`, `.claude/docs/INDEX.md`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: JNL-1〜8 の実装に先行して書く必要がある（設計書駆動）。battle/dungeon 領域の設計確定待ちの項目は TBD 節として明示する

#### JNL-10: discoverEquipment / discoverEvent の配線（図鑑の網羅性回復） — `modify` / S

- **現状**: discoverEquipment（JournalContext.tsx:223）と discoverEvent（JournalContext.tsx:230）も呼び出し箇所ゼロ。MemoriesPage の装備・イベントセクションは常に「未入手」「未達成」表示（MemoriesPage.tsx:331-426, 432-543）
- **要求**: concept-v2 §2 体験の柱2（観察と学習）の補助。A1 の主対象は敵情報だが、図鑑全体が機能していないと「手記に知識が蓄積される」体験が成立しないため、装備入手時（InventoryContext）・イベント遭遇時（dungeon イベント処理）に discovery を配線する
- **影響ファイル**: `src/contexts/InventoryContext.tsx`, `src/domain/dungeon/ (イベント処理箇所)`, `src/contexts/JournalContext.tsx`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: 優先度は敵観察（JNL-1〜4）より低い。Tier 2 扱いで後回し可

#### JNL-11: 手記の Thoughts（自由メモ）/ Tactics / Settings ページの維持 — `keep` / S

- **現状**: ThoughtsPage のメモ CRUD（JournalContext.tsx:180-206、20件上限・localStorage 永続）、TacticsPage のデッキ閲覧/編集、SettingsPage の設定・セーブ UI は実装済みで動作している。探索中も ExplorationJournalTab.tsx 経由で memories/thoughts にアクセス可能
- **要求**: concept-v2 §4「Journal は流用」に基づきそのまま維持。自由メモは体験の柱2「観察と学習」のプレイヤー主体の記録手段としてコンセプトと整合する。メモも死を越えて残る挙動（JNL-5 のライフサイクル定義に含めて明文化）
- **影響ファイル**: `src/ui/html/journalHtml/pages/ThoughtsPage.tsx`, `src/ui/html/journalHtml/pages/SettingsPage.tsx`, `src/ui/html/journalHtml/JournalOverlay.tsx`
- **リスク**: トーン調整（ダーク→ライト幻想寄り）で Journal の見た目（羊皮紙テクスチャ等、src/ui/css/journal/）の微修正が UI 領域から入る可能性のみ

### 7.6 キャンプ・経済（camp-economy / 11件）

**現状サマリ**: キャンプは5施設構成（Guild/Shop/Blacksmith/Sanctuary/DungeonGate、src/ui/html/campsHtml/）で、経済は ResourceContext.tsx の gold dual-pool（baseCamp/exploration）+ 魔石4種（small30/medium100/large350/huge1000、src/constants/itemConstants.ts:18-23）+ ソウル（src/domain/camps/logic/soulSystem.ts）の3層。回復経済は現状「実質無料」: キャンプ帰還時に resetRuntimeState（src/contexts/PlayerContext.tsx:390-402）で HP が無償全回復し、ダンジョン内 rest ノードも無料で20-30%回復（src/domain/dungeon/logic/nodeEventLogic.ts:36）。ポーションは50〜500G（src/constants/data/items/ConsumableItemData.ts）だが設計書（docs/item_document/EQUIPMENT_AND_ITEMS_DESIGN.md:513-516）と価格・効果に差分がある。ギルドの噂（src/constants/data/camps/RumorData.ts）は全て探索バフ型で敵情報ヒントは皆無、しかも RumorsTab.tsx がローカル state で購入管理し GuildContext.activateRumor は未使用、噂効果（elite_rate 等）はゲーム内のどこにも適用されていない「飾り」状態。B3 関連では DEPTH_DISPLAY_INFO の recommendedLevel（src/constants/dungeonConstants.ts:54-90）と昇格試験の等級（src/constants/data/camps/PromotionData.ts）が既存資産として存在する。

#### CAMP-1: キャンプ帰還の無償全回復を廃止し、有料回復（宿場町の宿・治療）へ置き換え — `modify` / M

- **現状**: 探索開始時に resetRuntimeState() が HP を maxHp に無償リセット（src/contexts/PlayerContext.tsx:390-402、呼び出し元 src/ui/html/dungeonHtml/DungeonGate.tsx:41）。camp_facilities_design.md V3.2 で Inn（宿）施設は削除済みで、キャンプに有料回復の手段が存在しない
- **要求**: A2「魔法は希少・回復は高価」+ 副柱「結果は不可逆に残る」。HP は帰還しても自動回復せず、ゴールドを払って宿/治療で回復する経済に変更。B2「宿場町」再解釈とセットで宿機能を導入
- **影響ファイル**: `src/contexts/PlayerContext.tsx`, `src/ui/html/dungeonHtml/DungeonGate.tsx`, `src/constants/campConstants.ts`, `src/ui/html/campsHtml/Guild/Guild.tsx`
- **設計書**: docs/camp_document/camp_facilities_design.md / docs/camp_document/guild_design.md
- **リスク**: HP が持ち越しになると詰み（金欠+瀕死）が発生しうる。最低限の無料回復ラインか救済措置の設計が必要。新戦闘コアのスタミナ（戦闘終了でリセット）との役割分担を設計書で明確化すること

#### CAMP-2: 回復アイテムの再価格付けと設計書差分の解消 — `modify` / S

- **現状**: 実装: 下級50G/+30、中級80G/+60、上級150G/+100、完全500G/full（src/constants/data/items/ConsumableItemData.ts:13-70）。設計書は Small 50G/+30、Medium 100G/+60、Large 200G/+100+デバフ1解除、Full 500G/全回復+全解除（docs/item_document/EQUIPMENT_AND_ITEMS_DESIGN.md:513-516）で価格・効果が不一致。さらに中級の description は「Restores 30 HP」だが effect は 60（ConsumableItemData.ts:31-33）、上級も desc 60 に対し effect 100 とテキスト不整合あり
- **要求**: A2「回復は高価」: 回復ポーション全種（resurrection_stone 800G、combo_elixir 700G 含む）を高価格帯へ再価格付け。合格基準「設計書と実装の差分ゼロ」のため、設計書を先に更新してから実装を合わせる
- **影響ファイル**: `src/constants/data/items/ConsumableItemData.ts`, `src/constants/data/camps/ShopStockConstants.ts`
- **設計書**: docs/item_document/EQUIPMENT_AND_ITEMS_DESIGN.md / docs/camp_document/shop_design.md
- **リスク**: 価格だけ上げて在庫（PERMANENT_SHOP_ITEMS の maxStock）を据え置くとバランスが二重に締まる。再価格はステージ報酬側の調整と同時にプレイテストすること

#### CAMP-3: 噂システムを「敵情報の初期ヒント」型へ再設計（Journal 接続） — `replace` / M

- **現状**: RUMORS は8種全てが探索バフ（elite_rate/shop_discount/treasure_rate/start_bonus、src/constants/data/camps/RumorData.ts:10-99）で敵情報に関するものはゼロ。敵図鑑は isEncountered: true 固定で全敵が最初から閲覧可能（src/constants/data/camps/EnemyEncyclopediaData.ts:57）
- **要求**: B2「ギルドの噂が敵情報の初期ヒント」+ 副柱「敵の情報は観察によってのみ得られる」+ A1「手記は死を越える」。噂の購入で特定の敵/ステージの部分情報（弱点・行動傾向など）が手記（Journal）に初期ヒントとして記録される enemy_hint 型の効果を新設し、観察システムの入口にする
- **影響ファイル**: `src/constants/data/camps/RumorData.ts`, `src/types/campTypes.ts`, `src/ui/html/campsHtml/Guild/RumorsTab.tsx`, `src/contexts/GuildContext.tsx`
- **設計書**: docs/camp_document/guild_design.md / docs/journal_document/
- **リスク**: 敵の観察・知識蓄積システム本体（journal/battle 領域）の設計が先行しないとヒントのデータ形式が決められない。依存順に注意

#### CAMP-4: 噂の状態管理の一本化と効果未適用バグの解消 — `modify` / S

- **現状**: RumorsTab.tsx がローカル state（purchasedIds/activeRumors、src/ui/html/campsHtml/Guild/RumorsTab.tsx:21-24）で購入を管理し、GuildContext.activateRumor（src/contexts/GuildContext.tsx:88-106）は UI から一度も呼ばれていない。噂効果（elite_rate/shop_discount/treasure_rate/start_bonus）は型定義（src/types/campTypes.ts:184-187）以外に参照箇所がなく、ゴールドを払っても探索・ショップに何の効果も及ばない
- **要求**: コンセプト前提の「状態単一所有」原則 + B2 再設計の前提整備。噂の状態は GuildContext に一本化し、効果が実際に適用される（または CAMP-3 で旧効果型ごと廃止する）こと。技術的負債は再設計着手前に返済する方針（§5）に該当
- **影響ファイル**: `src/ui/html/campsHtml/Guild/RumorsTab.tsx`, `src/contexts/GuildContext.tsx`
- **設計書**: docs/camp_document/guild_design.md
- **リスク**: CAMP-3 で噂を置き換えるなら旧効果の適用実装は無駄になる。CAMP-3 の方針確定後に「一本化のみ実施・効果は新型で実装」とするのが安全

#### CAMP-5: ギルドに各ステージの推奨等級掲示ボードを新設 — `new` / M

- **現状**: 推奨値は DEPTH_DISPLAY_INFO.recommendedLevel（1/5/10/15/20、src/constants/dungeonConstants.ts:54-90）として存在するが、表示は DungeonGate の深度選択（src/ui/html/dungeonHtml/DungeonGate.tsx:109-112）のみで、ギルドには掲示がない。一方ギルドには昇格試験による冒険者等級（見習い剣士→剣士→剣豪→剣聖→剣神、src/constants/data/camps/PromotionData.ts:8-79）が実装済み
- **要求**: B3「難易度＝歪みの濃さ。ギルドが各ステージの推奨等級を掲示。どこからでも入れるが難易度が違う」。ギルド（および新ステージ選択画面）に各ステージの推奨等級を掲示し、プレイヤーの現在等級（昇格試験の等級を流用）と対比できる UI を追加。掲示場所の本命はギルド、ステージ選択画面にも併記
- **影響ファイル**: `src/ui/html/campsHtml/Guild/Guild.tsx`, `src/constants/data/camps/PromotionData.ts`, `src/ui/html/dungeonHtml/DungeonGate.tsx`, `src/constants/dungeonConstants.ts`
- **設計書**: docs/camp_document/guild_design.md / docs/danjeon_document/
- **リスク**: ステージ定義（B1、dungeon 領域）のデータ形式が先に必要。推奨等級の単位を「冒険者等級」にするか数値レベルにするかをコンセプト段階で決めること

#### CAMP-6: キャンプ→宿場町の再解釈に伴う施設フレーバー更新とライト幻想トーン調整 — `modify` / M

- **現状**: 施設説明は英語ダークトーン（例: "Descend into the depths and face your destiny"、src/constants/campConstants.ts:24-28）。SanctuaryData.ts の全ノード name/description、RumorData.ts の噂テキスト、RumorsTab.tsx の UI ラベル（"Purchase"/"runs left"）も英語で、UI text Japanese 規約にも違反。BaseCamp.tsx は焚き火中心の野営地ビジュアル（src/ui/html/campsHtml/BaseCamp.tsx:79-91）
- **要求**: B2「キャンプ＝宿場町（ダンジョン群の中継地）」+ トーン「ダークファンタジーをライト幻想寄りに調整」。施設名・説明・フレーバーを宿場町の世界観で書き直し（日本語化を含む）、BaseCamp の演出も野営地→宿場町に再解釈
- **影響ファイル**: `src/constants/campConstants.ts`, `src/constants/data/camps/SanctuaryData.ts`, `src/constants/data/camps/RumorData.ts`, `src/constants/data/camps/GameTipsData.ts`, `src/ui/html/campsHtml/BaseCamp.tsx`, `src/ui/html/campsHtml/Guild/RumorsTab.tsx`
- **設計書**: docs/camp_document/camp_facilities_design.md / docs/ui_ux_design_guide.md
- **リスク**: テキスト変更は広範囲（ポーション説明・tips・試験文言にも波及）。トーンガイドライン（ui_ux_design_guide.md 系）を先に1枚決めてから一括適用しないと表記が混在する

#### CAMP-7: 経済基盤（dual-pool/魔石/ソウル/在庫/鍛冶）は不可逆性の土台として流用 — `keep` / S

- **現状**: gold dual-pool と死亡時の探索リソース喪失（src/contexts/ResourceContext.tsx:307-367 transferExplorationToBaseCamp/resetExplorationResources）、魔石交換（src/domain/camps/logic/shopLogic.ts:53-94）、ソウル恒久強化（src/domain/camps/logic/soulSystem.ts、sanctuaryLogic.ts）、在庫補充（src/domain/camps/logic/shopStockLogic.ts）、鍛冶（src/domain/camps/logic/blacksmithLogic.ts）が稼働中
- **要求**: §4「方針 B: キャンプ実装は流用」+ 副柱「結果は不可逆に残る」。死で探索分を失う dual-pool 構造は不可逆性をすでに体現しており、そのまま新コアの経済土台にする
- **影響ファイル**: `src/contexts/ResourceContext.tsx`, `src/domain/camps/logic/soulSystem.ts`, `src/domain/camps/logic/shopStockLogic.ts`, `src/domain/camps/logic/blacksmithLogic.ts`
- **設計書**: docs/camp_document/shop_design.md
- **リスク**: 特になし。ただし CAMP-1 の有料回復が入ると金の需要曲線が変わるため、報酬量の再バランスは別途必要

#### CAMP-8: Quest と Dark Market は凍結（現状維持・拡張しない） — `keep` / S

- **現状**: Quest はギルドのタブとして稼働中（src/constants/data/camps/QuestData.ts、src/ui/html/campsHtml/Guild/QuestsTab.tsx、GuildContext.tsx:127-206）。Dark Market もショップのタブとして稼働中（src/ui/html/campsHtml/Shop/DarkMarketTab.tsx、src/constants/data/camps/DarkMarketConstants.ts、src/constants/campConstants.ts:39）
- **要求**: §5 スコープ境界「Quest / Dark Market は凍結（廃棄しない）」。既存実装は残すが再設計の対象にせず、新機能追加もしない。CAMP-6 のトーン調整時に最小限のテキスト修正のみ許容
- **影響ファイル**: `src/constants/data/camps/QuestData.ts`, `src/ui/html/campsHtml/Guild/QuestsTab.tsx`, `src/ui/html/campsHtml/Shop/DarkMarketTab.tsx`, `src/constants/data/camps/DarkMarketConstants.ts`
- **設計書**: docs/camp_document/guild_design.md / docs/camp_document/shop_design.md
- **リスク**: ステージ制移行（B1）で Quest の対象（深度依存の敵 ID）や Dark Market の depth 依存ロジックが壊れる可能性。凍結でも最低限の追従修正は発生しうる

#### CAMP-9: Sanctuary「Boon of Recovery」の死に効果解消と設計書差分修正 — `modify` / S

- **現状**: boon_recovery ノード（src/constants/data/camps/SanctuaryData.ts:151-169）は「Recover 5% HP when resting at camp」だが、hpRecoveryPercent は集計されるだけ（src/domain/camps/logic/sanctuaryLogic.ts:145）でどこにも適用されていない死に効果。設計書では「Recover +5% HP after combat」（docs/camp_document/sanctuary_design.md:206）と記述も食い違う
- **要求**: 合格基準「設計書と実装の差分ゼロ」+ A2。CAMP-1 の有料回復導入と接続して「宿の回復量/割引に作用する恒久強化」として再定義するか、削除する。回復が高価になる世界では回復系の恒久強化は価値が上がるため再設計の好機
- **影響ファイル**: `src/constants/data/camps/SanctuaryData.ts`, `src/domain/camps/logic/sanctuaryLogic.ts`, `src/types/campTypes.ts`
- **設計書**: docs/camp_document/sanctuary_design.md
- **リスク**: CAMP-1 の宿仕様が決まる前に着手すると手戻り。順序は CAMP-1 → CAMP-9

#### CAMP-10: ダンジョン内 rest ノードの無料回復を回復経済の再価格付けに含める — `modify` / S

- **現状**: rest ノードは無料で maxHp の20-30%を回復（src/domain/dungeon/logic/nodeEventLogic.ts:33-47）、出現率 restChance 0.1（src/constants/dungeonConstants.ts:102）。キャンプ外の無料回復源として A2 と緊張関係にある
- **要求**: A2「回復は高価」。再価格付けの範囲に rest ノードを含め、回復量の縮小・コスト化（食料/時間消費）・スタミナ回復への役割変更のいずれかを新ステージ設計（B1、dungeon 領域）と整合させて決める
- **影響ファイル**: `src/domain/dungeon/logic/nodeEventLogic.ts`, `src/constants/dungeonConstants.ts`
- **設計書**: docs/danjeon_document/ / docs/camp_document/camp_facilities_design.md
- **リスク**: 主担当は dungeon 領域。回復経済全体（ポーション・宿・rest）の総量を一枚の表で管理しないと個別調整で破綻する

#### CAMP-11: ショップ在庫・価格の Depth 依存をステージ ID 依存へ移行 — `modify` / S

- **現状**: EPIC_RATE_BY_DEPTH が Depth 型（1-5）にハードコード（src/constants/data/camps/ShopStockConstants.ts:9-15）、shopStockLogic.ts も Depth を受け取る。Dark Market・装備日替わり生成（src/constants/data/camps/ShopData.ts:143-181）も同様に深度前提
- **要求**: B1「複数ダンジョン（ステージ）×テーマ別複数階層」への構造変更。深度 1-5 を前提にした在庫・レアリティテーブルを、ステージ ID（まず2ステージ）+ 歪みの濃さ（B3）をキーにした形へ置き換える
- **影響ファイル**: `src/constants/data/camps/ShopStockConstants.ts`, `src/domain/camps/logic/shopStockLogic.ts`, `src/types/campTypes.ts`
- **設計書**: docs/camp_document/shop_design.md / docs/danjeon_document/
- **リスク**: ステージ型定義（dungeon 領域）の確定が前提。Depth 型は src/types/ 全体に波及しているため、型置換は全領域横断で一斉に行う必要がある

### 7.7 不可逆性（irreversibility / 12件）

**現状サマリ**: 死亡ペナルティは deathHandler（src/domain/battles/logic/deathHandler.ts:38）が「全インベントリ・装備喪失 / 倉庫と拠点資源は保持 / ソウル100%移管」を担い、BattleScreen の useEffect（src/ui/html/battleHtml/BattleScreen.tsx:198-215）が decreaseLives と探索資源リセットを実行する。ライフは難易度別 3/3/2（src/domain/characters/player/logic/playerUtils.ts:16-20）で RuntimeBattleState（src/contexts/PlayerContext.tsx:97）のメモリ内のみに存在し、セーブデータ（src/types/saveTypes.ts:51-58, SAVE_VERSION 1.1.0）には含まれない。セーブは手動（SaveLoadUI.tsx:34）と新規ゲーム時（CharacterSelect.tsx:91）のみで、UI が謳う「拠点帰還時の自動セーブ」（SaveLoadUI.tsx:175）は未実装のため、死亡直後にリロードすれば全ペナルティが巻き戻る＝不可逆性が実質機能していない。生還時の確定処理も transferSouls / completeSurvival / transferExplorationToBaseCamp（ResourceContext.tsx:307）が全て未配線。ライフ0のゲームオーバーは deleteSave + character_select 遷移のみ（BattleScreen.tsx:484-488）で、Journal は独立 localStorage キー（JournalContext.tsx:28-31）のため偶然「死を越えて」残っており、A1 はほぼ実装済みだが仕様として未確定。回復はダンジョン入場時の無料フルヒール（PlayerContext.tsx:390-402）とレストノード無料20-30%回復（nodeEventLogic.ts:35-49）があり、A2「回復は高価」と正面衝突している。

#### IRR-1: イベント駆動の自動セーブ実装（セーブスカム防止の土台） — `new` / M

- **現状**: saveManager.save の呼び出しは新規ゲーム時（src/ui/html/characterSelectHtml/CharacterSelect.tsx:91）と設定画面の手動セーブ（src/ui/html/componentsHtml/SettingsPanels/SaveLoadUI.tsx:34）のみ。SaveLoadUI.tsx:175 は「拠点に戻った時に自動でセーブされます」と表示するが実装が存在しない。GameStateContext（src/contexts/GameStateContext.tsx:98-105）の returnToCamp も画面遷移のみ
- **要求**: コンセプト副柱「結果は不可逆に残る」（concept-v2 §1 優先2）。死亡・生還・購入/強化などの確定イベントで自動保存し、リロードによる巻き戻し（任意のセーブ＆リロードで死を無かったことにできる現状）を塞ぐ。手動セーブは廃止または「中断保存」に限定する方針も要検討
- **影響ファイル**: `src/domain/save/logic/saveManager.ts`, `src/contexts/GameStateContext.tsx`, `src/ui/html/componentsHtml/SettingsPanels/SaveLoadUI.tsx`, `src/App.tsx`
- **設計書**: .claude/docs/Overall_document/save_and_irreversibility_design.md（新規） / .claude/docs/Overall_document/game_design_master.md
- **リスク**: 全コンテキストの状態を1箇所で集約してシリアライズする必要があり、状態単一所有の原則とどう両立させるか（保存トリガーの集約点）の設計が要る。保存タイミング漏れがあると逆に不整合セーブが生まれる

#### IRR-2: ライフ・難易度のセーブデータ永続化 — `modify` / S

- **現状**: lives と difficulty は RuntimeBattleState（src/contexts/PlayerContext.tsx:96-99）のメモリ内のみ。SaveData（src/types/saveTypes.ts:51-58）に存在せず、loadFromSaveData（PlayerContext.tsx:456-463）は createInitialRuntimeState でライフを満タンに再生成する。-1ライフのペナルティはページリロードで消える
- **要求**: コンセプト副柱「死・喪失が重い」（concept-v2 §1 優先2）。ライフと難易度をセーブデータに含め、ロード時に復元する。死の重さがセッションを跨いで持続することが不可逆性の最低条件
- **影響ファイル**: `src/types/saveTypes.ts`, `src/domain/save/logic/saveManager.ts`, `src/contexts/PlayerContext.tsx`, `src/ui/html/componentsHtml/SettingsPanels/SaveLoadUI.tsx`, `src/ui/html/characterSelectHtml/CharacterSelect.tsx`
- **設計書**: .claude/docs/Overall_document/DESIGN_CHANGE_PLAN_lives_system.md / .claude/docs/Overall_document/save_and_irreversibility_design.md（新規）
- **リスク**: IRR-7 のスキーマ再設計と統合して一度にやらないと migrate() が多段化して複雑になる

#### IRR-3: 死亡確定処理の一本化（ペナルティ適用＋ライフ減＋即時保存） — `modify` / M

- **現状**: 死亡処理が BattleScreen の useEffect（src/ui/html/battleHtml/BattleScreen.tsx:198-215）に散在: handlePlayerDeathWithDetails → updatePlayerData → resetExplorationResources → decreaseLives を個別に呼び、保存はしない。ドメインロジック deathHandler.ts:38 は純関数だが、確定（永続化）の責務を持つ場所がない
- **要求**: コンセプト副柱「結果は不可逆に残る」。死亡を「ペナルティ適用＋ライフ減＋自動セーブ」の1つの確定トランザクションとしてドメイン層に集約し、UI 層（BattleScreen）からは1呼び出しにする。新戦闘コア（剣士再設計）の敗北フローからもそのまま呼べる形にする
- **影響ファイル**: `src/domain/battles/logic/deathHandler.ts`, `src/ui/html/battleHtml/BattleScreen.tsx`, `src/domain/save/logic/saveManager.ts`, `src/contexts/PlayerContext.tsx`
- **設計書**: .claude/docs/danjeon_document/return_system_design.md / .claude/docs/Overall_document/save_and_irreversibility_design.md（新規）
- **リスク**: 複数コンテキスト（Player/Resource/Inventory）に跨る更新を1トランザクション化する際、React の setState 非同期性により保存タイミングがズレる既知パターン（CLAUDE.md の Mutable result pattern 参照）に注意

#### IRR-4: ゲームオーバー（ライフ0＝フルリセット）処理の整備 — `modify` / M

- **現状**: src/ui/html/battleHtml/BattleScreen.tsx:484-488 で deleteSave + setDifficulty("normal") + character_select 遷移のみ。Player/Resource/Inventory/Guild/DungeonRun の各コンテキストはメモリに残留したまま。設計書側でも DESIGN_CHANGE_PLAN_lives_system.md:52 で「Game Over screen (Full Reset process)」が Pending のまま
- **要求**: コンセプト副柱「死・喪失が重い」の最終形。ライフ0で探索者は完全に失われる（フルリセット）が、A1 により手記＝知識だけは残る、という落差を仕様・演出として明確化。専用のゲームオーバー画面と全コンテキストの初期化処理を実装する
- **影響ファイル**: `src/ui/html/battleHtml/BattleScreen.tsx`, `src/ui/html/battleHtml/DefeatScreen.tsx`, `src/contexts/PlayerContext.tsx`, `src/contexts/ResourceContext.tsx`, `src/contexts/GuildContext.tsx`, `src/contexts/DungeonRunContext.tsx`
- **設計書**: .claude/docs/Overall_document/DESIGN_CHANGE_PLAN_lives_system.md / .claude/docs/Overall_document/save_and_irreversibility_design.md（新規）
- **リスク**: コンテキスト初期化漏れがあると「前の探索者の状態」が新ゲームに漏れる。逆に Journal まで消すと A1 が壊れる。何を消し何を残すかの一覧表を設計書に先行定義すべき

#### IRR-5: A1「手記は死を越える」の仕様確定と Journal 永続化の統合 — `modify` / S

- **現状**: Journal の notes/discovery は独立 localStorage キー journal_notes / journal_discovery（src/contexts/JournalContext.tsx:28-31）で保存され、ゲームオーバー時の deleteSave（BattleScreen.tsx:486）でも New Game（CharacterSelect.tsx:82-137）でも消えない。つまり A1 は「偶然」成立しているが、仕様として明文化されておらず、Journal 側には version もマイグレーション機構もない
- **要求**: concept-v2 §3 A1「手記は死を越える — 観察した敵情報は手記に蓄積。探索者が死んでも手記＝知識だけは次に引き継がれる」。これを意図された仕様として設計書に明文化し、Journal ストレージに version を導入。【論点提示】フルリセット/ニューゲームでも手記を残すのが A1 の素直な解釈（現挙動と一致）だが、「完全な新規スタート（手記も消す）」オプションを設定に置くか否かはユーザー判断が必要
- **影響ファイル**: `src/contexts/JournalContext.tsx`, `src/types/journalTypes.ts`, `src/ui/html/characterSelectHtml/CharacterSelect.tsx`, `src/ui/html/componentsHtml/SettingsPanels/SaveLoadUI.tsx`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md / .claude/docs/Overall_document/save_and_irreversibility_design.md（新規）
- **リスク**: 観察システム（情報領域）が Journal に蓄積するデータ構造を拡張する場合、その領域と保存形式のすり合わせが必要。SaveData に取り込むか独立キーのままにするかで IRR-7 の設計が変わる

#### IRR-6: 生還時の獲得確定処理の配線（現在は未接続） — `modify` / M

- **現状**: transferSouls（src/domain/characters/player/hooks/usePlayerProgression.ts:53）、completeSurvival（src/domain/camps/logic/soulSystem.ts:121）、transferExplorationToBaseCamp（src/contexts/ResourceContext.tsx:307）はいずれも呼び出し箇所ゼロ。Depth クリア時は retreatFromDungeon + returnToCamp のみ（src/ui/html/dungeonHtml/NodeMap.tsx:393-399）で、currentRunSouls と探索ゴールド/魔石は exploration プールに残ったまま確定イベントが発生しない。設計書 return_system_design.md の「Return = 100% Carry-back」と実装が乖離
- **要求**: コンセプト副柱「結果は不可逆に残る」— 死だけでなく生還も「結果の確定」。生還時に souls 移管＋探索資源の拠点移動＋自動セーブを1つの確定処理として配線する。新ステージ制（B1: 2ステージ）のクリア/帰還フローの確定点としてそのまま流用できる形にする
- **影響ファイル**: `src/ui/html/dungeonHtml/NodeMap.tsx`, `src/contexts/ResourceContext.tsx`, `src/domain/characters/player/hooks/usePlayerProgression.ts`, `src/domain/camps/logic/soulSystem.ts`
- **設計書**: .claude/docs/danjeon_document/return_system_design.md / .claude/docs/camp_document/sanctuary_design.md
- **リスク**: 帰還ルート/転移石（dungeon 領域）の再設計と帰還トリガーが重なるため、確定処理のインターフェースだけ先に切って dungeon 領域と分担を合意する必要がある

#### IRR-7: SaveData v2.0 スキーマ再設計とマイグレーション — `modify` / L

- **現状**: SAVE_VERSION は 1.1.0（src/constants/saveConstants.ts:8）。SaveData は player/resources/inventory/progression の4区画のみ（src/types/saveTypes.ts:51-58）で、ライフ・難易度・カード熟練度（cardMasteryStore）・派生解放カード（unlockedCardTypeIds）・ギルド状態・Journal が未保存。migrate()（src/domain/save/logic/saveManager.ts:176-216）は 1.0.0→1.1.0 の1段のみ。ProgressionSaveData.unlockedDepths は深度制前提
- **要求**: concept-v2 §3 B1（複数ステージ制）・§1（スタミナ等の新戦闘コア）・A1（手記）を収容できる SaveData v2.0 を設計書先行で定義し、migrate() に 1.x→2.0 を追加。【後方互換の要否論点】N=1 プレイヤー（concept-v2 §6）かつコア再設計のため「旧セーブは破棄して新規開始」も合理的。マイグレーションを書くか破棄するかは要ユーザー判断（手記キーだけは温存推奨）
- **影響ファイル**: `src/types/saveTypes.ts`, `src/constants/saveConstants.ts`, `src/domain/save/logic/saveManager.ts`, `src/contexts/PlayerContext.tsx`, `src/ui/html/componentsHtml/SettingsPanels/SaveLoadUI.tsx`
- **設計書**: .claude/docs/Overall_document/save_and_irreversibility_design.md（新規） / .claude/docs/Overall_document/game_design_master.md
- **リスク**: 新戦闘コア・ステージ制の型が固まる前に着手すると手戻りする。各領域の要件確定後（実装プラン後半）に置くべき依存関係の強いタスク

#### IRR-8: デッキの保存・復元の修正（ロードで購入/派生カードが消える） — `modify` / M

- **現状**: deckCardIds は保存されるが、loadFromSaveData は常に classInfo.starterDeck で復元（src/contexts/PlayerContext.tsx:412-414、コメント「In a full implementation, we'd need to store cardTypeIds and reconstruct properly」）。購入・派生で得たカードはロードのたびに失われる。unlockedCardTypeIds（PlayerContext.tsx:315-324）も未保存
- **要求**: コンセプト体験の柱3「成長の手応え — カード熟練度・装備・技術の積み上げ」（concept-v2 §2）と不可逆性の両立: 積み上げた成長が正しく永続化されなければ「結果が残る」が成立しない。cardTypeId ベースでデッキと解放状態を保存・再構築する
- **影響ファイル**: `src/contexts/PlayerContext.tsx`, `src/types/saveTypes.ts`, `src/domain/save/logic/saveManager.ts`, `src/constants/data/cards/`
- **設計書**: .claude/docs/Overall_document/save_and_irreversibility_design.md（新規） / .claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md
- **リスク**: カード定義がデータ（src/constants/data/cards/）に分離されているため再構築自体は可能だが、不可侵コード deck.ts / deckReducer.ts に触れずにインスタンス再生成する経路を確認する必要がある

#### IRR-9: unlockedDepths のステージ進行記録への置き換え — `replace` / S

- **現状**: PlayerData.progression.unlockedDepths は常にハードコードで [1]（src/contexts/PlayerContext.tsx:508, 552）。保存はされる（saveTypes.ts:46）が更新する処理が存在せず、深度アンロック制は実質機能していない
- **要求**: concept-v2 §3 B3「難易度＝歪みの濃さ — どこからでも入れるが難易度が違う」により深度アンロック制自体が不要化。unlockedDepths を撤去し、代わりにステージ別のクリア/到達記録（ギルドの推奨等級掲示や実績表示に使う進行データ）へ置き換える
- **影響ファイル**: `src/contexts/PlayerContext.tsx`, `src/types/saveTypes.ts`, `src/types/campTypes.ts`, `src/domain/save/logic/saveManager.ts`
- **設計書**: .claude/docs/danjeon_document/dungeon_exploration_ui_design_v3.0.md / .claude/docs/Overall_document/save_and_irreversibility_design.md（新規）
- **リスク**: ステージ領域（B1）のステージ ID 設計が決まってから着手しないと型が二度変わる。IRR-7 と同一コミットで実施するのが安全

#### IRR-10: 回復経済の再設計（A2: 回復は高価・無料フルヒール廃止） — `modify` / M

- **現状**: ダンジョン入場時に resetRuntimeState が currentHp=maxHp で無料フルヒール（src/contexts/PlayerContext.tsx:390-402、呼び出しは src/ui/html/dungeonHtml/DungeonGate.tsx:41）。レストノードは無料で 20-30% 回復（src/domain/dungeon/logic/nodeEventLogic.ts:35-49）。回復薬は 30HP/50G〜60HP/150G（src/constants/data/items/ConsumableItemData.ts:13-69）と安価で、Sanctuary には「拠点休憩で 5% HP 回復」パーク（src/constants/data/camps/SanctuaryData.ts:154）もある
- **要求**: concept-v2 §3 A2「魔法は希少・回復は高価 — 治癒は貴重で回復が安くない世界律」+ §1 優先2「回復が安くない」+ B2「キャンプ＝宿場町」。拠点回復を有料化（宿泊費）または条件付きにし、レストノードと回復薬の価格・在庫・回復量を世界律に合わせて再設計。具体的な数値は設計書側で先に確定する（設計書駆動）
- **影響ファイル**: `src/contexts/PlayerContext.tsx`, `src/ui/html/dungeonHtml/DungeonGate.tsx`, `src/domain/dungeon/logic/nodeEventLogic.ts`, `src/constants/data/items/ConsumableItemData.ts`, `src/constants/data/camps/ShopStockConstants.ts`, `src/constants/data/camps/SanctuaryData.ts`
- **設計書**: .claude/docs/camp_document/shop_design.md / .claude/docs/camp_document/camp_facilities_design.md / .claude/docs/camp_document/sanctuary_design.md / .claude/docs/item_document/EQUIPMENT_AND_ITEMS_DESIGN.md
- **リスク**: 新戦闘コア（スタミナ・間合い）のダメージ収支が決まらないと回復単価のバランスが取れない。戦闘コア領域の数値確定後にバランス調整する2段構えが必要。HP が戻りにくい設計は難易度を直接押し上げるため、2ステージ通しプレイ検証（合格基準2）での調整幅を確保すること

#### IRR-11: ソウル100%継承と聖域成長の維持（死んでも残る成長） — `keep` / S

- **現状**: 死亡時に currentRunSouls を 100% totalSouls へ移管する V3.0 残滓システム（src/domain/camps/logic/soulSystem.ts:149-163、src/domain/battles/logic/deathHandler.ts:86-96）と、聖域の unlockedNodes / explorationLimitBonus の保持は実装済みで設計書（return_system_design.md V3.0）とも一致
- **要求**: concept-v2 §4「方針 B — 資産流用」: ソウル/聖域は「死で失われないメタ成長」として A1（手記）と対になる既存資産であり、そのまま流用する。新コンセプトでも「死で失うもの（持ち物・装備・探索資源・ライフ）/ 残るもの（手記・ソウル・聖域・倉庫）」の対比表の「残る側」として設計書に再掲して確定する
- **影響ファイル**: `src/domain/camps/logic/soulSystem.ts`, `src/domain/battles/logic/deathHandler.ts`, `src/constants/data/camps/SanctuaryData.ts`
- **設計書**: .claude/docs/danjeon_document/return_system_design.md / .claude/docs/camp_document/sanctuary_design.md
- **リスク**: IRR-2/IRR-7 でセーブに乗せ忘れると「残るはずのものが消える」事故になる（sanctuaryProgress 自体は既に SaveData に含まれている点は確認済み）

#### IRR-12: ラン中断＝ラン放棄の仕様化（DungeonRun は保存しない） — `modify` / S

- **現状**: DungeonRun（src/contexts/DungeonRunContext.tsx:72）はメモリのみで一切保存されず、リロードするとラン消滅。ただし現状は探索資源・HP も最後の手動セーブまで巻き戻るため「中断したら何が起きるか」が未定義の挙動になっている
- **要求**: コンセプト副柱「結果は不可逆に残る」をブラウザゲームの現実（タブを閉じられる）と両立させる仕様決め: ラン状態は保存せず「中断＝そのランを放棄（探索で得た未確定資源は失う、ライフは維持）」を正式仕様として設計書に明記し、IRR-1 の自動セーブ（ラン開始時に「ラン突入済み」フラグを保存）で整合させる。フルセーブ&リジューム実装より大幅に安く、ローグライクの定石とも一致
- **影響ファイル**: `src/contexts/DungeonRunContext.tsx`, `src/domain/save/logic/saveManager.ts`, `src/types/saveTypes.ts`
- **設計書**: .claude/docs/danjeon_document/return_system_design.md / .claude/docs/Overall_document/save_and_irreversibility_design.md（新規）
- **リスク**: 「ラン放棄でライフは減らさない」とするとリロードが実質無料の逃走手段になる。突入時点で探索持ち込み品を確定保存する等、抜け道の閉じ方は設計書で要検討（ライフ-1 とするかは難易度感に直結するためユーザー判断）

### 7.8 PixiJS 統合（pixijs-integration / 13件）

**現状サマリ**: PixiJS Phase 1（ハイブリッド描画基盤）は完了済み（archive/2026-05-17-pixijs-phase1-code-level.md Status=COMPLETED 2026-05-23）。src/ui/pixi/ に PixiStage.tsx（透過 WebGL キャンバス + StrictMode 回避）、BattleCanvas.tsx（BattleScreen.tsx:615 と GuildBattleScreen.tsx:262 の2箇所にマウント）、3レイヤー（BackgroundLayer/CharacterLayer は空プレースホルダ、EffectLayer はテスト粒子1個のみ）、PixiEffectBridge.ts（interface 宣言のみで実装・配線なし）がある。つまり現状「描けるもの」は固定座標のテスト粒子1個だけで、実バトル演出は全て DOM 側（src/ui/html/componentsHtml/useCardAnimation.tsx 401行 + src/ui/animations/animationEngine.ts 321行）が担っている。キャンバスは battle-layout.css:194-205 で .battle-field と同形状の固定オーバーレイ（z-index:15, pointer-events:none）、Pixi へ渡る状態は HP スカラーのみ（pixiTypes.ts:6-13、敵は aliveEnemies[0] のみ）。Phase 2-4 プラン（docs/vision/plans/pixijs_phase2〜4）は 2026-02-22 作成の旧版で、v8 非対応の @pixi/particle-emitter 依存（Phase 1 監査 §0B-3 で「死亡」判定、package.json にも未導入）、6属性エフェクト（魔術師前提）、5深度×50体アセット、ダークファンタジー・スタイルガイドなど、コンセプト（剣士先行・2ステージ・ライト幻想・間合い/体勢/スタミナ主柱）と衝突する前提を多数含む。間合いの視覚表現（キャラ位置移動）を Pixi でやる場合の前提は、(1) BattlePixiProps への距離/体勢/スタミナ伝搬、(2) 距離値→キャンバス座標の座標系モジュール（現状ハードコード EffectLayer.tsx:39）、(3) キャラ絵の DOM <img>（EnemyFrame/PlayerFrame）→ CharacterLayer スプライト移行、(4) ターゲット選択を Pixi で受ける場合の per-object eventMode 方針（usePixiApp.ts:25-41 で global move 無効化済み）の4点。

#### PIX-1: Phase 1 基盤（PixiStage / BattleCanvas / StrictMode 回避 / CSS レイヤリング）の維持 — `keep` / S

- **現状**: src/ui/pixi/core/PixiStage.tsx（透過 WebGL、resolution+autoDensity、preference:webgl）、src/ui/pixi/battle/BattleCanvas.tsx:33-66（React.lazy + マウントゲートで StrictMode issue #602 回避）、src/ui/css/pages/battle/battle-layout.css:194-205（z-index:15 オーバーレイ）、src/ui/pixi/core/usePixiApp.ts:25-41（pointermove 漏れ対策）。ビルド・テスト（src/ui/pixi/__tests__/pixiFoundation.test.ts）も整備済み
- **要求**: concept-v2 §4「PixiJS 基盤は流用」に該当。新戦闘コアの演出も同じ Application/レイヤー構成の上に載せるため、Phase 1 の成果物はそのまま土台として維持する
- **影響ファイル**: `src/ui/pixi/core/PixiStage.tsx`, `src/ui/pixi/core/usePixiApp.ts`, `src/ui/pixi/battle/BattleCanvas.tsx`, `src/ui/css/pages/battle/battle-layout.css`
- **リスク**: GuildBattleScreen.tsx が BattleScreen の構造を複製しているため（Phase 1 監査で確認済み）、BattleCanvas への変更は常に2箇所同期が必要

#### PIX-2: PixiEffectBridge の実体実装と戦闘フックへの配線 — `modify` / M

- **現状**: src/ui/pixi/battle/PixiEffectBridge.ts:14-26 は interface 宣言のみ。playDamageEffect/playHealEffect/playShieldEffect は未実装、実装クラス・インスタンス生成・呼び出し元が一切ない。EffectLayer.tsx:35-40 はマウント時にテスト粒子を自前発火するだけで Bridge を経由していない。実演出は DOM 側 useCardAnimation.tsx:230-360 が担当
- **要求**: concept-v2 §5「PixiJS Phase 2-4 は新戦闘コアの演出として統合」の最初の足場。間合い・体勢・スタミナ・観察のどの演出も、戦闘ロジック側から命令的に発火する Bridge 実装（エフェクトキュー含む）が前提になる
- **影響ファイル**: `src/ui/pixi/battle/PixiEffectBridge.ts`, `src/ui/pixi/battle/layers/EffectLayer.tsx`, `src/ui/pixi/battle/BattleCanvas.tsx`, `src/ui/html/componentsHtml/useCardAnimation.tsx`
- **設計書**: .claude/docs/battle_document/battle_presentation.md（新設、PIX-12）
- **リスク**: 新戦闘コア（useBattleOrchestrator 再設計）と同時進行になるため、Bridge の API は新コアのイベント語彙（間合い変化・体勢変化・スタミナ消費）を先に決めてから固める必要がある

#### PIX-3: BattlePixiProps の新戦闘コア状態（間合い・体勢・スタミナ・複数敵）への拡張 — `modify` / S

- **現状**: src/ui/pixi/types/pixiTypes.ts:6-13 は playerHp/enemyHp/isPlayerPhase/phaseCount のスカラーのみ。BattleScreen.tsx:615-622 では敵情報を aliveEnemies[0] の HP しか渡していない。間合い・体勢・スタミナに相当する型は src/types/battleTypes.ts にも存在しない（grep で distance/range/stance/stamina ヒットなし）
- **要求**: concept-v2 §1 主柱（間合い・体勢・スタミナ）の値を Pixi レイヤーで描画するための伝搬経路。距離値・体勢状態・スタミナ残量・敵ごとの位置を含むプロップ契約に拡張する
- **影響ファイル**: `src/ui/pixi/types/pixiTypes.ts`, `src/ui/html/battleHtml/BattleScreen.tsx`, `src/ui/html/campsHtml/Guild/GuildBattleScreen.tsx`, `src/ui/pixi/__tests__/pixiFoundation.test.ts`
- **設計書**: .claude/docs/battle_document/battle_logic.md
- **リスク**: 新戦闘コアの型定義（battle-core 領域）に依存。型が確定する前に進めると手戻りになる

#### PIX-4: 距離値→キャンバス座標の座標系モジュール新設 — `new` / S

- **現状**: 座標は EffectLayer.tsx:39 の emit(160, 120, ...) のようにハードコード。キャンバスは battle-layout.css:196-200 の固定 vh/vw 形状（top:18vh/left:5vw/90vw/51vh）に resizeTo で追従するだけで、論理座標→画面座標の変換層が存在しない
- **要求**: concept-v2 §2 体験の柱1「間合いの読み合い」の視覚化前提。距離（ドメイン値）→レーン上の x 座標へのマッピングを、キャンバス実寸（useApplication の screen サイズ）に応じてレスポンシブに解決するユーティリティが必要
- **影響ファイル**: `src/ui/pixi/shared/layout/laneCoordinates.ts（新規）`, `src/ui/pixi/battle/layers/EffectLayer.tsx`, `src/ui/pixi/battle/layers/CharacterLayer.tsx`
- **設計書**: .claude/docs/battle_document/battle_presentation.md（新設、PIX-12）
- **リスク**: B1 環境ルール（広さが間合いに影響）により、ステージごとにレーン長が可変になる可能性。マッピングはステージパラメータを引数に取る設計にしておく

#### PIX-5: 間合いレーン表示と距離変化アニメーション（新戦闘コアの中核演出） — `new` / L

- **現状**: 存在しない。CharacterLayer.tsx:5 は空コンテナ、BackgroundLayer.tsx:6 も空。既存プラン群（pixijs_phase2_battle_effects.md / phase3）にも間合い・レーンの概念は一切ない（属性パーティクルとスプライト浮遊のみ）
- **要求**: concept-v2 §1 優先1「戦闘のリアリティ＝間合い・体勢」の主要システム化、§5「Phase 2-4 を新戦闘コアの演出として統合」。レーン（距離目盛り）描画、前進/後退時のキャラ位置トゥイーン、現在間合いの強調表示を Pixi レイヤーで実装する
- **影響ファイル**: `src/ui/pixi/battle/layers/CharacterLayer.tsx`, `src/ui/pixi/battle/layers/BackgroundLayer.tsx`, `src/ui/pixi/battle/effects/（新規ディレクトリ）`, `src/ui/pixi/battle/PixiEffectBridge.ts`
- **設計書**: .claude/docs/battle_document/battle_presentation.md（新設、PIX-12） / .claude/docs/battle_document/battle_logic.md
- **リスク**: PIX-3/PIX-4/PIX-6 が前提。DOM フレーム（HP バー等）とスプライト位置の整合（キャラが動くと DOM 側ステータス表示の位置関係が崩れる）の設計判断が必要

#### PIX-6: キャラクター画像の DOM <img> → CharacterLayer スプライト移行 — `replace` / L

- **現状**: 敵/プレイヤーのビジュアルは .battle-field 内の DOM コンポーネント（BattleScreen.tsx:581-613 の EnemyFrame/PlayerFrame）が <img> で描画。CharacterLayer.tsx は「Phase 3 で本格化」の空プレースホルダ。Phase 3 プラン §5.2 に DOM/Pixi 共存→置換の段階移行案（3a/3b）が既に書かれている
- **要求**: concept-v2 主柱の間合い表現でキャラ位置を動かす前提条件。位置移動・体勢変化のアニメーションは DOM <img> では実用的でないため、キャラ絵を Pixi スプライトへ移し、ステータスバー類は DOM に残す（Phase 3 §5.2 の段階移行案は流用可）
- **影響ファイル**: `src/ui/pixi/battle/layers/CharacterLayer.tsx`, `src/ui/html/battleHtml/EnemyFrame.tsx`, `src/ui/html/battleHtml/PlayerFrame.tsx`, `src/ui/pixi/shared/textures/TextureManager.ts（新規）`
- **設計書**: .claude/docs/battle_document/battle_presentation.md（新設、PIX-12）
- **リスク**: useCardAnimation/animationEngine は DOM 要素の getBoundingClientRect 基準（useCardAnimation.tsx:236-243）で演出座標を決めており、キャラが Pixi に移ると既存ダメージ演出の座標取得が壊れる。PIX-2 の Bridge 移行と同時に進める必要がある

#### PIX-7: 体勢変化・スタミナ消費のフィードバック演出 — `new` / M

- **現状**: 存在しない。既存プランのバフ/デバフ演出（pixijs_phase2_battle_effects.md §4 のオーラ・アイコン類）は旧バフ体系（attack UP / bleed / poison 等）前提で、体勢・スタミナの概念がない
- **要求**: concept-v2 §1「間合い・体勢・スタミナを持つ駆け引き」（主柱）。体勢変化のスプライト表現（姿勢オフセット/ティント/構えアイコン）と、重い行動でのスタミナ消費を体感させる演出（消費量に応じた重み表現）を新規実装する
- **影響ファイル**: `src/ui/pixi/battle/effects/StanceEffect.tsx（新規）`, `src/ui/pixi/battle/effects/StaminaEffect.tsx（新規）`, `src/ui/pixi/battle/PixiEffectBridge.ts`, `src/ui/pixi/battle/layers/CharacterLayer.tsx`
- **設計書**: .claude/docs/battle_document/battle_presentation.md（新設、PIX-12） / .claude/docs/battle_document/battle_logic.md
- **リスク**: 体勢・スタミナのゲージ UI 本体を DOM と Pixi のどちらに置くかの設計判断が先（状態単一所有の原則上、値は新コア所有・表示層は選択制）

#### PIX-8: 観察情報の開示演出（Journal 連携リビール） — `new` / M

- **現状**: 存在しない。Pixi 側に観察・情報開示に関する実装もプラン記述もない（Phase 2-4 プランは全て攻撃演出・アセット・最適化のみ）
- **要求**: concept-v2 §1 優先3「敵の情報は観察によってのみ得られる」+ §3 A1「手記は死を越える」。観察で敵情報が解放された瞬間のリビール演出（敵スプライト上のスキャン/インク表現など）と Journal 蓄積への接続を新規実装する
- **影響ファイル**: `src/ui/pixi/battle/effects/ObservationRevealEffect.tsx（新規）`, `src/ui/pixi/battle/PixiEffectBridge.ts`, `src/ui/pixi/battle/layers/CharacterLayer.tsx`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md / .claude/docs/battle_document/battle_presentation.md（新設、PIX-12）
- **リスク**: 観察システム本体（journal/情報領域）の仕様待ち。演出単体では完結しない

#### PIX-9: Phase 2 プランの全面改訂（particle-emitter 依存除去 + 新コア向け再スコープ） — `replace` / M

- **現状**: .claude/docs/vision/plans/pixijs_phase2_battle_effects.md（2026-02-22）は @pixi/particle-emitter の EmitterConfigV3 を中核に据える（§6 Step 1、§7.1:318）が、Phase 1 監査（.claude/archive/2026-05-17-pixijs-phase1-code-level.md §0B-3）で同パッケージは v8 非対応・事実上死亡と判定済みで package.json にも未導入。さらに §2.2 の6属性パーティクル・§4.3 のフィールドエフェクトは魔術師/属性共鳴の旧戦闘コア前提
- **要求**: concept-v2 §4「まず剣士のみで新戦闘コア」+ §5「Phase 2-4 は新戦闘コアの演出として統合」。流用可能部分（ダメージ/回復/シールド演出、画面シェイク/フラッシュ、フィルタープリセット、Feature Flag 移行戦略、剣士の斬撃演出 §2.2）を抽出し、粒子技術を pixi.js v8 内蔵 ParticleContainer / Graphics ベースに差し替え、間合い/体勢/スタミナ/観察演出を主役にしたプランへ書き直す
- **影響ファイル**: `.claude/docs/vision/plans/pixijs_phase2_battle_effects.md`, `.claude/docs/vision/plans/（新プラン YYYY-MM-DD-battle-presentation-phase2.md）`
- **設計書**: .claude/docs/battle_document/battle_presentation.md（新設、PIX-12）
- **リスク**: 6属性エフェクトカタログは魔術師の後追い適合時に再利用できるため、廃棄せず凍結（バックログ）扱いにして残す

#### PIX-10: Phase 3 プランの改訂（5深度×50体 → 2ステージ制・ライト幻想トーンへ） — `replace` / M

- **現状**: .claude/docs/vision/plans/pixijs_phase3_asset_pipeline.md は Depth 1-5（森林迷宮〜深淵）×各10体=50体のバッチ生成（§3.2）、depth 別アセットバンドル battle-depth{N}（§9）、depth 別パララックス背景（§6）、「dark fantasy」固定のプロンプトテンプレート（§2.3）で構成。CharacterLayer 実装案 §5.1 は敵=上部中央/プレイヤー=下部左の固定配置で位置移動の概念がない
- **要求**: concept-v2 §3 B1「複数ステージ×テーマ別階層、まず2ステージ（例: 翠苔の森殿/霧の水郷）」+ §2「ライト幻想寄りに調整」。アセットパイプラインの機構（TexturePacker / Assets API / スプライトシート / 遅延ロード）は流用し、単位を depth→stage に、スタイルガイドをライト幻想に、キャラ配置仕様を間合いレーン対応（可動）に書き直す
- **影響ファイル**: `.claude/docs/vision/plans/pixijs_phase3_asset_pipeline.md`, `src/ui/pixi/shared/textures/AssetManifest.ts（新規時にステージ単位設計）`, `public/assets/spritesheets/（ステージ単位構成）`
- **設計書**: .claude/docs/enemy_document/（ステージ別 DB へ再編） / .claude/docs/ui_ux_design_guide.md
- **リスク**: アセット制作自体（2ステージ分の敵+背景生成）は別途 XL 級の作業。本ギャップはプラン改訂までで、制作はステージ実装側のギャップと統合管理すべき

#### PIX-11: Phase 4 プランの仕分け（最適化・レガシー削除=統合 / Spine・他画面展開=凍結） — `modify` / S

- **現状**: .claude/docs/vision/plans/pixijs_phase4_polish.md は (a) DOM アニメ削除+Feature Flag 除去（§2）、(b) バンドル分割・モバイル最適化（§3、vite.config.ts には現状 manualChunks なし）、(c) ダンジョンマップ/キャンプ/画面遷移への展開（§4）、(d) Spine Go/No-Go（§5）が混在。§6 のドキュメント更新先は存在しないパス（.claude/docs/battle_design.md）や廃止運用（TODO.md 直接編集）を参照
- **要求**: concept-v2 §5 のスコープ境界。(a)(b) は新戦闘コア演出の完成工程として統合、(c) は NodeMap がステージ再設計で変わるため凍結、(d) Spine は新コアのビジュアル方針確定まで凍結。ドキュメント参照先を現行構造（battle_document/ / task-tracker 運用）に修正する
- **影響ファイル**: `.claude/docs/vision/plans/pixijs_phase4_polish.md`, `vite.config.ts`, `src/ui/animations/animationEngine.ts`, `src/ui/html/componentsHtml/useCardAnimation.tsx`
- **リスク**: package.json:17 の @rollup/rollup-linux-arm64-gnu ハードコード（concept §5 の返済対象負債）はチャンク分割変更と同じ vite/rollup 領域なので、負債返済を先に終えてから Phase 4 のビルド最適化に着手する順序を守る

#### PIX-12: 戦闘演出設計書の新設（設計書駆動の充足） — `new` / M

- **現状**: docs/*_document/ に演出・Pixi レイヤーの設計書が存在しない（.claude/docs/battle_document/ は battle_logic.md / buff_debuff_system.md / element_system_spec.md のみ）。演出仕様は plans/pixijs_phase2-4 という「プラン文書」にしかなく、合格基準の監査対象になる設計書がない
- **要求**: concept-v2 §5 合格基準「設計書と実装の差分ゼロ（新設計書を先に更新し監査）」。間合いレーン・体勢・スタミナ・観察開示の演出仕様、レイヤー構成（Background/Character/Effect）、座標系、DOM/Pixi の役割分担を設計書として battle_document/ に新設する
- **影響ファイル**: `.claude/docs/battle_document/battle_presentation.md（新規）`, `.claude/docs/INDEX.md`
- **設計書**: .claude/docs/battle_document/battle_presentation.md（新規） / .claude/docs/battle_document/battle_logic.md
- **リスク**: 新戦闘コアの設計書（battle_logic.md 改訂）と整合を取る必要があるため、コア設計書の確定後に着手する

#### PIX-13: Pixi レイヤーのインタラクション方針決定（per-object eventMode） — `modify` / S

- **現状**: src/ui/pixi/core/usePixiApp.ts:25-41 で renderer.events.features.move を全体無効化し、キャンバスは pointer-events:none（battle-layout.css:201）。ターゲット選択は DOM 側（BattleScreen.tsx:597 onSelectTarget=setSelectedTargetIndex）。Pixi 側でクリック/ホバーを受ける仕組みは意図的にゼロ
- **要求**: concept-v2 主柱の間合い UI で、レーン上のキャラ/位置をクリック・ホバー対象にする場合の前提。usePixiApp.ts のコメント通り global feature 再有効化ではなく per-display-object の eventMode で限定的に開放するか、選択操作は DOM に残すかを設計書で確定する
- **影響ファイル**: `src/ui/pixi/core/usePixiApp.ts`, `src/ui/css/pages/battle/battle-layout.css`, `src/ui/pixi/battle/layers/CharacterLayer.tsx`
- **設計書**: .claude/docs/battle_document/battle_presentation.md（新設、PIX-12）
- **リスク**: pointer-events を部分開放すると DOM 戦闘 UI（手札・ターゲット選択）のクリックを奪う退行リスクがある。Phase 1 で踏んだ pointermove 漏れ問題（監査 §0B-5）の再発点

### 7.9 技術的負債（tech-debt / 7件）

**現状サマリ**: 技術的負債3項目はすべて現存を実コードで確認した。(1) known-issue 001: ダメージ側は play-aware 化済み（src/domain/battles/managements/useElementalChain.ts:56-61 の getDamageModifierForPlay / src/domain/characters/player/logic/elementalSystem.ts:200-206 の getDamageModifierIncludingCard）だが、共鳴デバフ側は src/domain/battles/managements/useBattleOrchestrator.ts:377-389 のクロージャが依然プレイ前の elementalAbilityState を読み、getResonanceEffectsForPlay は未実装（grep で ForPlay 系は damage 側のみ）。(2) rollup ハードコード: package.json:17 に @rollup/rollup-linux-arm64-gnu@^4.57.1 が dependencies に残存し、package-lock.json では他の rollup バイナリ群 4.53.3 と版不整合。MEMORY.md:47 に対処方針記載済み、.github/workflows は存在せず CI 依存なし。(3) テスト負債: テストは8ファイルまで前進（deck/damageCalculation/bleedDamage/buffLogic/phaseLogic/enemyAI/elementalSystem/pixiFoundation）だが renderHook 使用は0件・共通 factory なしで、.claude/docs/code-explanation/testing_analysis.md は「2ファイル66ケース」(413-422行) のまま陳腐化している。流用確定資産のうち masteryManager.ts / equipmentStats.ts / buffCalculation.ts が未テスト。加えて聖域の属性強化（enhancedElements）が戦闘経路で一度も渡されていない潜在負債を発見した（useBattleOrchestrator.ts:384 が2引数呼び出し）。

#### TD-1: known-issue 001 返済: 共鳴デバフの play-aware 化（getResonanceEffectsForPlay 追加）+ 回帰テスト — `modify` / S

- **現状**: useBattleOrchestrator.ts:377-389 の getResonanceEffects クロージャがプレイ前の elementalAbilityState（stale state）を読み、useCardExecution.ts:567 がそれを消費して burn/freeze/stun 等を付与。ダメージ側のみ play-aware 化済み（useElementalChain.ts:56-61 / elementalSystem.ts:200-206）で、「3枚目の炎でダメージは大共鳴なのにburnスタックは2枚目相当」の非対称が残存。getResonanceEffectsForPlay / getResonanceEffectsIncludingCard は未実装（grep 確認済み）
- **要求**: concept-v2 §5 スコープ境界「技術的負債は着手前に全返済（known-issue 001 を名指し）」。修正は known-issues/001 の提案通り3段: (1) elementalSystem.ts に getResonanceEffectsIncludingCard(state, card)（ElementalSystem.onCardPlay 後の仮想 state から導出する純粋関数）を追加 (2) useElementalChain.ts に getResonanceEffectsForPlay(card) を追加 (3) useBattleOrchestrator.ts:377-389 のクロージャを card 引数ありの時だけ play-aware 経路へ切替（swordsman・非カードプレイ経路は不変）。検証: elementalSystem.test.ts に「同一カードでダメージ倍率とデバフ強度の共鳴レベルが一致する」対称性テストを追加 + 手動で炎3連打時の burn スタックが大共鳴相当になることを確認。完了時 001 を Status=Fixed に更新
- **影響ファイル**: `src/domain/characters/player/logic/elementalSystem.ts`, `src/domain/battles/managements/useElementalChain.ts`, `src/domain/battles/managements/useBattleOrchestrator.ts`, `src/domain/characters/player/logic/__tests__/elementalSystem.test.ts`, `.claude/docs/known-issues/001-resonance-debuff-card-lag.md`, `.claude/docs/known-issues/INDEX.md`
- **設計書**: .claude/docs/battle_document/element_system_spec.md
- **リスク**: 【論点: 魔術師後追い方針との折り合い】「魔術師は後追い適合だから 001 も適合時まで凍結」という選択肢はあるが、今返済を推奨する。理由: (a) concept-v2 §5 が 001 を名指しで着手前返済に含めている (b) 純粋関数追加が主で副作用面積が小さく半日で閉じる (c) 001 の Lessons にある stale-state lag パターンは新戦闘コアのスタミナ・体勢 state でも同型で再発し得るため、対称化の定石をテスト付きで残すこと自体が再設計の予防線になる。先送りする場合は 001 に「魔術師適合時に再オープン」を明記し、合格基準1（差分ゼロ）からの除外をユーザーと合意する必要がある

#### TD-2: package.json の @rollup/rollup-linux-arm64-gnu ハードコード除去 + lockfile 再生成 — `remove` / S

- **現状**: package.json:17 の dependencies に "@rollup/rollup-linux-arm64-gnu": "^4.57.1" が直接記載。package-lock.json では他の rollup プラットフォームバイナリ群が 4.53.3 なのに linux-arm64-gnu のみ 4.57.1 で版不整合（package-lock.json:12, 1390-1392）。darwin では npm install（非 force）と npm audit fix が EBADPLATFORM で失敗する time bomb（MEMORY.md:47 / security-reviewer 監査 2026-05-19）。.github/workflows は存在せず CI がこのバイナリに依存している事実はない
- **要求**: concept-v2 §5「rollup ネイティブバイナリ・ハードコードを再設計着手前に解消」。手順は MEMORY.md:47 の対処方針に従う: (1) package.json:17 の該当行を削除 (2) rm -rf node_modules package-lock.json && npm install を --force なしで実行し EBADPLATFORM が出ないこと確認 (3) npm audit fix（dev/build 専用の脆弱性9件の大半が semver-major なしで解消見込み）(4) npm run build / npm run test:run / npm run lint 全通過 (5) 独立コミット（他の変更と混ぜない、MEMORY.md:47 の明示方針）
- **影響ファイル**: `package.json`, `package-lock.json`
- **リスク**: lockfile 全再生成で diff が大きくなるため独立コミット必須。この行が元々 Linux ARM64 環境（クラウド実行等）での npm optional-deps バグ回避として入った可能性があり、その場合該当環境の install が壊れる → 復活させず該当環境側で npm ci のリトライや npm rebuild で対処する方針を HISTORY に残す。vite 7 / vitest 4 が rollup 4.53.3 系で動作することは現 lockfile が証明済みなので機能リスクは低い

#### TD-3: テスト共通基盤の新設（src/test/factories/ + 重複 factory の集約） — `new` / M

- **現状**: factory 関数が各テストファイルに重複定義されている（src/domain/cards/decks/__tests__/deck.test.ts の createTestCard、src/domain/battles/calculators/__tests__/damageCalculation.test.ts の createBattleStats 等。testing_analysis.md:134-152, 191-205 が記録）。src/test/ には setup.ts のみで factories/ と fixtures/ が不在（testing_analysis.md:277-282 が欠落を指摘）
- **要求**: concept-v2 §5「フック統合テスト不足の返済」の土台 + 新戦闘コア（間合い・体勢・スタミナ）を設計書駆動で組む際のテスト受け皿。src/test/factories/ に card / enemy / battleStats の共通 factory（Partial オーバーライド型）を新設し、既存8テストファイルの重複定義を移行。追加依存なし（@testing-library/react 16.3.2 導入済み）。検証: npm run test:run 全通過 + 重複 factory 定義が0になること
- **影響ファイル**: `src/test/factories/card.ts`, `src/test/factories/enemy.ts`, `src/test/factories/battleStats.ts`, `src/domain/cards/decks/__tests__/deck.test.ts`, `src/domain/battles/calculators/__tests__/damageCalculation.test.ts`, `src/domain/battles/logic/__tests__/buffLogic.test.ts`, `src/domain/battles/logic/__tests__/bleedDamage.test.ts`, `src/domain/characters/enemy/logic/__tests__/enemyAI.test.ts`
- **リスク**: 既存テストの一斉書き換えで一時的に green が崩れる可能性 → factory 新設→1ファイルずつ移行の順で進め、各移行ごとに test:run を回す。新戦闘コアの型（スタミナ等）が入ると factory 形が変わるが、Partial オーバーライド方式なら追従コストは小さい

#### TD-4: useElementalChain の renderHook テスト新設（001 修正の対称性をフックレベルで固定） — `new` / S

- **現状**: リポジトリ全体で renderHook 使用が0件（grep 確認）。useElementalChain.ts:32-96 は ElementalSystem 純粋関数の薄いラッパで、外部 Context 依存なしのため renderHook で安価にテスト可能だがテスト不在
- **要求**: concept-v2 §5 のフックテスト負債返済のうち最小単位 + known-issues/001 の Lessons「ダメージとデバフ付与が同じ state を参照する場合は両経路を同時に play-aware 化する」をテストで恒久化。検証項目: onCardPlayed 後の状態遷移 / getDamageModifierForPlay と getResonanceEffectsForPlay（TD-1 で追加）が同一カードで同じ共鳴レベルを参照すること / resetAbility と onTurnEnd の初期化。これがプロジェクト初の renderHook 前例となり、新戦闘コアのフック（スタミナ管理等）のテスト雛形を兼ねる
- **影響ファイル**: `src/domain/battles/managements/__tests__/useElementalChain.test.ts`
- **リスク**: TD-1 完了が前提（getResonanceEffectsForPlay が存在しないとテスト対象がない）。useBattleOrchestrator 経由の結線までは検証できないが、そこは置換予定のため意図的に範囲外とする（TD-6 の線引きに記載）

#### TD-5: 流用確定資産の純粋関数テスト追加（masteryManager / equipmentStats / buffCalculation） — `new` / M

- **現状**: 3モジュールとも未テスト（src 内 __tests__ の grep で参照ゼロ）: src/domain/cards/state/masteryManager.ts、src/domain/item_equipment/logic/equipmentStats.ts、src/domain/battles/calculators/buffCalculation.ts。testing_analysis.md:232-237 で Critical priority（battle-affecting）指定のまま放置
- **要求**: concept-v2 §4「データ定義・キャンプ・熟練度システム・Journal は流用」+ §2 体験の柱3「成長の手応え＝熟練度→派生システム活用」。流用が確定している資産は再設計後も生き残るため、着手前にテストで挙動を固定する価値が最も高い: masteryManager（熟練度レベル閾値・派生解放条件）/ equipmentStats（複数スロット集計・耐久劣化）/ buffCalculation（buff スタック・DoT 計算 — burn/freeze 等は新コアでも効果として残る前提）。検証: 各モジュールに境界値含む unit test、npm run test:run 通過
- **影響ファイル**: `src/domain/cards/state/__tests__/masteryManager.test.ts`, `src/domain/item_equipment/logic/__tests__/equipmentStats.test.ts`, `src/domain/battles/calculators/__tests__/buffCalculation.test.ts`
- **リスク**: buffCalculation は新戦闘コアで数値バランスが変わる可能性があるが、「計算ロジックの構造（スタック合成・上限処理）」を固定するテストにすれば数値変更時も流用できる。speedCalculation.ts は速度ソート型ターン制が間合い・体勢制に置換される可能性が高いため、意図的に対象外とする（過剰投資回避、TD-6 の negative list に記載）

#### TD-6: テスト投資の線引き明文化 + testing_analysis.md の陳腐化解消（負債返済のスコープ確定） — `modify` / S

- **現状**: testing_analysis.md:413-422 は「Test Files 2 / Total Test Cases 66」のままだが、実態は8ファイル（bleedDamage / buffLogic / phaseLogic / enemyAI / elementalSystem / pixiFoundation が追加済み）で大幅に陳腐化。また「フック統合テスト不足」（MEMORY.md:45）の返済範囲が未定義で、置換予定の useBattleOrchestrator.ts（1006行）/ useCardExecution.ts（795行）/ useBattlePhase.ts / executeCharacterManage.ts / dungeonLogic.ts にどこまで投資するかの線引きがどこにも書かれていない
- **要求**: concept-v2 §4 方針B「戦闘コアとダンジョン構造は設計から見直す」+ §5「着手前に全返済」を両立させるための線引きを文書化: (a) negative list — useBattleOrchestrator / useCardExecution / useBattlePhase / executeCharacterManage / battleFlowManage / dungeonLogic / speedCalculation は置換予定のため新規テストを書かない（新コアのフックは実装時に TDD で書く）(b) positive list — TD-3/4/5 の範囲のみ投資 (c) testing_analysis.md の現状カウント・カバレッジ表を実態（8ファイル）へ更新。完了後 task-tracker 経由で MEMORY.md の負債項目 #2（001）/#3（テスト負債）/#5（rollup）をクローズ。これにより合格基準1「設計書と実装の差分ゼロ」の監査対象からテスト負債の曖昧さが消える
- **影響ファイル**: `.claude/docs/code-explanation/testing_analysis.md`, `.claude/MEMORY.md`, `.claude/HISTORY.md`
- **リスク**: 線引き（negative list）はユーザー承認を取ってから確定すべき。「全返済」の解釈が「既存全モジュールのテスト網羅」だと誤解されると XL 級に膨張するため、本ギャップで「返済＝流用資産の固定 + 置換対象の意図的非投資」と定義することが膨張防止の鍵

#### TD-7: 【新発見・要確認】聖域の属性強化（enhancedElements）が戦闘の共鳴効果に未接続 — `modify` / S

- **現状**: elementalSystem.ts:213-265 の getResonanceEffects は第3引数 enhancedElements（聖域の属性強化）で burn スタック増等の強化を実装済みで、sanctuaryLogic.ts:130,191 が enhancedElements を生成している。しかし戦闘側の唯一の呼び出し元 useBattleOrchestrator.ts:384 は2引数呼び出しで enhancedElements を渡しておらず、grep の全使用箇所（campTypes.ts:364 / campConstants.ts:130 / sanctuaryLogic.ts / elementalSystem.ts）に戦闘への結線が存在しない。聖域で属性強化を買っても戦闘で効果が出ない疑いがある
- **要求**: concept-v2 §5 合格基準1「設計書と実装の差分ゼロ」に抵触する候補。まず .claude/docs/battle_document/element_system_spec.md と camp_document の聖域仕様を確認し、(a) 設計上有効な機能なら TD-1 の play-aware 化と同時に結線（getResonanceEffectsIncludingCard に enhancedElements を引き回す）、(b) 魔術師後追い方針で凍結するなら known-issue として起票し差分ゼロ監査の除外リストへ明記。どちらかに倒して不可視の負債を残さない
- **影響ファイル**: `src/domain/battles/managements/useBattleOrchestrator.ts`, `src/domain/characters/player/logic/elementalSystem.ts`, `.claude/docs/known-issues/INDEX.md`
- **設計書**: .claude/docs/battle_document/element_system_spec.md
- **リスク**: 調査前のため「バグ」と断定しない（聖域効果が別経路で戦闘に渡っている可能性は grep 上は見えないが、設計書確認が先）。魔術師専用機能のため TD-1 と同じ「後追い適合との折り合い」論点を共有する — 推奨は TD-1 と同一コミットでの同時結線（同じ関数を触るため分けると二度手間）

### 7.10 設計書群（design-docs / 25件）

**現状サマリ**: 設計書は .claude/docs/ 配下の 9 ディレクトリ + 単発 2 ファイル（ap-equipment-system.md / ui_ux_design_guide.md）で計約 16,400 行。battle_document/ 3 本・card_document/ 3 本・camp_document/ 5 本・danjeon_document/ 2 本・enemy_document/ 6 本・item/journal/util 各 1 本・Overall_document/ 2 本という構成。コンセプト主柱の「間合い・体勢・スタミナ」は全設計書を grep しても記述ゼロで、battle_document/battle_logic.md (Ver4.0) はエナジー/速度差連続フェーズ/Guard→AP→HP 防御のみを定義している。全面改訂・置換が必要なのは battle_logic.md、SWORDSMAN_CARDS_40.md、danjeon_document/dungeon_exploration_ui_design_v3.0.md（5深度砂時計前提）、enemy_document/depth1〜5 データベース（腐食/狂乱/混沌/虚無/深淵テーマで計約3,800行）、Overall_document/game_design_master.md。部分改訂は buff_debuff/element/guild（噂がバフ型のみ）/journal（図鑑が発見時自動全開示）/shop+item（回復高価ポリシー欠如）/ui_ux_design_guide（ダークファンタジー前提）。維持可能なのは sanctuary/blacksmith/inventory/ap-equipment の 4 本。docs/vision/core.md は旧 Core Value（5深度×5フロア、Slay the Spire 系）のまま concept-v2.md と矛盾しており、concept-v2.md:6 自身が core.md への統合を指示している。docs/requirements/ は空。改訂順序は (1) core.md 統合 → (2) requirements Tier1-3 → (3) 新規 4 仕様（戦闘コア/ステージ定義/環境ルール/観察・手記）→ (4) 既存設計書の改訂（battle→card→enemy→dungeon→camp）→ (5) game_design_master/INDEX の総括更新、の順で「実装前に該当設計書を確定させる」設計書駆動を守るのが妥当。

#### DOC-1: vision/core.md へ concept-v2 を統合し正本を一本化 — `modify` / S

- **現状**: .claude/docs/vision/core.md:15-17 の Core Value が「5深度×5フロアの手続き生成ダンジョン」「Slay the Spire 系」のままで concept-v2 と矛盾。concept-v2.md:6 に「承認後: core.md へ統合 or 置換」と明記。入力資料 .claude/docs/vision/2026-06-11-realism-concept-kickoff.md は untracked のまま残留
- **要求**: concept-v2 §1-5（リアル性定義・コアファンタジー・世界設定 A1/A2/B1-B3・方針B・スコープ境界）を core.md の Core Value / Non-Goals / 設計原則に統合。concept-v2.md は Status 付き経緯資料として保持または archive、kickoff は archive へ移動
- **影響ファイル**: `.claude/docs/vision/core.md`, `.claude/docs/vision/concept-v2.md`, `.claude/docs/vision/2026-06-11-realism-concept-kickoff.md`
- **設計書**: .claude/docs/vision/core.md
- **リスク**: core.md を太らせすぎると CLAUDE.md との役割分担が崩れる。定義は core.md、数値は各設計書という分担を維持する

#### DOC-2: 新規: 戦闘コア仕様書（間合い・体勢・スタミナ） — `new` / L

- **現状**: battle_document/ は battle_logic.md / buff_debuff_system.md / element_system_spec.md の 3 本のみ。「スタミナ/stamina/間合い/体勢」は docs 全体を grep してもヒットゼロ（element_system_spec.md:36 の defensive stance は元素ラベルの説明にすぎない）
- **要求**: concept §1 主柱（戦闘が間合い・体勢・スタミナを持つ駆け引き）とスタミナ仕様（重い行動ほど消費・戦闘終了でリセット）を数値込みで定義する新設計書。設計書駆動の「数値の正」となる最重要文書。カード属性（射程・体勢要求・スタミナコスト）のスキーマもここで定義
- **影響ファイル**: `.claude/docs/battle_document/combat_core_spec.md`
- **設計書**: .claude/docs/battle_document/combat_core_spec.md
- **リスク**: ここの設計品質が以降の全改訂（カード・敵・UI）の前提になる。剣士のみで先に閉じる範囲を明確に区切ること

#### DOC-3: battle_logic.md 全面改訂（Ver 5.0） — `replace` / M

- **現状**: .claude/docs/battle_document/battle_logic.md:18-78 が非対称エナジー + 速度差連続フェーズシステム、:106-130 が Guard→AP→HP のハイブリッド防御を定義。新コア（間合い・体勢・スタミナ）との共存/置換関係が未定義
- **要求**: concept §4 方針B（戦闘コアは設計から見直し・資産流用）。combat_core_spec 確定後、残す要素（フェーズキュー等）と置換する要素を仕分けて Ver 5.0 へ全面改訂
- **影響ファイル**: `.claude/docs/battle_document/battle_logic.md`
- **設計書**: .claude/docs/battle_document/battle_logic.md

#### DOC-4: buff_debuff_system.md 部分改訂（体勢・スタミナ系状態の追加） — `modify` / M

- **現状**: .claude/docs/battle_document/buff_debuff_system.md:42-66 で Ver 5.0 42種（Stagger は :55 に存在）を定義済みだが、体勢有利/不利・スタミナ切れなど新コア由来の状態体系がない
- **要求**: concept §1 主柱に由来する状態（体勢崩れ・スタミナ枯渇ペナルティ等）の追加と、新コアで不要になる種別の整理。トーン調整（ライト幻想寄り）に伴う名称見直しも同時に
- **影響ファイル**: `.claude/docs/battle_document/buff_debuff_system.md`
- **設計書**: .claude/docs/battle_document/buff_debuff_system.md

#### DOC-5: element_system_spec.md 部分改訂（剣士先行・魔術師凍結注記） — `modify` / M

- **現状**: .claude/docs/battle_document/element_system_spec.md:5-17 で全カード元素タグ・剣気=slash 限定・魔術師共鳴を定義。新コアにおける剣気と間合い/体勢/スタミナの関係が未定義
- **要求**: concept §4「まず剣士のみで新戦闘コアを完成、魔術師（属性共鳴）は後追い適合」。剣気と新コアの関係を再定義し、共鳴の節（§4 以降）に凍結・後追い注記を付ける
- **影響ファイル**: `.claude/docs/battle_document/element_system_spec.md`
- **設計書**: .claude/docs/battle_document/element_system_spec.md

#### DOC-6: SWORDSMAN_CARDS_40.md 全面改訂（カード＝実際に取れる行動） — `replace` / L

- **現状**: .claude/docs/card_document/SWORDSMAN_CARDS_40.md:5-23 が剣意（Sword Intent 0-10）前提の 40 枚設計。各カードに間合い・体勢・スタミナのコスト属性が一切ない
- **要求**: concept §1「カード＝実際に取れる行動」（間合い・体勢・武器の重さ）。全 40 枚（+派生カード）に combat_core_spec のカード属性スキーマ（射程・体勢要求/変化・スタミナコスト）を付与した改訂版
- **影響ファイル**: `.claude/docs/card_document/SWORDSMAN_CARDS_40.md`
- **設計書**: .claude/docs/card_document/SWORDSMAN_CARDS_40.md
- **リスク**: 実装側 src/constants/data/cards/ との差分ゼロ監査の対象になるため、カード ID・名称の対応表を改訂時に残すこと

#### DOC-7: MAGE_CARDS_40.md は凍結注記のみ（後追い適合） — `keep` / S

- **現状**: .claude/docs/card_document/MAGE_CARDS_40.md:1-23 が共鳴システム前提の魔術師 40 枚を現役仕様として記載
- **要求**: concept §4 のクラス方針（剣士先行・魔術師後追い）。文書冒頭に「剣士コア完成後に新コアへ適合改訂、現内容は旧コア基準」の Status 注記を入れて凍結保持（廃棄しない）
- **影響ファイル**: `.claude/docs/card_document/MAGE_CARDS_40.md`
- **設計書**: .claude/docs/card_document/MAGE_CARDS_40.md

#### DOC-8: NEW_CHARACTER_SYSTEM_DESIGN.md 部分改訂（熟練度維持・Title 凍結明記） — `modify` / S

- **現状**: .claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md:11-33 の熟練度（Lv0-4・才能カード解放）は流用対象だが、:36-58 の Title System が現役仕様として残っている（concept §5 で Title は凍結）
- **要求**: concept §2 体験の柱3（成長の手応え＝既存熟練度活用）と §5 スコープ境界（Title 凍結）。熟練度の節は維持を明記、Title の節に凍結注記、新コアとの関係（熟練度ボーナスがスタミナ等に及ぶか）を追記
- **影響ファイル**: `.claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md`
- **設計書**: .claude/docs/card_document/NEW_CHARACTER_SYSTEM_DESIGN.md

#### DOC-9: 新規: ステージ定義仕様書（複数ステージ制・推奨等級） — `new` / L

- **現状**: danjeon_document/ には「ステージ」概念の設計書が存在しない。dungeon_exploration_ui_design_v3.0.md:32-35 は Depth 1-5 の単一ダンジョン前提、game_design_master.md:520-532 も Depth 1-5 難易度カーブを定義
- **要求**: concept §3 B1/B3 + ステージ構成（複数ダンジョン×テーマ別階層、どのステージからでも開始可＝難易度選択、ギルドが推奨等級を掲示）。開発用 2 ステージ（候補例: 翠苔の森殿＝初級 / 霧の水郷＝中級）の階層数・ノード構成・報酬テーブルを定義
- **影響ファイル**: `.claude/docs/danjeon_document/stage_definition_spec.md`
- **設計書**: .claude/docs/danjeon_document/stage_definition_spec.md

#### DOC-10: 新規: 環境ルール仕様書（視界・足場・広さ → 間合い・体勢への影響） — `new` / M

- **現状**: 環境が戦闘に影響する仕様はどの設計書にも存在しない（grep で視界/足場系の記述ヒットなし）。dungeon_exploration_ui_design_v3.0.md はノードタイプ確率（:83-92）のみ
- **要求**: concept §3 B1「環境（視界・足場・広さ）が間合い・体勢に影響」。環境パラメータ×戦闘パラメータの影響マトリクスと 2 ステージ分の具体値。combat_core_spec（DOC-2）とステージ定義（DOC-9）の橋渡し文書。ステージ仕様の 1 章に統合してもよいが、数値の正として独立定義を推奨
- **影響ファイル**: `.claude/docs/danjeon_document/environment_rules_spec.md`
- **設計書**: .claude/docs/danjeon_document/environment_rules_spec.md

#### DOC-11: dungeon_exploration_ui_design_v3.0.md 全面改訂（ステージ制マップへ） — `replace` / M

- **現状**: .claude/docs/danjeon_document/dungeon_exploration_ui_design_v3.0.md:23-27 が「The Descent」砂時計マップ＝5深度前提、:71-96 が全深度共通の固定 12 ノード構造（src/constants/dungeonConstants.ts を source of truth と明記）
- **要求**: concept §3 ステージ構成（5深度×5フロア置き換え）。ステージ選択 UI・ステージ内マップ構造の v4.0 へ全面改訂。Lives・帰還の緊張感（:38-65）は副柱「不可逆性」としてそのまま維持
- **影響ファイル**: `.claude/docs/danjeon_document/dungeon_exploration_ui_design_v3.0.md`
- **設計書**: .claude/docs/danjeon_document/dungeon_exploration_ui_design_v3.0.md

#### DOC-12: return_system_design.md 部分改訂（ステージ制への適合） — `modify` / S

- **現状**: .claude/docs/danjeon_document/return_system_design.md:1-40 が生存/死亡ペナルティ・テレポート石・Abyss Escape Route を 5 深度前提で定義
- **要求**: concept §1 優先2（結果の不可逆性）は仕様の核として維持しつつ、深度依存の記述（Abyss 等）をステージ制・宿場町の用語に適合。A2（回復は高価）との整合確認
- **影響ファイル**: `.claude/docs/danjeon_document/return_system_design.md`
- **設計書**: .claude/docs/danjeon_document/return_system_design.md

#### DOC-13: 敵データベースをステージ別 2 本へ再編（新コアパラメータ付き） — `replace` / XL

- **現状**: enemy_document/depth1〜depth5 の 5 ファイル計約 3,830 行が深度テーマ（depth1_enemy_database.md:1「腐食」、depth2:1「狂乱」、depth3:1「混沌」、depth4:1「虚無」、depth5:1「深淵」）で Ver4.0 パラメータ（depth1_enemy_database.md:24-50 の baseEnemyEnergy/speed/aiPatterns）を定義。間合い・体勢・スタミナの行動定義なし。トーンも硬派ダークファンタジー（depth1:15-17）
- **要求**: concept §3 B1（2 ステージ）+ §2 トーン（ライト幻想寄り）+ §1 主柱。ステージ別敵 DB 2 本を新規作成: 新コアパラメータ（射程帯・体勢挙動・スタミナ行動パターン）、観察開示用の情報段階（DOC-15 連携）、調整済みトーンの名称/フレーバー。既存 5 ファイルは「資産流用元」として凍結注記付きで保持（敵デザイン現状ベース維持は A3 不採用の決定どおり）
- **影響ファイル**: `.claude/docs/enemy_document/stage1_enemy_database.md`, `.claude/docs/enemy_document/stage2_enemy_database.md`, `.claude/docs/enemy_document/depth1_enemy_database.md`, `.claude/docs/enemy_document/depth2_enemy_database.md`, `.claude/docs/enemy_document/depth3_enemy_database.md`, `.claude/docs/enemy_document/depth4_enemy_database.md`, `.claude/docs/enemy_document/depth5_boss_database.md`
- **設計書**: .claude/docs/enemy_document/stage1_enemy_database.md / .claude/docs/enemy_document/stage2_enemy_database.md
- **リスク**: 最大ボリュームの改訂。1 ステージずつ（初級→中級）にプランを分割し、実装側 src/constants/data/characters/enemies/ との差分ゼロ監査を各ステージ完了時に行う

#### DOC-14: boss_system_redesign.md の凍結アーカイブ — `remove` / S

- **現状**: .claude/docs/enemy_document/boss_system_redesign.md:5-16 が「拠点→上層→中層→下層→深層→深淵(初回ボス)」の単一ダンジョン 5 層構造と亀裂システムを前提に設計
- **要求**: concept §3 B1 でこの前提（単一ダンジョン・深淵終点）が消える。文書を凍結注記付きでアーカイブし、ステージボスの仕様は DOC-9（ステージ定義）と DOC-13（ステージ敵 DB）側で定義
- **影響ファイル**: `.claude/docs/enemy_document/boss_system_redesign.md`

#### DOC-15: 新規: 観察・手記仕様書（敵情報の段階開示と死越え永続） — `new` / M

- **現状**: 観察による敵情報の段階開示仕様はどの設計書にもない。journal_system_implementation_plan.md:100-108 は「発見時に自動更新・FULL ACCESS」で、遭遇＝全情報開示の前提
- **要求**: concept §1 優先3（敵ステータスは初見非開示、観察・経験で判明）+ §3 A1（手記は死を越える: 死んでも知識は引き継がれる）。開示段階（未知→名前→行動傾向→ステータス等）、観察トリガー（遭遇回数・被弾・撃破等）、ゲームオーバー時の永続範囲、噂（B2）による初期ヒント注入の接続仕様を定義
- **影響ファイル**: `.claude/docs/journal_document/observation_journal_spec.md`
- **設計書**: .claude/docs/journal_document/observation_journal_spec.md

#### DOC-16: journal_system_implementation_plan.md 部分改訂（観察ゲーティング対応） — `modify` / M

- **現状**: .claude/docs/journal_document/journal_system_implementation_plan.md:88-108 で Encyclopedia の Monsters カテゴリが「stats, patterns, weaknesses」を発見時に開示、:202-211 の EncyclopediaData が discoveredMonsters: Set<string> の二値（発見/未発見）モデル。死を越える永続化の明示仕様なし
- **要求**: concept §3 A1。Monsters カテゴリを DOC-15 の段階開示モデルに改め、「ゲームオーバー（Life=0 フルリセット）でも手記＝知識は残る」例外を明記。データモデルを開示レベル付きに更新
- **影響ファイル**: `.claude/docs/journal_document/journal_system_implementation_plan.md`
- **設計書**: .claude/docs/journal_document/journal_system_implementation_plan.md
- **リスク**: game_design_master.md の「Life=0 で完全リセット（実績のみ永続）」と矛盾するため、永続化の例外リストを master 側（DOC-21）と同時に揃える必要あり

#### DOC-17: guild_design.md 部分改訂（噂＝敵情報ヒント・推奨等級掲示） — `modify` / M

- **現状**: .claude/docs/camp_document/guild_design.md:138-145 の RumorEffect は elite_rate / shop_discount / treasure_rate / start_bonus のラン強化バフ型のみ。ステージ推奨等級の掲示機能は存在しない
- **要求**: concept §3 B2（ギルドの噂が敵情報の初期ヒント）+ B3（ギルドが各ステージの推奨等級を掲示）。RumorEffect に enemy_info 型（DOC-15 の開示段階を進める）を追加し、ステージ掲示板の節を新設
- **影響ファイル**: `.claude/docs/camp_document/guild_design.md`
- **設計書**: .claude/docs/camp_document/guild_design.md

#### DOC-18: camp_facilities_design.md 部分改訂（キャンプ＝宿場町の再解釈） — `modify` / S

- **現状**: .claude/docs/camp_document/camp_facilities_design.md:15-20 は BaseCamp を「探索の合間の休息と準備の場」とのみ定義。単一ダンジョン前提で「ダンジョン群の中継地」という位置づけがない
- **要求**: concept §3 B2（既存 5 施設をダンジョン群の中継地＝宿場町に再解釈）。世界観の節を改訂。施設の機能仕様自体は流用（方針B）なので変更最小
- **影響ファイル**: `.claude/docs/camp_document/camp_facilities_design.md`
- **設計書**: .claude/docs/camp_document/camp_facilities_design.md

#### DOC-19: 回復高価ポリシーの反映（shop_design + EQUIPMENT_AND_ITEMS_DESIGN） — `modify` / M

- **現状**: .claude/docs/camp_document/shop_design.md（V3、1505 行）の消耗品・回復アイテム価格と .claude/docs/item_document/EQUIPMENT_AND_ITEMS_DESIGN.md の Consumable 20 種は現行経済バランス前提。「回復は高価」という価格政策の記述はどちらにもない
- **要求**: concept §3 A2（魔法は希少・回復は高価 — 治癒は貴重で回復が安くない世界律）。回復アイテムの希少度・価格・入手経路を A2 基準で再設計し、両設計書の数値を更新（数値の正は設計書）
- **影響ファイル**: `.claude/docs/camp_document/shop_design.md`, `.claude/docs/item_document/EQUIPMENT_AND_ITEMS_DESIGN.md`
- **設計書**: .claude/docs/camp_document/shop_design.md / .claude/docs/item_document/EQUIPMENT_AND_ITEMS_DESIGN.md
- **リスク**: 回復を高価にすると現行の敵火力バランス（depth1 設計コンセプト「7〜10 回のバトルで装備耐久度が危険域」）と連動して難易度が跳ねる。新敵 DB（DOC-13）と同時に調整

#### DOC-20: 維持グループの確認（sanctuary / blacksmith / inventory / ap-equipment） — `keep` / S

- **現状**: .claude/docs/camp_document/sanctuary_design.md（V3.0 ソウル/スキルツリー）、blacksmith_design.md（V1.1 品質+強化）、util_doument/inventory_design.md（V3.0）、ap-equipment-system.md（AP=装備耐久由来）はいずれも新戦闘コアと直交する領域
- **要求**: concept §4 方針B（データ定義・UI・キャンプ実装・熟練度・Journal は流用）。4 本とも維持。新コア確定後に整合確認（例: スタミナとサンクチュアリスキルの関係、AP 防御と体勢の関係）を 1 パスだけ行い、必要なら軽微追記
- **影響ファイル**: `.claude/docs/camp_document/sanctuary_design.md`, `.claude/docs/camp_document/blacksmith_design.md`, `.claude/docs/util_doument/inventory_design.md`, `.claude/docs/ap-equipment-system.md`

#### DOC-21: game_design_master.md 全面改訂（V4.0: コンセプト v2 反映） — `replace` / M

- **現状**: .claude/docs/Overall_document/game_design_master.md:13-24 がジャンルを「Extraction Dungeon RPG × Card Battle」（参照: 風来のシレン/StS/ダークソウル）と定義、:520-532 が Depth1-5 難易度カーブ。コアファンタジー（生身の探索者）・体験の柱・ステージ制・新戦闘コアの記載なし
- **要求**: concept §1-3 全体（リアル性定義・コアファンタジー・体験の柱 3 本・トーン調整・世界設定 A1/A2/B1-B3）を反映した V4.0 全面改訂。コアループ（:52-89）はステージ選択込みに更新、Lives/不可逆性の章は維持。手記の死越え例外（DOC-16）もここで整合
- **影響ファイル**: `.claude/docs/Overall_document/game_design_master.md`
- **設計書**: .claude/docs/Overall_document/game_design_master.md

#### DOC-22: DESIGN_CHANGE_PLAN_lives_system.md を archive へ移動 — `remove` / S

- **現状**: .claude/docs/Overall_document/DESIGN_CHANGE_PLAN_lives_system.md:11-30 は Phase 1-3 実装完了済みのプラン文書だが、設計書ディレクトリに残留している
- **要求**: 運用原則（完了プランは .claude/archive/ へ）+ concept §5 合格基準「設計書と実装の差分ゼロ」の監査母集合を設計書のみに揃えるため、archive へ移動し INDEX から除外
- **影響ファイル**: `.claude/docs/Overall_document/DESIGN_CHANGE_PLAN_lives_system.md`

#### DOC-23: ui_ux_design_guide.md 部分改訂（トーン調整 + 新戦闘 UI 3 要素） — `modify` / M

- **現状**: .claude/docs/ui_ux_design_guide.md:24-29 が「Dark Fantasy Aesthetic（低彩度・高コントラスト）」を規定し、:19 で Psychological Pressure を原則化。スタミナ/間合い/体勢の表示原則はゼロ。vision/mockups/2026-06-11-battle-ui-mockup.html が先行して存在
- **要求**: concept §2 トーン（ダークファンタジーをライト幻想寄りに調整）に合わせたカラートーン節の改訂と、新戦闘コア 3 要素（スタミナ・間合い・体勢）+ 観察段階表示の UI 原則を追加。mockup との整合を取り、PixiJS Phase 2-4 を「新戦闘コアの演出」として位置づける注記（§5 スコープ）
- **影響ファイル**: `.claude/docs/ui_ux_design_guide.md`, `.claude/docs/vision/mockups/2026-06-11-battle-ui-mockup.html`
- **設計書**: .claude/docs/ui_ux_design_guide.md

#### DOC-24: INDEX.md 更新とディレクトリ構成整理 — `modify` / S

- **現状**: .claude/docs/INDEX.md:21-33 の索引は現行 9 ディレクトリ構成のみで、新規仕様書（戦闘コア/ステージ/環境/観察）の置き場が未定義。danjeon_document / util_doument にスペルミスあり。INDEX.md:17 で requirements/ が「現状空」と明記
- **要求**: concept §7（設計書更新フェーズ）の受け皿として、新規 4 仕様書の配置・凍結文書の Status 凡例・改訂順序を INDEX に反映。ディレクトリ名タイポ修正は任意（CLAUDE.md・スキル群からの参照リンク更新を伴うため、やるなら単独コミットで）
- **影響ファイル**: `.claude/docs/INDEX.md`, `.claude/docs/danjeon_document`, `.claude/docs/util_doument`
- **リスク**: ディレクトリ改名は design-research スキルや CLAUDE.md References のパス参照を壊す可能性。改名する場合は grep で全参照を洗うこと

#### DOC-25: requirements/ に Tier 1-3 要件定義を新規作成 — `new` / M

- **現状**: .claude/docs/requirements/ は空ディレクトリ（ls で確認、ファイルゼロ）。INDEX.md:17 も「現状空、新機能追加時に使用」と記載
- **要求**: concept §7 step 3（要件定義: Tier 1 コア / Tier 2 補助 / Tier 3 実験）。ギャップ分析の結果を Tier 分けし、各設計書改訂（DOC-2 以降）の前提となる要件文書を作成。見送り軸（身体フル実装・世界生活・確率透明性等）は Tier 2/3 再検討としてここに記録
- **影響ファイル**: `.claude/docs/requirements/tier1-core.md`, `.claude/docs/requirements/tier2-support.md`, `.claude/docs/requirements/tier3-experimental.md`
