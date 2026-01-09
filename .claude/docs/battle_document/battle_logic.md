# BATTLE SYSTEM LOGIC SPECIFICATION (Ver 4.0)

## 1. 概要

本ドキュメントは「ローグライトカード RPG」のコアバトルシステムの論理仕様である。
以下の主要システムを定義する：

1. **非対称バトルフロー**: プレイヤーと敵で異なるエナジーシステム
2. **行動速度システム**: ターン順序決定と先攻ボーナス
3. **敵の複数行動システム**: エナジーによる行動回数制御
4. **防御システム**: AP（装備耐久）と Guard（一時防御）の分離
5. **バフ/デバフシステム**: 簡素化されたバフ/デバフとその効果
6. **ダメージ計算**: バフ/デバフを含む包括的なダメージ計算
7. **状態異常管理**: 持続時間、スタック、効果値の管理
8. **深度システム**: 深度による敵種類の変化

---

## 2. バトルフローシステム (Ver 4.0 新規)

### 2.1 非対称エナジーシステム

プレイヤーと敵で異なるエナジーの概念を採用する。

#### プレイヤー側

```
Energy = カードコスト支払い用リソース（従来通り）
- 基本エナジー: 3
- 1ターンに手札から好きなだけカードをプレイ可能
- エナジーが続く限り行動可能
- カードコストシステムはそのまま維持
```

#### 敵側

```
Enemy Energy = 行動回数
- 1エナジー = 1回の行動（攻撃、バフ、防御など）
- 深度・敵の種類によってエナジーが変化
```

### 2.2 敵エナジーの計算

```typescript
/**
 * 敵のエナジー計算（行動回数）
 */
function calculateEnemyEnergy(enemy: Enemy, addEnergy: number): number {
  const baseEnergy = enemy.actEnergy;
  // energyAddActionは敵のスキルや行動によって変動
  return Math.floor(baseEnergy * addEnergy);
}
```

### 2.3 敵の行動定義

```typescript
export interface EnemyAction {
  name: string; // 行動名
  type: EnemyActionType; // 行動タイプ
  baseDamage: number; // 基本ダメージ
  applyDebuffs?: BuffDebuff[]; // 付与するデバフ
  applyBuffs?: BuffDebuff[]; // 付与するバフ
  guardGain?: number; // Guard獲得量
  hitCount?: number; // 攻撃回数

  // Ver 4.0 新規追加
  displayIcon?: string; // UI表示用アイコン
  priority: number; // 行動優先度（高いほど優先）
  energyCost: number; // エナジーコスト（デフォルト1）
}

export interface Enemy {
  id: string;
  name: string;
  nameJa: string;
  description: string;

  // 基礎ステータス
  maxHp: number;
  maxAp: number;
  startingGuard: number;
  evasionRate: number;
  immunities: string[];

  // Ver 4.0 新規追加
  baseEnemyEnergy: number; // 基本エナジー（行動回数）
  speed: number; // 行動速度（0-100）

  // AI パターン
  aiPatterns: EnemyAIPattern[]; //AIパターンは複雑化しやすいため初期テスト時には固定の動きのみ
  imagePath?: string;
}
```

---

## 3. 行動速度システム (Ver 4.0 新規)

### 3.1 速度パラメータ

プレイヤーと敵の両方が「速度」パラメータを持つ。

```typescript
interface SpeedStats {
  baseSpeed: number; // 基本速度（プレイヤー: 50、敵: 固有値）
  currentSpeed: number; // バフ/デバフ適用後の速度
}
```

### 3.2 速度計算

```typescript
/**
 * プレイヤーの速度計算
 */
function calculatePlayerSpeed(buffs: BuffDebuffMap): number {
  let speed = player.speed; // 基本速度

  // 速度上昇バフ
  if (buffs.has("speedUp")) {
    const speedBuff = buffs.get("speedUp")!;
    speed += speedBuff.value * speedBuff.stacks;
  }

  // スロウデバフ
  if (buffs.has("slow")) {
    const slowDebuff = buffs.get("slow")!;
    speed -= slowDebuff.value * 10; // -10/スタック
  }

  // 速度低下デバフ
  if (buffs.has("speedDown")) {
    const speedDown = buffs.get("speedDown")!;
    speed -= speedDown.value;
  }

  return Math.max(0, speed);
}

/**
 * 敵の速度計算
 */
function calculateEnemySpeed(enemy: Enemy, buffs: BuffDebuffMap): number {
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

  return Math.max(0, speed);
}
```

