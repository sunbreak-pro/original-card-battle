# Plan: 戦闘プロトタイプ — 間合い × スタミナ 最小検証台

> **Status**: IN_PROGRESS
> **Created**: 2026-06-27
> **Task**: MEMORY.md 進行中「リアル性コンセプト v2 — 戦闘システム上流確定 + プロトタイプ計画」
> **Project**: `/Users/newlife/dev/apps/original-card-battle`
> **Branch**: `feat/battle-prototype-range-stamina`
> **入力仕様**: `docs/vision/concept-v2.md` §1（スタミナ: 回復 近+1/中+2/遠+3・MAX20・疲労で威力/防御/体勢を確定減衰）/ `docs/requirements/tier1-core.md` R1-5（スタミナ）・R1-6（間合い）
> **再構成メモ**: 当初参照された計画書が実在しなかったため、上記入力仕様＋タスク本文から再構成（2026-06-27）。本書が実装契約。

## Context

「リアル性コンセプト v2」の戦闘主柱（間合いの読み合い × スタミナの消耗）が**遊びとして面白いか**を、本実装に入る前に最小コストで体感検証する。隔離した throwaway プロトタイプ。本番コードへは流用しない。

**検証したい問い**:

1. 敵の有効間合いを「外して詰める」と有利になる相性（固定強弱なし）は体感できるか
2. 「攻めると枯渇→精度低下／退くと回復」のジレンマ（押し引き）は成立するか
3. 回復 近+1/中+2/遠+3・MAX20・カードコストのテンポは妥当か

### Non-goals（含めない）

剣気・体勢フル・観察・ステージ・不可逆性・セーブ・PixiJS 演出・魔術師。網羅テスト。本番 BattleScreen/useBattleOrchestrator への統合。

### 制約（厳守）

- **隔離**: 実装は `src/ui/prototype/` 配下の新規ファイルのみ。既存 `useBattleOrchestrator` / `BattleScreen` は流用も改変もしない
- **不可侵コード**（`src/domain/cards/decks/deck.ts` / `deckReducer.ts`）変更禁止。プロトタイプは自前の最小デッキ操作を持つ（import もしない）
- `src/domain/` / `src/contexts/` の既存資産を変更しない
- 既存ファイルを一切編集しない（`prototype.html` をルートに**新規追加**して起動口とする。`index.html`・`vite.config.ts`・`App.tsx` は触らない）
- $0 厳守 / React 19 + TS strict / explicit return types（public）/ named exports / `import type` / `as const`（enum 禁止）
- CSS は `.prototype-battle` スコープ。サイズは vh/vw（border のみ px）。UI テキストは日本語、コード/コメントは英語
- **精度は確率ミスではなく確定の効果量低下**で表現（運に左右させない）

## 設計（実装の正・数値はすべてここを正とする）

### 間合い（距離）

- 段階: `close`(近)=0 / `mid`(中)=1 / `far`(遠)=2。プレイヤー⇔敵の単一距離 state（index 0–2、clamp）
- 初期距離 = `mid`（中＝敵のキルゾーン。開幕から判断を迫る）
- **固定強弱なし（相性ベース）**: どの間合いも一長一短
  - close: プレイヤーの高威力攻撃が通る／だが回復は +1 のみ・敵に押し戻される
  - mid: 敵のキルゾーン（敵の薙ぎ払いが最大）
  - far: 回復 +3 で立て直せる／だが届く攻撃が牽制（弱）だけ・敵の穂先の突きが届く

### スタミナ

- `MAX_STAMINA = 20`、開始 20、戦闘内のみ（終了概念なし＝検証台は1戦）
- **ターン開始時、現在の間合いで回復**: `close +1 / mid +2 / far +3`（`STAMINA_RECOVERY`）
- カードコストで消費。スタミナ不足のカードはプレイ不可（理由表示 + グレーアウト）
- **疲労＝確定の威力減衰**（確率ミスではない）:
  - `FATIGUE_THRESHOLD = 8`、`FATIGUE_FLOOR_MULT = 0.4`
  - `staminaDamageMultiplier(s) = s >= 8 ? 1.0 : max(0.4, s / 8)`（例: 8→1.0 / 4→0.5 / 1→0.4）

