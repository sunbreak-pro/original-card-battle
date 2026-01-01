# バトルシステム Ver 4.0 実装計画書

## 概要

本ドキュメントは、バトルシステム Ver 4.0（非対称エナジーシステム、速度システム、敵複数行動システム）の実装計画を示す。

**実装優先度**: 高
**推定工数**: 中規模（2-3週間）
**依存関係**: 既存のバトルロジック、敵AI、UIコンポーネント

---

## 実装フェーズ

### Phase 1: 型定義とデータ構造の更新 ✅

**Status**: 完了

**実施内容**:
- `EnemyAction` インターフェースに以下を追加:
  - `displayIcon?: string`
  - `priority?: number`
  - `energyCost?: number`
- `Enemy` インターフェースに以下を追加:
  - `baseEnemyEnergy: number`
  - `speed: number`
- 全ての既存敵データ（Depth1）を新仕様に更新

**完了ファイル**:
- `/src/Character/data/EnemyData.ts` ✅
- `/blueprint/battle_document/battle_logic.md` (Ver 4.0) ✅

---

### Phase 2: 速度システムの実装 ✅

**Status**: 完了

#### 2.1 速度計算ロジック ✅

**完了ファイル**: `/src/battles/logic/speedCalculation.ts`

```typescript
/**
 * 速度計算とターン順序決定
 */

import type { BuffDebuffMap } from "@/cards/type/baffType";
import type { Enemy } from "@/Character/data/EnemyData";

export interface SpeedBonus {
  name: "先制" | "電光石火";
  attackBonus: number;
  criticalBonus: number;
}

/**
 * プレイヤーの速度計算
 */
export function calculatePlayerSpeed(buffs: BuffDebuffMap): number {
  let speed = 50; // 基本速度

  // 速度上昇バフ
  if (buffs.has("speedUp")) {
    const speedBuff = buffs.get("speedUp")!;
    speed += speedBuff.value * speedBuff.stacks;
  }

  // スロウデバフ
  if (buffs.has("slow")) {
    const slowDebuff = buffs.get("slow")!;
    speed -= slowDebuff.value * 10;
  }

  // 速度低下デバフ
  if (buffs.has("speedDown")) {
    const speedDown = buffs.get("speedDown")!;
    speed -= speedDown.value;
  }

  // 加速バフ
  if (buffs.has("haste")) {
    speed += 30;
  }

  return Math.max(0, speed);
}

/**
 * 敵の速度計算
 */
export function calculateEnemySpeed(
  enemy: Enemy,
  buffs: BuffDebuffMap
): number {
  let speed = enemy.speed; // 敵固有の速度

  // バフ/デバフ適用（プレイヤーと同じロジック）
  if (buffs.has("speedUp")) {
    const speedBuff = buffs.get("speedUp")!;
    speed += speedBuff.value * speedBuff.stacks;
  }

  if (buffs.has("slow")) {
    const slowDebuff = buffs.get("slow")!;
    speed -= slowDebuff.value * 10;
  }

  if (buffs.has("speedDown")) {
    const speedDown = buffs.get("speedDown")!;
    speed -= speedDown.value;
  }

  if (buffs.has("haste")) {
    speed += 30;
  }

  return Math.max(0, speed);
}

/**
 * ターン順序を決定
 */
export function determineTurnOrder(
  playerSpeed: number,
  enemySpeed: number
): "player" | "enemy" {
  return playerSpeed >= enemySpeed ? "player" : "enemy";
}

/**
 * 速度差ボーナスの計算
 */
export function calculateSpeedBonus(
  actorSpeed: number,
  targetSpeed: number
): SpeedBonus | null {
  const speedDiff = actorSpeed - targetSpeed;

  if (speedDiff >= 50) {
    return {
      name: "電光石火",
      attackBonus: 0.15,
      criticalBonus: 0.2,
    };
  } else if (speedDiff >= 30) {
    return {
      name: "先制",
      attackBonus: 0.15,
      criticalBonus: 0,
    };
  }

  return null;
}
```