### 3.3 ターン順序決定

```typescript
/**
 * ターン順序を決定
 */
function determineTurnOrder(
  playerSpeed: number,
  enemySpeed: number
): "player" | "enemy" {
  if (playerSpeed >= enemySpeed) {
    return "player";
  } else if (enemySpeed > playerSpeed) {
    return "enemy";
  }
}
```

### 3.4 速度差によるボーナス

```typescript
/**
 * 速度差ボーナスの計算
 */
function calculateSpeedBonus(
  actorSpeed: number,
  targetSpeed: number
): SpeedBonus {
  const speedDiff = actorSpeed - targetSpeed;

  if (speedDiff >= 50) {
    return {
      name: "電光石火",
      attackBonus: 0.15, // 攻撃力+15%
      criticalBonus: 0.2, // クリティカル率+20%
    };
  } else if (speedDiff >= 30) {
    return {
      name: "先制",
      attackBonus: 0.15, // 攻撃力+15%
      criticalBonus: 0,
    };
  }

  return null; // ボーナスなし
}
```

---

## 4. ターンフロー (Ver 4.0 更新)

### 4.1 ターン全体の流れ

```typescript
function executeCompleteTurn() {
  // 1. ターン開始処理
  incrementTurnCounter();

  // 2. 速度計算
  const playerSpeed = calculatePlayerSpeed(playerBuffs);
  const enemySpeed = calculateEnemySpeed(currentEnemy, enemyBuffs);

  // 3. ターン順序決定
  const firstActor = determineTurnOrder(playerSpeed, enemySpeed);

  // 4. 先攻側のフェーズ実行
  if (firstActor === "player") {
    await executePlayerPhase(playerSpeed, enemySpeed);
    if (isBattleEnd()) return;
    await executeEnemyPhase(enemySpeed, playerSpeed);
  } else {
    await executeEnemyPhase(enemySpeed, playerSpeed);
    if (isBattleEnd()) return;
    await executePlayerPhase(playerSpeed, enemySpeed);
  }

  // 5. ターン終了処理
  onTurnEnd();
}
```

### 4.2 プレイヤーフェーズ

```typescript
async function executePlayerPhase(
  playerSpeed: number,
  enemySpeed: number
): Promise<void> {
  // 1. フェーズ開始処理
  onPlayerTurnStart();

  // 2. 速度ボーナス適用
  const speedBonus = calculateSpeedBonus(playerSpeed, enemySpeed);
  if (speedBonus) {
    applyTemporaryBuff("speedBonus", speedBonus);
  }

  // 3. プレイヤーの行動待機
  // ユーザーがカードをプレイし、End Turnボタンを押すまで待機
  await waitForPlayerAction();

  // 4. フェーズ終了処理
  onPlayerTurnEnd();

  // 5. 速度ボーナス削除
  if (speedBonus) {
    removeTemporaryBuff("speedBonus");
  }
}

function onPlayerTurnStart(): void {
  // 1. Guardの消滅
  setPlayerGuard(0);

  // 2. バフ/デバフの持続時間減少
  decreaseBuffDebuffDuration(playerBuffs);
  decreaseBuffDebuffDuration(enemyBuffs);

  // 3. 再生・シールド再生処理
  const healing = calculateStartTurnHealing(playerBuffs);
  applyHealing(healing.hp);
  applyShield(healing.shield);

  // 4. エナジー回復
  const energyGain = calculateEnergyGain(playerBuffs);
  setEnergy(energyGain);

  // 5. カードドロー
  const drawCount = calculateDrawCount(playerBuffs);
  drawCards(drawCount);

  // 6. 自動浄化
  if (playerBuffs.has("cleanse")) {
    const cleanse = playerBuffs.get("cleanse")!;
    removeDebuffs(playerBuffs, cleanse.value * cleanse.stacks);
  }

  // 7. 行動不可チェック
  if (!canAct(playerBuffs)) {
    autoEndTurn();
  }
}

function onPlayerTurnEnd(): void {
  // 1. 持続ダメージ処理
  const dotDamage = calculateEndTurnDamage(playerBuffs);
  applyDamageToPlayer(dotDamage);

  // 2. Momentum（勢い）のスタック増加
  if (playerBuffs.has("momentum")) {
    const momentum = playerBuffs.get("momentum")!;
    momentum.stacks += 1;
  }
}
```