### 相性（間合い補正）

- `rangeMultiplier(distance, effRange)`: 距離差 `diff = |distance - effRange|`
  - diff 0 → ×1.0（最適）/ diff 1 → ×0.5（不適）/ diff 2 → ×0.15（ほぼ空振り）
- 攻撃カードは**プレイ可能だがダメージが相性で変動**する方式（不適でも撃てるが激減）。これにより「外す/詰める」の相性勾配を**カード上の予測ダメージ表示**で体感させる（tier1 の「間合い外は不可」より、検証台では勾配の可視化を優先。要件本実装との差異として記録）

### ダメージ計算（純関数）

```
computeAttackDamage(card, attackerStamina, distance):
  raw = card.basePower
        * rangeMultiplier(distance, card.effectiveRange)
        * staminaDamageMultiplier(attackerStamina)
  return max(0, round(raw))   // ガードは適用時に減算
```

### カード（剣士・初期デッキ 12 枚 = 6 種 × 2）

| id       | 名前         | type   | cost | effRange | power | shift(距離変化) | guard | 備考                         |
| -------- | ------------ | ------ | ---- | -------- | ----- | --------------- | ----- | ---------------------------- |
| thrust   | 突き         | attack | 4    | close    | 9     | 0               | —     | 近接最大火力                 |
| lunge    | 踏み込み斬り | attack | 5    | close    | 7     | -1（詰める）    | —     | 攻撃内蔵の前進               |
| feint    | 牽制         | attack | 3    | mid      | 4     | +1（退く）      | —     | 削りつつ後退                 |
| step_in  | 足捌き・前   | move   | 2    | —        | 0     | -1              | —     | 専用・詰め                   |
| step_out | 足捌き・後   | move   | 1    | —        | 0     | +1              | —     | 専用・退き（安い）           |
| brace    | 呼吸を整える | guard  | 2    | —        | 0     | 0               | 6     | 受けを固める（次の被弾軽減） |

- `shift`: 距離 index への加算（- = 詰める / + = 離す）。攻撃適用後に距離変更、clamp[0,2]
- **「移動カードが来ないターンでも詰まない」保証**: 攻撃カードの lunge/feint が footwork(shift) を内蔵。デッキ 12 枚中 shift≠0 は 8 枚。さらにデッキ再循環で必ず移動手段に到達するため恒久的な詰みは構造的に発生しない（検証 #4）
- デッキ操作はプロトタイプ自前（draw/discard/再シャッフル）。`Math.random` 使用、テストは `vi.spyOn(Math,'random')` で決定化

### 敵（1体・長柄リーチ型）

- 名前「長柄の歪み兵」、`maxHp = 38`、stamina MAX 20・開始 20・回復は同じ間合い表
- 行動（自前定義）:
  - `sweep` 薙ぎ払い: effRange mid, power 8, cost 5（キルゾーン主力）
  - `reach_thrust` 穂先の突き: effRange far, power 3, cost 4（遠から届く軽い牽制）
  - `shove` 石突きの押し込み: effRange close, power 2, cost 3, shift +1（**矯正技**: 懐に入った敵＝プレイヤーを mid へ押し戻す）
  - `reposition` 間合い取り直し: cost 2, shift（mid 方向へ ±1）, power 0（不利位置/低スタミナ時）
- **AI（決定的・1ターン1行動）** `chooseEnemyAction(distance, enemyStamina)`:
  - distance close: shove が撃てれば shove（押し戻し＋微ダメ）。撃てなければ reposition/休む
  - distance mid: sweep（不可なら reach_thrust、なお不可なら休む）
  - distance far: reach_thrust（不可なら mid 方向へ reposition）
  - 最安攻撃も撃てない低スタミナ: reposition で立て直し
- これで「close はプレイヤー有利（敵は弱い shove のみ）→ 敵が mid へ押し戻す → mid で sweep」の押し引きが生まれる（検証 #1/#2）