**テスト項目**:
- [x] プレイヤー速度計算（バフ/デバフ適用）
- [x] 敵速度計算（バフ/デバフ適用）
- [x] ターン順序決定（同速の場合はプレイヤー優先）
- [x] 速度差ボーナス計算（30差、50差）

---

#### 2.2 バフ/デバフ型定義の更新 ✅

**完了ファイル**: `/src/cards/type/baffType.ts`

**追加する型**:
```typescript
export type BuffDebuffType =
  // 既存の型...
  | "speedUp"
  | "speedDown"
  | "haste"
  // ...その他
```

**削除する型**:
- `burn`
- `freeze`
- `paralyze`
- `defDown`
- `defUp`
- `physicalUp`
- `magicUp`

**注意**: 削除する型を使用している箇所を全て修正すること

---

### Phase 3: 敵の複数行動システム ✅

**Status**: 完了

#### 3.1 敵エナジー管理 ✅

**更新ファイル**: `/src/battles/logic/useBattleLogic.ts` (Phase 4で実装予定)

**追加state**:
```typescript
const [enemyEnergy, setEnemyEnergy] = useState(0);
```

**エナジー計算関数**:
```typescript
function calculateEnemyEnergy(enemy: Enemy): number {
  return enemy.baseEnemyEnergy;
}

function applyEnemyEnergyModifiers(
  baseEnergy: number,
  buffs: BuffDebuffMap
): number {
  let energy = baseEnergy;

  // slow デバフはエナジーに影響しない（速度のみ）
  // 将来的に energyRegen バフなどで変動可能

  return Math.max(1, energy); // 最低1エナジー保証
}
```

---

#### 3.2 敵行動実行ロジック ✅

**完了ファイル**: `/src/battles/logic/enemyActionExecution.ts`

```typescript
import type { Enemy, EnemyAction } from "@/Character/data/EnemyData";
import { determineEnemyAction } from "./enemyAI";

/**
 * 敵のエナジー分の行動を実行
 */
export async function executeEnemyActions(
  enemy: Enemy,
  enemyHp: number,
  enemyMaxHp: number,
  turn: number,
  enemyEnergy: number,
  onExecuteAction: (action: EnemyAction) => Promise<void>,
  checkBattleEnd: () => boolean
): Promise<void> {
  let remainingEnergy = enemyEnergy;
  const actionsToExecute: EnemyAction[] = [];

  // エナジーが尽きるまで行動を選択
  while (remainingEnergy > 0) {
    const action = determineEnemyAction(
      enemy,
      enemyHp,
      enemyMaxHp,
      turn,
      remainingEnergy
    );

    const actionCost = action.energyCost ?? 1;

    if (actionCost > remainingEnergy) {
      // エナジー不足なら低コスト行動を選択
      const fallbackAction = getFallbackAction(remainingEnergy);
      actionsToExecute.push(fallbackAction);
      break;
    }

    actionsToExecute.push(action);
    remainingEnergy -= actionCost;
  }

  // 行動を順次実行
  for (let i = 0; i < actionsToExecute.length; i++) {
    await onExecuteAction(actionsToExecute[i]);

    // 行動間のディレイ
    await new Promise((resolve) => setTimeout(resolve, 800));

    // 戦闘終了チェック
    if (checkBattleEnd()) {
      break;
    }
  }
}

/**
 * エナジー不足時のフォールバック行動
 */
function getFallbackAction(remainingEnergy: number): EnemyAction {
  if (remainingEnergy >= 1) {
    return {
      name: "基本攻撃",
      type: "attack",
      baseDamage: 5,
      displayIcon: "⚔️",
      priority: 0,
      energyCost: 1,
    };
  }

  return {
    name: "待機",
    type: "special",
    baseDamage: 0,
    displayIcon: "💤",
    priority: 0,
    energyCost: 0,
  };
}
```

**テスト項目**:
- [x] 1エナジー敵の行動（1回のみ）
- [x] 2エナジー敵の行動（2回実行）
- [x] エナジー不足時のフォールバック
- [x] 戦闘終了時の中断処理

---

### Phase 4: ターンフロー統合

**Status**: 進行中