### 4.3 敵フェーズ (Ver 4.0 新規)

```typescript
async function executeEnemyPhase(
  enemySpeed: number,
  playerSpeed: number
): Promise<void> {
  // 1. フェーズ開始処理
  onEnemyTurnStart();

  // 2. 速度ボーナス適用
  const speedBonus = calculateSpeedBonus(enemySpeed, playerSpeed);
  if (speedBonus) {
    applyTemporaryBuff("enemySpeedBonus", speedBonus);
  }

  // 3. 敵のエナジー計算
  const baseEnergy = currentEnemy.baseEnemyEnergy;
  const finalEnergy = applyEnemyEnergyModifiers(baseEnergy, enemyBuffs);

  // 4. 行動実行
  await executeEnemyActions(finalEnergy);

  // 5. フェーズ終了処理
  onEnemyTurnEnd();

  // 6. 速度ボーナス削除
  if (speedBonus) {
    removeTemporaryBuff("enemySpeedBonus");
  }
}

function onEnemyTurnStart(): void {
  // 1. Guardの消滅
  setEnemyGuard(0);

  // 2. 再生・シールド再生処理
  const healing = calculateStartTurnHealing(enemyBuffs);
  enemyHp = Math.min(enemyMaxHp, enemyHp + healing.hp);
  enemyGuard += healing.shield;

  // 3. 行動不可チェック
  if (!canAct(enemyBuffs)) {
    skipEnemyTurn();
  }
}

function onEnemyTurnEnd(): void {
  // 1. 持続ダメージ処理
  const dotDamage = calculateEndTurnDamage(enemyBuffs);
  enemyHp -= dotDamage;
}
```

### 4.4 敵の行動実行ロジック (Ver 4.0 新規)

```typescript
/**
 * 敵のエナジー分の行動を実行
 */
async function executeEnemyActions(enemyEnergy: number): Promise<void> {
  let remainingEnergy = enemyEnergy;
  const actionsToExecute: EnemyAction[] = [];

  // エナジーが尽きるまで行動を選択
  while (remainingEnergy > 0) {
    const action = determineEnemyAction(
      currentEnemy,
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

  // 行動を順次実行（アニメーション付き）
  for (let i = 0; i < actionsToExecute.length; i++) {
    showMessage(
      `${currentEnemy.nameJa}の行動 ${i + 1}/${actionsToExecute.length}`
    );

    await executeEnemyAction(actionsToExecute[i]);

    // 行動間のディレイ
    await delay(800);

    // 戦闘終了チェック
    if (isBattleEnd()) {
      break;
    }
  }
}

/**
 * 単一の敵行動を実行
 */
async function executeEnemyAction(action: EnemyAction): Promise<void> {
  showActionPreview(action);

  switch (action.type) {
    case "attack":
      await executeEnemyAttack(action);
      break;
    case "buff":
      await executeEnemyBuff(action);
      break;
    case "debuff":
      await executeEnemyDebuff(action);
      break;
    case "special":
      await executeEnemySpecial(action);
      break;
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
      energyCost: 1,
    };
  }

  // エナジー0の場合はスキップ
  return {
    name: "待機",
    type: "special",
    baseDamage: 0,
    displayIcon: "💤",
    energyCost: 0,
  };
}
```

---

## 5. 防御システム (Hybrid Defense System)

防御機構を「持ち越し可能な装備耐久」と「ターンごとの防御行動」に分離する。

### 5.1 定義

- **HP (Health Points):** キャラクターの生命力。0 になると死亡。
- **AP (Armor Points):** 装備の耐久値。
  - 戦闘終了後も**現在値が次回戦闘へ持ち越される**。
  - 最大値は装備アイテムの性能に依存。
  - 原則として戦闘中に自動回復しない（修理カード/アイテムが必要）。
- **GP (Guard Points):** カード効果による一時的な防御壁。
  - **各キャラクターのターン開始時に消滅する**（0 になる）。
  - AP を守るための手段や、AP が亡くなった時の HP ダメージの緩和として機能する。