### チューニング所見（ヘッドレス sim 由来・2026-06-27）

実装後にヘッドレスで4戦略（ゴリ押し近接 / 遠カイト / メリハリ / 近ロック）を流して数値検証した結果、初期値（敵HP42・reach_thrust power6）は**勝ち筋がほぼ無い**＝「退いて回復」する逃げ場が無く、遠も穂先6で詰められ詰みだった。設計意図（敵＝中がキルゾーンのリーチ型）を保ったまま最小調整:

- `reach_thrust` power **6→3**（遠を回復の逃げ場にし「退いて回復」を成立させる＝最重要）
- `ENEMY_MAX_HP` **42→38**（疲労→回復サイクルを1回挟まないと勝てない＝スタミナが勝敗に効く帯。かつ熟練で勝てる）

調整後も「ゴリ押し・カイトは負け、近でバースト→遠で回復→再突入＋中(キルゾーン)回避の賢い立ち回りで勝てる」を維持（押し引きのジレンマ保存）。**最終バランスはユーザーの実機プレイで確定・微調整する前提**。数値は `constants.ts` / `enemy.ts` で容易に変更可。

### プレイヤー / 勝敗

- `PLAYER_MAX_HP = 30`、`ENEMY_MAX_HP = 38`、`HAND_SIZE = 3`
- ガード: 敵攻撃時に `dmg -= guard`（min0）、`guard -= dmg`（min0）。残ガードはプレイヤー次ターン開始でクリア
- 敵 HP 0 → 勝利 / プレイヤー HP 0 → 敗北。結果オーバーレイ + リスタート

### ターン進行（ターン制・ドロー制）

1. **プレイヤー turn 開始**: 現在間合いでスタミナ回復 → 手札を `HAND_SIZE` までドロー（山切れは捨札を再シャッフル）→ 残ガードクリア
2. **プレイヤー行動**: スタミナの続く限りカードを任意枚プレイ（攻撃=敵にダメージ／移動=距離変更／brace=ガード付与）。攻撃カードには現在間合いでの**予測ダメージ**を表示
3. **ターン終了**: 手札を捨て、敵フェーズへ
4. **敵フェーズ**: 敵スタミナ回復 → `chooseEnemyAction` で1行動 → 適用（攻撃は敵の相性×疲労で計算）→ 勝敗判定 → 次のプレイヤー turn
5. ログに各イベント（回復・カード・疲労減衰・敵行動・勝敗）を日本語で記録

## Steps

- [ ] 1. `src/ui/prototype/engine/types.ts` — 型定義（RangeBand/CardType/PrototypeCard/EnemyAction/EnemyDef/BattleState/BattleAction）。`as const` オブジェクト、enum 禁止、explicit types
- [ ] 2. `src/ui/prototype/engine/constants.ts` — チューニング定数（MAX_STAMINA, STAMINA_RECOVERY, FATIGUE_THRESHOLD/FLOOR, RANGE_MULT, HP, HAND_SIZE, 初期距離）
- [ ] 3. `src/ui/prototype/engine/combat.ts` — 純関数（staminaRecovery, staminaDamageMultiplier, rangeMultiplier, computeAttackDamage, clampDistance, shiftDistance）
- [ ] 4. `src/ui/prototype/engine/cards.ts` — 6 種カード定義 + 初期デッキ12枚 + 自前 shuffle/draw/discard（`Math.random`、deck.ts 非依存）
- [ ] 5. `src/ui/prototype/engine/enemy.ts` — 敵定義 + `chooseEnemyAction`（決定的）+ 敵行動適用ヘルパ
- [ ] 6. `src/ui/prototype/engine/battleReducer.ts` — 純 reducer（PLAY_CARD / END_TURN〔敵フェーズ内包〕/ RESTART）+ 初期 state 生成
- [ ] 7. `src/ui/prototype/components/*` + `src/ui/prototype/PrototypeBattle.tsx` — useReducer 接続。DistanceTrack（近/中/遠の視覚）/ Combatant パネル（HP・スタミナ・疲労帯・敵の有効間合いヒント）/ HandView（cost・有効間合い・予測ダメージ・footwork・不可理由）/ BattleLog / Controls / ResultOverlay
- [ ] 8. `src/ui/prototype/prototype-battle.css`（`.prototype-battle` スコープ・vh/vw）+ `src/ui/prototype/main.tsx`（StrictMode mount）+ ルート `prototype.html`（新規）
- [ ] 9. `src/ui/prototype/engine/__tests__/combat.test.ts` + `battleReducer.test.ts` — 純関数の最小テスト（境界値・相性・疲労・距離clamp・スタミナ消費上限・敵フェーズ・勝敗）。`vi.spyOn(Math,'random')` で決定化