#### 4.1 useBattleLogicの更新 ✅

**完了ファイル**: `/src/battles/logic/useBattleLogic.ts`

**主な変更点**:

1. **速度計算とターン順序決定**:
```typescript
import {
  calculatePlayerSpeed,
  calculateEnemySpeed,
  determineTurnOrder,
  calculateSpeedBonus,
} from "./speedCalculation";

// ターン開始時
const playerSpeed = calculatePlayerSpeed(playerBuffs);
const enemySpeed = calculateEnemySpeed(currentEnemy, enemyBuffs);
const firstActor = determineTurnOrder(playerSpeed, enemySpeed);
```

2. **プレイヤーフェーズと敵フェーズの分離**:
```typescript
async function executePlayerPhase() {
  onPlayerTurnStart();

  const speedBonus = calculateSpeedBonus(playerSpeed, enemySpeed);
  // 速度ボーナスを一時バフとして適用

  // プレイヤーの行動待機（既存のhandleEndTurn）

  onPlayerTurnEnd();
}

async function executeEnemyPhase() {
  onEnemyTurnStart();

  const speedBonus = calculateSpeedBonus(enemySpeed, playerSpeed);
  // 速度ボーナスを一時バフとして適用

  const enemyEnergy = calculateEnemyEnergy(currentEnemy);
  await executeEnemyActions(/* ... */);

  onEnemyTurnEnd();
}
```

3. **完全なターンフロー**:
```typescript
async function executeCompleteTurn() {
  // 1. ターン開始
  setTurn(prev => prev + 1);

  // 2. 速度計算
  const playerSpeed = calculatePlayerSpeed(playerBuffs);
  const enemySpeed = calculateEnemySpeed(currentEnemy, enemyBuffs);

  // 3. ターン順序決定
  const firstActor = determineTurnOrder(playerSpeed, enemySpeed);

  // 4. 先攻側のフェーズ実行
  if (firstActor === "player") {
    await executePlayerPhase();
    if (isBattleEnd()) return;
    await executeEnemyPhase();
  } else {
    await executeEnemyPhase();
    if (isBattleEnd()) return;
    await executePlayerPhase();
  }
}
```

**実装状況**:
- [x] 速度計算関数のインポート
- [x] 速度関連のstate追加（playerSpeed, enemySpeed, turnOrder, speedBonusPlayer, speedBonusEnemy）
- [x] ターン開始時の速度計算ロジック追加
- [ ] 敵エナジー管理の追加（Phase 4-2で実装予定）
- [ ] プレイヤー/敵フェーズの完全分離（Phase 4-3で実装予定）
- [ ] 出血ダメージ適用（Phase 4-4で実装予定）

**テスト項目**:
- [x] 速度計算の正確性
- [x] ターン順序決定（同速の場合はプレイヤー優先）
- [x] 速度ボーナスの計算
- [ ] プレイヤー先攻時の正しいフロー
- [ ] 敵先攻時の正しいフロー
- [ ] 速度ボーナスの適用と削除
- [ ] 戦闘終了時の処理

---

### Phase 5: UI実装

**Status**: 未着手

#### 5.1 ターン順序インジケーター

**新規作成ファイル**: `/src/battles/battleUI/TurnOrderIndicator.tsx`

```tsx
interface TurnOrderIndicatorProps {
  playerSpeed: number;
  enemySpeed: number;
  firstActor: "player" | "enemy";
  playerBonus: SpeedBonus | null;
  enemyBonus: SpeedBonus | null;
}

export const TurnOrderIndicator: React.FC<TurnOrderIndicatorProps> = ({
  playerSpeed,
  enemySpeed,
  firstActor,
  playerBonus,
  enemyBonus,
}) => {
  return (
    <div className="turn-order-indicator">
      <div className={`actor ${firstActor === "player" ? "first" : "second"}`}>
        <div className="speed-value">{playerSpeed}</div>
        <div className="actor-name">Player</div>
        {playerBonus && (
          <div className="speed-bonus">{playerBonus.name}</div>
        )}
      </div>

      <div className="vs-icon">⚡</div>

      <div className={`actor ${firstActor === "enemy" ? "first" : "second"}`}>
        <div className="speed-value">{enemySpeed}</div>
        <div className="actor-name">Enemy</div>
        {enemyBonus && (
          <div className="speed-bonus">{enemyBonus.name}</div>
        )}
      </div>
    </div>
  );
};
```