### 5.2 ダメージ受容優先度

基本原則として、以下の順序でダメージを減算する。

1. **Guard**（盾で防ぐ）
2. **AP**（鎧で受ける）
3. **HP**（肉体で受ける）

### 5.3 アーマーブレイク (Armor Break)

- **条件:** `AP` が `0` になった状態。
- **効果 (貫通ペナルティ):**
  - AP が 0 の状態では、**攻撃ダメージの 50%が Guard を無視して直接 HP にヒットする**。
  - 残りの 50%は通常通り Guard で受ける。
  - _意図:_ 鎧が壊れた生身の状態では、盾の上から衝撃が通るリアリティの表現。

---

## 6. バフ/デバフシステム

### 6.1 バフ/デバフの基本構造

```typescript
interface BuffDebuff {
  type: BuffDebuffType; // バフ/デバフの種類
  stacks: number; // スタック数（重ね掛け）
  duration: number; // 残りターン数
  value: number; // 効果値（倍率やダメージ量）
  isPermanent: boolean; // 永続フラグ
  source?: string; // 発生源（カードID、装備IDなど）
}
type BuffDebuffMap = Map<BuffDebuffType, BuffDebuff>;
```

バフデバフの種類などは
**roguelike-card-battle/blueprint/battle_document/buff_debuff_system.md** を確認

### 6.2 スタックシステム

```typescript
/**
 * バフ/デバフを追加または更新
 */
function addOrUpdateBuffDebuff(
  map: BuffDebuffMap,
  type: BuffDebuffType,
  stacks?: number,
  duration: number,
  value: number,
  isPermanent: boolean = false,
  source?: string
): BuffDebuffMap {
  const newMap = new Map(map);
  const existing = newMap.get(type);

  if (existing) {
    // 既存のバフ/デバフがある場合、スタックを加算
    newMap.set(type, {
      ...existing,
      stacks: existing.stacks + stacks,
      duration: Math.max(existing.duration, duration), // 長い方を採用
      value: Math.max(existing.value, value), // 大きい方を採用
    });
  } else {
    // 新規追加
    newMap.set(type, {
      type,
      stacks,
      duration,
      value,
      isPermanent,
      source,
    });
  }

  return newMap;
}
```

### 6.4 持続時間管理

```typescript
/**
 * ターン経過による持続時間減少
 */
function decreaseBuffDebuffDuration(map: BuffDebuffMap): BuffDebuffMap {
  const newMap = new Map<BuffDebuffType, BuffDebuff>();

  map.forEach((buff, type) => {
    if (buff.isPermanent) {
      // 永続は変更なし
      newMap.set(type, buff);
    } else if (buff.duration > 1) {
      // 持続時間を減少
      newMap.set(type, {
        ...buff,
        duration: buff.duration - 1,
      });
    }
    // duration === 1 の場合は削除（新Mapに追加しない）
  });

  return newMap;
}
```

---

## 7. ダメージ計算ロジック (Damage Formula)

### 7.1 計算フロー

攻撃発生時、以下のアルゴリズムで最終ダメージを決定する。