## Files

| File                                                      | Operation | Notes                                                                              |
| --------------------------------------------------------- | --------- | ---------------------------------------------------------------------------------- |
| `prototype.html`                                          | Create    | ルート起動口。`/src/ui/prototype/main.tsx` を読む。dev で `/prototype.html` を開く |
| `src/ui/prototype/main.tsx`                               | Create    | StrictMode で `<PrototypeBattle/>` を #root に mount                               |
| `src/ui/prototype/PrototypeBattle.tsx`                    | Create    | useReducer コンテナ。敵フェーズは reducer 内で同期解決（StrictMode 安全）          |
| `src/ui/prototype/engine/types.ts`                        | Create    | 型のみ                                                                             |
| `src/ui/prototype/engine/constants.ts`                    | Create    | 数値の正                                                                           |
| `src/ui/prototype/engine/combat.ts`                       | Create    | 純関数                                                                             |
| `src/ui/prototype/engine/cards.ts`                        | Create    | カード + 自前デッキ操作                                                            |
| `src/ui/prototype/engine/enemy.ts`                        | Create    | 敵 + AI                                                                            |
| `src/ui/prototype/engine/battleReducer.ts`                | Create    | 純 reducer + initState                                                             |
| `src/ui/prototype/components/DistanceTrack.tsx`           | Create    | 間合い視覚化                                                                       |
| `src/ui/prototype/components/CombatantPanel.tsx`          | Create    | HP/スタミナ/疲労帯/ガード                                                          |
| `src/ui/prototype/components/HandView.tsx`                | Create    | 手札・予測ダメージ・不可理由                                                       |
| `src/ui/prototype/components/BattleLog.tsx`               | Create    | ログ                                                                               |
| `src/ui/prototype/prototype-battle.css`                   | Create    | `.prototype-battle` スコープ                                                       |
| `src/ui/prototype/engine/__tests__/combat.test.ts`        | Create    | 純関数テスト                                                                       |
| `src/ui/prototype/engine/__tests__/battleReducer.test.ts` | Create    | reducer テスト                                                                     |

> 既存ファイルの編集は**ゼロ**。すべて新規。

## Verification

- [ ] `npm run build`（`tsc -b && vite build`）通過（型ゲート＝prototype も型チェックされる）
- [ ] `npm run test:run` 通過（プロトタイプ純関数テスト green、既存テストも green）
- [ ] `npm run lint` 通過（`.prototype-battle` スコープ・未使用なし・import type 準拠）
- [ ] `npm run dev` → `http://localhost:5173/prototype.html` で起動し1戦プレイ可能
- [ ] **検証#1**: 敵の有効間合い（mid/far）を外して close に詰めると、敵が弱い shove しか撃てず有利になる相性が体感できる
- [ ] **検証#2**: close で攻め続けると回復+1で枯渇→威力が確定減衰、far へ退くと+3で回復、のジレンマが成立する
- [ ] **検証#3**: 回復 1/2/3・MAX20・カードコストのテンポが「攻めと消耗の押し引き」として妥当（破綻＝即死/不動の膠着がない）
- [ ] **検証#4**: 専用移動カードが手札に来ないターンでも、lunge/feint の内蔵移動＋デッキ再循環で間合いを変えられ、詰まない