**配置場所**: BattleScreen の右上または左上

---

#### 5.2 敵行動予告UI

**新規作成ファイル**: `/src/battles/battleUI/EnemyActionPreview.tsx`

```tsx
import type { EnemyAction } from "@/Character/data/EnemyData";

interface EnemyActionPreviewProps {
  actions: EnemyAction[];
  enemyEnergy: number;
}

export const EnemyActionPreview: React.FC<EnemyActionPreviewProps> = ({
  actions,
  enemyEnergy,
}) => {
  return (
    <div className="enemy-action-preview">
      <div className="preview-header">
        Next Actions ({enemyEnergy} Energy)
      </div>
      <div className="action-list">
        {actions.map((action, index) => (
          <div key={index} className="action-item">
            <span className="action-icon">{action.displayIcon}</span>
            <span className="action-name">{action.name}</span>
            <span className="action-damage">
              {action.baseDamage > 0 ? `${action.baseDamage} DMG` : ""}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
};
```

**配置場所**: 敵キャラクターの上部または横

---

#### 5.3 速度ボーナス表示

**更新ファイル**: `/src/components/StatusEffect.tsx`

速度ボーナスを一時バフとして表示する実装を追加。

---

### Phase 6: 出血システムの特殊実装 ✅

**Status**: 完了

#### 6.1 出血ダメージ計算 ✅

**完了ファイル**: `/src/battles/logic/bleedDamage.ts`

```typescript
import type { BuffDebuffMap } from "@/cards/type/baffType";

/**
 * 出血ダメージ計算（特殊実装）
 * プレイヤー: カード使用毎、敵: 1行動毎に呼び出される
 */
export function calculateBleedDamage(
  maxHp: number,
  buffDebuffs: BuffDebuffMap
): number {
  if (!buffDebuffs.has("bleed")) {
    return 0;
  }

  // 最大HPの5%
  return Math.floor(maxHp * 0.05);
}
```

#### 6.2 統合

**更新箇所**: Phase 4-4で実装予定

1. **プレイヤーのカード使用時** (`useBattleLogic.ts`):
```typescript
function handleCardPlay(card: Card) {
  // カード効果を適用
  // ...

  // 出血ダメージ
  const bleedDamage = calculateBleedDamage(playerMaxHp, playerBuffs);
  if (bleedDamage > 0) {
    setPlayerHp(prev => Math.max(0, prev - bleedDamage));
    // ダメージ表示アニメーション
  }
}
```

2. **敵の行動実行時** (`enemyActionExecution.ts`):
```typescript
async function onExecuteAction(action: EnemyAction) {
  // 行動を実行
  // ...

  // 出血ダメージ
  const bleedDamage = calculateBleedDamage(enemyMaxHp, enemyBuffs);
  if (bleedDamage > 0) {
    enemyHp -= bleedDamage;
    // ダメージ表示アニメーション
  }
}
```

**テスト項目**:
- [x] 出血ダメージ計算関数の実装
- [x] 最大HPの5%計算の正確性
- [ ] プレイヤーがbleed状態でカード使用時のダメージ（Phase 4-4で実装予定）
- [ ] 敵がbleed状態で行動時のダメージ（Phase 4-4で実装予定）

---

### Phase 7: テストと調整

**Status**: 未着手

#### 7.1 単体テスト

**テスト対象**:
- [ ] 速度計算関数
- [ ] ターン順序決定
- [ ] 速度ボーナス計算
- [ ] 敵エナジー計算
- [ ] 出血ダメージ計算

**ツール**: Jest / Vitest

---

#### 7.2 統合テスト

**テストシナリオ**:
1. **シナリオ1**: プレイヤー先攻、通常バトル
   - プレイヤー速度50、敵速度40
   - プレイヤーが先に行動
   - 敵が1回行動