```typescript
interface Character {
  hp: number;
  ap: number;
  guard: number;
  buffDebuffs: BuffDebuffMap;
  equipment_def_percent: number;
}

interface Card {
  power: number;
  category: "atk" | "def" | "buff" | "debuff" | "defense" | "heal";
  // その他のカード情報
}

interface DamageResult {
  finalDamage: number;
  isCritical: boolean;
  penetrationDamage: number;
  reflectDamage: number;
  lifestealAmount: number;
}

/**
 * ダメージ計算メイン関数
 */
function calculateDamage(
  attacker: Character,
  defender: Character,
  card: Card
): DamageResult {
  // --- Phase 1: 基本攻撃力計算 ---
  const baseDmg = card.power;

  // --- Phase 2: バフ/デバフによる攻撃力補正 ---
  let atkMultiplier = 1.0;

  // 攻撃力上昇バフ
  atkMultiplier += calculateAttackMultiplier(attacker.buffDebuffs);

  // 攻撃力低下デバフ
  if (attacker.buffDebuffs.has("weak")) {
    atkMultiplier *= 0.7; // -30%
  }

  if (attacker.buffDebuffs.has("atkDown")) {
    const atkDown = attacker.buffDebuffs.get("atkDown")!;
    atkMultiplier *= 1 - atkDown.value / 100;
  }

  // --- Phase 3: クリティカル判定 ---
  let critMod = 1.0;
  const critRate = calculateCriticalRate(attacker.buffDebuffs);
  const isCritical =
    Math.random() < critRate && !attacker.buffDebuffs.has("weak");

  if (isCritical) {
    critMod = 1.5; // 基本クリティカルダメージ

    // クリティカルダメージボーナス
    if (attacker.buffDebuffs.has("critical")) {
      const critBuff = attacker.buffDebuffs.get("critical")!;
      critMod += critBuff.value / 100;
    }
  }

  // --- Phase 4: キャラクター固有バフ ---
  // 剣士: 剣気ダメージ
  // 魔術士: 共鳴ボーナス
  // 召喚士: 召喚強化
  // （これらは別途処理）

  const finalAtk = Math.floor(baseDmg * atkMultiplier * critMod);

  // --- Phase 5: 装備DEF軽減 ---
  const defMitigation = defender.equipment_def_percent;

  const incomingDmg = Math.floor(finalAtk * (1.0 - defMitigation));

  // --- Phase 6: ダメージ配分 ---
  const { penetrationDamage, actualDamage } = applyDamageAllocation(
    defender,
    incomingDmg
  );

  // --- Phase 7: 特殊効果処理 ---
  // 反撃ダメージ
  const reflectDamage = calculateReflectDamage(
    defender.buffDebuffs,
    actualDamage
  );

  // 吸血回復
  const lifestealAmount = calculateLifesteal(
    attacker.buffDebuffs,
    actualDamage
  );

  // 棘の鎧ダメージ
  if (defender.buffDebuffs.has("thorns") && card.category === "physical") {
    const thorns = defender.buffDebuffs.get("thorns")!;
    const thornsDamage = thorns.value * thorns.stacks;
    // 攻撃者にダメージ（別途処理）
  }

  return {
    finalDamage: incomingDmg,
    isCritical,
    penetrationDamage,
    reflectDamage,
    lifestealAmount,
  };
}
```

### 7.2 バフ/デバフ計算関数

```typescript
/**
 * 攻撃力の倍率計算
 */
function calculateAttackMultiplier(buffDebuffs: BuffDebuffMap): number {
  let multiplier = 0;

  if (buffDebuffs.has("atkUp")) {
    const buff = buffDebuffs.get("atkUp")!;
    multiplier += buff.value / 100;
  }

  return multiplier;
}

/**
 * クリティカル率の計算
 */
function calculateCriticalRate(buffDebuffs: BuffDebuffMap): number {
  let rate = 0.1; // 基本クリティカル率10%

  if (buffDebuffs.has("critical")) {
    const buff = buffDebuffs.get("critical")!;
    rate += buff.value / 100;
  }

  return Math.min(0.8, rate); // 最大80%
}

/**
 * 反撃ダメージ計算
 */
function calculateReflectDamage(
  buffDebuffs: BuffDebuffMap,
  damage: number
): number {
  let reflectDamage = 0;

  if (buffDebuffs.has("reflect")) {
    const reflect = buffDebuffs.get("reflect")!;
    reflectDamage = Math.floor(damage * (reflect.value / 100));
  }

  return reflectDamage;
}

/**
 * 吸血回復計算
 */
function calculateLifesteal(
  buffDebuffs: BuffDebuffMap,
  damage: number
): number {
  let healAmount = 0;

  if (buffDebuffs.has("lifesteal")) {
    const lifesteal = buffDebuffs.get("lifesteal")!;
    healAmount = Math.floor(damage * (lifesteal.value / 100));
  }

  return healAmount;
}
```

### 7.3 ダメージ配分ロジック

- ダメージを Guard → AP → HP の順に配分
- AP === 0 (アーマーブレイク)の時、Guard > 0 でもダメージは HP に 50%貫通する

---

## 8. バフ/デバフ計算関数

### 8.1 ターン終了時の持続ダメージ

```typescript
/**
 * ターン終了時の持続ダメージ計算
 */
function calculateEndTurnDamage(buffDebuffs: BuffDebuffMap): number {
  let totalDamage = 0;

  if (buffDebuffs.has("poison")) {
    const poison = buffDebuffs.get("poison")!;
    totalDamage += poison.stacks * 2;
  }

  if (buffDebuffs.has("curse")) {
    const curse = buffDebuffs.get("curse")!;
    totalDamage += curse.stacks * 2;
  }
  return totalDamage;
}

/**
 * 出血ダメージ計算（特殊実装）
 * プレイヤー: カード使用毎、敵: 1行動毎に呼び出される
 */
function calculateBleedDamage(
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

### 8.2 ターン開始時の回復・再生

```typescript
/**
 * ターン開始時の回復・再生計算
 */
function calculateStartTurnHealing(buffDebuffs: BuffDebuffMap): {
  hp: number;
  shield: number;
} {
  let hp = 0;
  let shield = 0;

  if (buffDebuffs.has("regeneration")) {
    const regen = buffDebuffs.get("regeneration")!;
    hp += regen.value * regen.stacks;
  }

  if (buffDebuffs.has("shieldRegen")) {
    const shieldRegen = buffDebuffs.get("shieldRegen")!;
    shield += shieldRegen.value * shieldRegen.stacks;
  }

  // 呪いの回復効果減少
  if (buffDebuffs.has("curse")) {
    hp = Math.floor(hp * 0.5);
  }

  if (buffDebuffs.has("healingDown")) {
    const healingDown = buffDebuffs.get("healingDown")!;
    hp = Math.floor(hp * (1 - healingDown.value / 100));
  }

  return { hp, shield };
}
```

### 8.3 デバフ解除

```typescript
/**
 * 指定数のデバフを解除
 */
function removeDebuffs(buffDebuffs: BuffDebuffMap, count: number): void {
  const debuffs: BuffDebuffType[] = [];

  buffDebuffs.forEach((buff, type) => {
    // デバフ判定は BuffDebuffEffects を参照
    if (isDebuff(type)) {
      debuffs.push(type);
    }
  });

  // ランダムまたは優先度順で解除
  for (let i = 0; i < Math.min(count, debuffs.length); i++) {
    buffDebuffs.delete(debuffs[i]);
  }
}
```

---

## 9. ダンジョン深度システム (Depth System)

### 9.1 深度の概念

深度は**敵の種類（強さ）のみ**を決定する。

各深度には固有の敵プールが存在し、深度が上がるほど強力な敵が出現する。
敵のステータス（HP、エナジー、速度など）は敵個体データで直接定義される。

| 深度 | 名称 | 出現する敵の特徴                 |
| ---- | ---- | -------------------------------- |
| 1    | 腐食 | 基本的な敵、単純な行動パターン   |
| 2    | 狂乱 | デバフを使用する敵が増加         |
| 3    | 混沌 | 2 エナジー敵、複雑な行動パターン |
| 4    | 虚無 | 高 HP・高エナジー敵、強力な攻撃  |
| 5    | 深淵 | 最強の敵、ボス級の能力           |

### 9.2 深度と敵の関係

```typescript
interface DepthInfo {
  depth: number;
  name: string;
  description: string;
}

const DEPTH_INFO: Record<number, DepthInfo> = {
  1: { depth: 1, name: "腐食", description: "汚染の始まり" },
  2: { depth: 2, name: "狂乱", description: "理性の喪失" },
  3: { depth: 3, name: "混沌", description: "秩序の崩壊" },
  4: { depth: 4, name: "虚無", description: "存在の消失" },
  5: { depth: 5, name: "深淵", description: "終焉の深淵" },
};

/**
 * 深度情報を取得
 */
function getDepthInfo(depth: number): DepthInfo {
  const info = DEPTH_INFO[depth];
  if (!info) {
    throw new Error(`Invalid depth: ${depth}`);
  }
  return info;
}

/**
 * 深度に応じた敵プールから敵を選択
 * 敵のステータスは敵データで直接定義される（倍率計算なし）
 */