2. **シナリオ2**: 敵先攻、速度差ボーナス
   - プレイヤー速度40、敵速度70（速度差30）
   - 敵が「先制」ボーナス獲得
   - 敵の攻撃力+15%

3. **シナリオ3**: 敵の複数行動
   - 敵のbaseEnemyEnergy = 2
   - 敵が2回連続で行動

4. **シナリオ4**: 出血ダメージ
   - プレイヤーにbleed付与
   - カード使用毎に最大HPの5%ダメージ

---

#### 7.3 バランス調整

**調整対象**:
- [ ] 敵の速度値（depth1全敵）
- [ ] 敵のbaseEnemyEnergy（強敵は2以上も検討）
- [ ] 速度差ボーナスの倍率（30差、50差）
- [ ] 出血ダメージの割合（5%が適切か）

**データ収集**:
- 実際のプレイテストで勝率を計測
- 平均ターン数
- プレイヤーの残HP

---

## 実装順序まとめ

1. ✅ Phase 1: 型定義とデータ構造の更新（完了）
2. ✅ Phase 2: 速度システムの実装（完了）
3. ✅ Phase 3: 敵の複数行動システム（完了）
4. ⏳ Phase 4: ターンフロー統合（進行中 - Phase 4-1完了、4-2以降実装予定）
5. ⏳ Phase 5: UI実装（未着手）
6. ✅ Phase 6: 出血システムの特殊実装（完了 - Phase 4-4で統合予定）
7. ⏳ Phase 7: テストと調整（未着手）

---

## リスクと対策

### リスク1: ターンフローの複雑化

**リスク**: プレイヤーフェーズと敵フェーズを分離することで、既存のバトルロジックとの整合性が取れなくなる可能性

**対策**:
- Phase 4で慎重に実装
- 既存の`handleEndTurn`を段階的にリファクタリング
- 十分な統合テストを実施

### リスク2: パフォーマンス低下

**リスク**: 敵の複数行動やアニメーション処理により、バトルの進行が遅くなる可能性

**対策**:
- アニメーション時間を調整可能にする（800ms → 設定で変更可能）
- スキップ機能の実装検討
- React.memoやuseMemoでレンダリング最適化

### リスク3: バランス崩壊

**リスク**: 速度システムや敵の複数行動により、ゲームバランスが大きく変わる可能性

**対策**:
- Phase 7で徹底的なバランス調整
- プレイテストを複数回実施
- パラメータを外部データ化して調整しやすくする

---

## 成果物

### コードファイル

- `/src/battles/logic/speedCalculation.ts` (新規)
- `/src/battles/logic/enemyActionExecution.ts` (新規)
- `/src/battles/logic/bleedDamage.ts` (新規)
- `/src/battles/logic/useBattleLogic.ts` (更新)
- `/src/battles/battleUI/TurnOrderIndicator.tsx` (新規)
- `/src/battles/battleUI/EnemyActionPreview.tsx` (新規)
- `/src/cards/type/baffType.ts` (更新)
- `/src/Character/data/EnemyData.ts` (更新済み)

### ドキュメント

- `/blueprint/battle_document/battle_logic.md` Ver 4.0 (更新済み)
- `/blueprint/IMPLEMENTATION_PLAN_V4.md` (本ドキュメント)

### テストファイル

- `/src/battles/logic/__tests__/speedCalculation.test.ts` (新規)
- `/src/battles/logic/__tests__/enemyActionExecution.test.ts` (新規)
- `/src/battles/logic/__tests__/bleedDamage.test.ts` (新規)

---

## Next Steps

1. **Phase 2の着手**: 速度計算ロジックの実装から開始
2. **段階的な実装**: 各Phaseを完了後、動作確認してから次へ進む
3. **継続的なテスト**: Phase 7を待たずに、各Phase完了時にテストを実施

---

**Version**: 1.1
**Created**: 2025-12-31
**Last Updated**: 2025-12-31
**Author**: こうだい
**Status**: Phase 1-3, 6完了、Phase 4進行中（4-1完了、4-2実装予定）