function selectEnemyForDepth(
  depth: number,
  encounterType: "normal" | "elite" | "boss"
): Enemy {
  const enemyPool = getEnemyPoolForDepth(depth, encounterType);
  return enemyPool[Math.floor(Math.random() * enemyPool.length)];
}
```

### 9.3 ダメージ計算への深度の影響（廃止）

**Ver 4.0 で変更:** 深度による自動的なダメージ倍率は廃止。

- 深度 1 の敵: HP 40, baseDamage 7
- 深度 3 の敵: HP 100, baseDamage 18
- 深度 5 の敵: HP 250, baseDamage 40

このように、敵データで直接強さを定義する。

---

## 10. 実装上の注意点

### 10.1 用語の区別

- **Depth (深度)**: ダンジョンの階層（敵の強さ）
- **Duration (持続時間)**: バフ/デバフの残りターン数
- **Stacks (スタック)**: バフ/デバフの重ね掛け数
- **cardEnergy (プレイヤー)**: カードコスト支払い用リソース
- **actEnergy (敵)**: 行動回数を表すリソース
- **Speed (速度)**: 行動順序を決定するパラメータ

### 10.2 バフ/デバフの優先度

```
【ダメージ計算時の適用順序】
1. 基本攻撃力
2. 攻撃力上昇バフ (atkUp)
3. 攻撃力低下デバフ (weak, atkDown)
4. クリティカル判定
5. 装備DEF軽減
6. 反撃・吸血処理
```

### 10.3 Guard の特殊処理

- プレイヤーターン開始時に必ず 0 になる
- 敵ターン開始時にも 0 になる
- 戦闘終了後は引き継がれない
- アーマーブレイク時は 50%貫通される

### 10.4 持続ダメージの処理

```typescript
/**
 * 持続ダメージは防御を無視してHPに直接ダメージ
 */
function applyDoTDamage(character: Character): void {
  const dotDamage = calculateEndTurnDamage(character.buffDebuffs);

  // Guard、APを無視してHPに直接ダメージ
  character.hp -= dotDamage;
}
```

### 10.5 戦闘終了時の状態保存

```typescript
interface BattleEndState {
  currentAp: number;
  maxAp: number;
  currentHp: number;
  // バフ/デバフは保存しない（戦闘終了で消滅）
}

function saveBattleState(player: Character): BattleEndState {
  return {
    currentAp: player.ap,
    maxAp: player.maxAp,
    currentHp: player.hp,
  };
}

function loadBattleState(player: Character, savedState: BattleEndState): void {
  player.ap = savedState.currentAp;
  player.maxAp = savedState.maxAp;
  player.hp = savedState.currentHp;
  player.guard = 0;
  player.buffDebuffs = new Map(); // バフ/デバフはクリア
}
```

---

## 11. UI 表示のための予測計算

### 11.1 ダメージ予測

```typescript
interface DamagePreview {
  totalDamage: number;
  guardDamage: number;
  apDamage: number;
  hpDamage: number;
  penetrationDamage: number;
  isArmorBreak: boolean;
  isCritical: boolean;
  reflectDamage: number;
  lifestealAmount: number;
}

function calculateDamagePreview(
  attacker: Character,
  defender: Character,
  card: Card,
  currentDepth: number
): DamagePreview {
  // ダメージ計算（実際には適用しない）
  const result = calculateDamage(attacker, defender, card, currentDepth);

  // 配分をシミュレート
  let remainingDmg = result.finalDamage;
  let guardDmg = 0;
  let apDmg = 0;
  let hpDmg = 0;
  let penetrationDmg = 0;

  const isArmorBreak = defender.ap <= 0;

  if (isArmorBreak) {
    penetrationDmg = Math.floor(remainingDmg * 0.5);
    hpDmg += penetrationDmg;
    remainingDmg -= penetrationDmg;
  }

  if (defender.guard > 0) {
    guardDmg = Math.min(defender.guard, remainingDmg);
    remainingDmg -= guardDmg;
  }

  if (remainingDmg > 0 && defender.ap > 0) {
    apDmg = Math.min(defender.ap, remainingDmg);
    remainingDmg -= apDmg;
  }

  if (remainingDmg > 0) {
    hpDmg += remainingDmg;
  }

  return {
    totalDamage: result.finalDamage,
    guardDamage: guardDmg,
    apDamage: apDmg,
    hpDamage: hpDmg,
    penetrationDamage: penetrationDmg,
    isArmorBreak: isArmorBreak,
    isCritical: result.isCritical,
    reflectDamage: result.reflectDamage,
    lifestealAmount: result.lifestealAmount,
  };
}
```

### 11.2 ターン順序予測表示 (Ver 4.0 新規)

```typescript
interface TurnOrderPreview {
  playerSpeed: number;
  enemySpeed: number;
  firstActor: "player" | "enemy";
  speedDifference: number;
  playerBonus: SpeedBonus | null;
  enemyBonus: SpeedBonus | null;
}

function calculateTurnOrderPreview(
  playerBuffs: BuffDebuffMap,
  enemyBuffs: BuffDebuffMap,
  currentEnemy: Enemy
): TurnOrderPreview {
  const playerSpeed = calculatePlayerSpeed(playerBuffs);
  const enemySpeed = calculateEnemySpeed(currentEnemy, enemyBuffs);

  const firstActor = determineTurnOrder(playerSpeed, enemySpeed);
  const speedDifference = Math.abs(playerSpeed - enemySpeed);

  let playerBonus = null;
  let enemyBonus = null;

  if (firstActor === "player") {
    playerBonus = calculateSpeedBonus(playerSpeed, enemySpeed);
  } else {
    enemyBonus = calculateSpeedBonus(enemySpeed, playerSpeed);
  }

  return {
    playerSpeed,
    enemySpeed,
    firstActor,
    speedDifference,
    playerBonus,
    enemyBonus,
  };
}
```

### 11.3 敵の次の行動予告 (Ver 4.0 新規)

```typescript
interface EnemyActionPreview {
  actions: EnemyAction[];
  totalEnergy: number;
  displayLevel: "full" | "partial" | "minimal";
}

/**
 * 敵の次のターンの行動を予告
 */
function previewEnemyActions(
  enemy: Enemy,
  currentHp: number,
  nextTurn: number
): EnemyActionPreview {
  // エナジー計算
  const totalEnergy = enemy.baseEnemyEnergy;

  // 行動決定（予告用）
  const actions: EnemyAction[] = [];
  let remainingEnergy = totalEnergy;

  while (remainingEnergy > 0) {
    const action = determineEnemyAction(
      enemy,
      currentHp,
      enemy.maxHp,
      nextTurn,
      remainingEnergy
    );

    const actionCost = action.energyCost ?? 1;

    if (actionCost > remainingEnergy) {
      break;
    }

    actions.push(action);
    remainingEnergy -= actionCost;
  }

  return {
    actions,
    totalEnergy,
    displayLevel: "full",
  };
}
```

---

## 12. Ver 4.0 変更履歴

### 新機能追加

- **非対称エナジーシステム**: プレイヤー（カードコスト用）と敵（行動回数）で異なるエナジー概念を導入
- **行動速度システム**: 速度パラメータによるターン順序決定
- **速度差ボーナス**: 速度差 30 以上で「先制」、50 以上で「電光石火」ボーナス
- **敵の複数行動システム**: 敵エナジーによる 1 ターン内の複数回行動
- **ターン順序 UI**: 次のターンの行動順を可視化
- **敵の行動予告**: 敵の次の行動を事前表示

**追加されたバフ/デバフ:**

- `speedUp`: 速度+value
- `speedDown`: 速度-value
- `haste`: 速度+30（先制確定級）

**特殊実装に変更:**

- `bleed（出血）`: プレイヤーはカード使用毎に最大 HP の 5%、敵は 1 行動毎に最大 HP の 5%ダメージ

### 敵データ構造拡張

- `baseEnemyEnergy`: 敵の基本エナジー（行動回数）
- `speed`: 行動速度値（0-100）
- `energyCost`: 各行動のエナジーコスト
- `displayIcon`: UI 表示用アイコン
- `priority`: 行動優先度

### システム仕様変更

- **深度スケーリング廃止**: 深度による魔力倍率、物理倍率、HP 倍率、エナジー倍率を全て削除
- **深度の役割変更**: 深度は敵の種類（どの敵プールから選択するか）のみを決定
- **敵ステータスの直接定義**: 各敵の HP、ダメージ、エナジーは敵データで直接設定
- **Guard 消滅タイミング**: 各キャラクターのターン開始時に 0 に
- **slow デバフ変更**: エナジー-1 → 速度-10/スタック
- **ダメージ計算簡素化**: 深度補正を削除、防御関連バフの統合

---

**Version:** 4.0
**Updated:** 2025-12-31
**Status:** 設計完了、実装待ち
