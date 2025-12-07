# BATTLE SYSTEM LOGIC SPECIFICATION (Ver 3.0)

## 1. 概要

本ドキュメントは「ローグライトカード RPG」のコアバトルシステムの論理仕様である。
以下の主要システムを定義する：

1. **防御システム**: AP（装備耐久）と Guard（一時防御）の分離
2. **バフ/デバフシステム**: 44 種類のバフ/デバフとその効果
3. **ダメージ計算**: バフ/デバフを含む包括的なダメージ計算
4. **状態異常管理**: 持続時間、スタック、効果値の管理
5. **深度スケーリング**: ダンジョン深度による難易度変化

---

## 2. 防御システム (Hybrid Defense System)

防御機構を「持ち越し可能な装備耐久」と「ターンごとの防御行動」に分離する。

### 2.1 定義

- **HP (Health Points):** キャラクターの生命力。0 になると死亡。
- **AP (Armor Points):** 装備の耐久値。
  - 戦闘終了後も**現在値が次回戦闘へ持ち越される**。
  - 最大値は装備アイテムの性能に依存。
  - 原則として戦闘中に自動回復しない（修理カード/アイテムが必要）。
- **GP (Guard Points):** カード効果による一時的な防御壁。
  - **プレイヤーターンの開始時に残っていれば消滅する**（0 になる）。
  - AP を守るための手段や、AP が亡くなった時の HP ダメージの緩和として機能する。

### 2.2 ダメージ受容優先度

基本原則として、以下の順序でダメージを減算する。

1. **Guard**（盾で防ぐ）
2. **AP**（鎧で受ける）
3. **HP**（肉体で受ける）

### 2.3 アーマーブレイク (Armor Break)

- **条件:** `AP` が `0` になった状態。
- **効果 (貫通ペナルティ):**
  - AP が 0 の状態では、**敵攻撃ダメージの 50%が Guard を無視して直接 HP にヒットする**。
  - 残りの 50%は通常通り Guard で受ける。
  - _意図:_ 鎧が壊れた生身の状態では、盾の上から衝撃が通るリアリティの表現。

---

## 3. バフ/デバフシステム

### 3.1 バフ/デバフの基本構造

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

### 3.2 バフ/デバフのカテゴリ

#### A. デバフ - 持続ダメージ系

```
burn（火傷）:        毎ターン終了時、スタック×3ダメージ（シールド無視）
bleed（出血）:       毎ターン終了時、スタック×2ダメージ
poison（毒）:        毎ターン終了時、スタック×2ダメージ（防御無視）
curse（呪い）:       回復効果-50%、毎ターン終了時スタック×2ダメージ
```

#### B. デバフ - 状態異常系

```
slow（スロウ）:      エナジー-1
freeze（凍結）:      行動不可
paralyze（麻痺）:    攻撃力-50%
stun（気絶）:        行動不可
weak（弱体化）:      攻撃力-30%
```

#### C. デバフ - 能力減少系

```
defDown（防御力低下）:     防御力がvalue%低下
atkDown（攻撃力低下）:     攻撃力がvalue%低下
healingDown（回復効果減少）: 回復効果がvalue%減少
```

#### D. バフ - 能力上昇系

```
atkUp（攻撃力上昇）:        攻撃力がvalue%上昇
defUp（防御力上昇）:        防御力がvalue%上昇
magicUp（魔力上昇）:        魔力がvalue%上昇
physicalUp（物理攻撃力上昇）: 物理攻撃力がvalue%上昇
penetrationUp（貫通力上昇）:  貫通力がvalue%上昇
critical（クリティカル率上昇）: クリティカル率+value%
```

#### E. バフ - 回復・防御系

```
regeneration（再生）:       毎ターン開始時、value HP回復
shieldRegen（シールド再生）: 毎ターン開始時、valueシールド付与
reflect（反撃）:            被ダメージのvalue%を反撃
evasion（回避率上昇）:      回避率+value%
immunity（デバフ無効）:     デバフを無効化
```

#### F. バフ - リソース管理系

```
energyRegen（エナジー再生）:   毎ターン開始時、valueエナジー回復
drawPower（ドロー強化）:       毎ターン開始時、value枚追加ドロー
costReduction（コスト軽減）:   カードコスト-value
```

#### G. バフ - 戦闘スタイル変化系

```
thorns（棘の鎧）:           物理攻撃を受けた時、攻撃者にvalueダメージ
lifesteal（吸血）:          与ダメージのvalue%をHP回復
doubleStrike（連撃）:       攻撃カードが2回発動（威力value%）
splash（範囲拡大）:         単体攻撃が隣接敵にもvalue%ダメージ
```

#### H. バフ - キャラクター固有系

```
【剣士用】
swordEnergyGain（剣気増幅）:      攻撃時の剣気獲得量+value
swordEnergyEfficiency（剣気効率）: 剣気ダメージ+value%

【魔術士用】
resonanceExtension（共鳴延長）:   属性共鳴の持続+valueターン
elementalMastery（属性熟練）:     共鳴ボーナス+value%

【召喚士用】
summonDuration（召喚延長）:       召喚獣の持続+valueターン
summonPower（召喚強化）:          召喚獣の能力+value%
sacrificeBonus（犠牲強化）:       犠牲効果+value%
```

#### I. バフ - 特殊効果系

```
barrier（バリア）:              valueダメージまで無効化する障壁
damageReduction（ダメージ軽減）: 全ダメージ-value%
focus（集中）:                  次のカードの効果+value%
momentum（勢い）:               カード使用ごとに攻撃力+value%（累積）
cleanse（自動浄化）:            毎ターン開始時、デバフをvalue個解除
tenacity（不屈）:               デバフの効果-value%
lastStand（背水の陣）:          HP30%以下で全能力+value%
```

### 3.3 スタックシステム

```typescript
/**
 * バフ/デバフを追加または更新
 */
function addOrUpdateBuffDebuff(
  map: BuffDebuffMap,
  type: BuffDebuffType,
  stacks: number,
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

### 3.4 持続時間管理

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

## 4. ダメージ計算ロジック (Damage Formula)

### 4.1 計算フロー

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
  category: "physical" | "magic" | "defense" | "heal";
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
  card: Card,
  currentDepth: number
): DamageResult {
  // --- Phase 1: 基本攻撃力計算 ---
  const baseDmg = card.power;
  const depthMod = getDepthModifier(currentDepth);

  // --- Phase 2: バフ/デバフによる攻撃力補正 ---
  let atkMultiplier = 1.0;

  // 攻撃力上昇バフ
  atkMultiplier += calculateAttackMultiplier(attacker.buffDebuffs);

  // 攻撃力低下デバフ
  if (attacker.buffDebuffs.has("weak")) {
    const weak = attacker.buffDebuffs.get("weak")!;
    atkMultiplier *= 1 - weak.value / 100;
  }

  if (attacker.buffDebuffs.has("paralyze")) {
    atkMultiplier *= 0.5;
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

  const finalAtk = Math.floor(baseDmg * depthMod * atkMultiplier * critMod);

  // --- Phase 5: 防御側のバフ/デバフ補正 ---
  let vulnMod = 1.0;

  // 防御力低下デバフ
  if (defender.buffDebuffs.has("defDown")) {
    const defDown = defender.buffDebuffs.get("defDown")!;
    vulnMod *= 1 + defDown.value / 100;
  }

  // ダメージ軽減バフ
  let damageReductionMod = 1.0;
  if (defender.buffDebuffs.has("damageReduction")) {
    const reduction = defender.buffDebuffs.get("damageReduction")!;
    damageReductionMod *= 1 - reduction.value / 100;
  }

  if (defender.buffDebuffs.has("defUp")) {
    const defUp = defender.buffDebuffs.get("defUp")!;
    damageReductionMod *= 1 - defUp.value / 100;
  }

  // 装備DEF軽減
  const defMitigation = defender.equipment_def_percent;

  const incomingDmg = Math.floor(
    finalAtk * vulnMod * damageReductionMod * (1.0 - defMitigation)
  );

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

### 4.2 バフ/デバフ計算関数

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

  if (buffDebuffs.has("physicalUp")) {
    const buff = buffDebuffs.get("physicalUp")!;
    multiplier += buff.value / 100;
  }

  if (buffDebuffs.has("magicUp")) {
    const buff = buffDebuffs.get("magicUp")!;
    multiplier += buff.value / 100;
  }

  // Momentum（勢い）バフ
  if (buffDebuffs.has("momentum")) {
    const momentum = buffDebuffs.get("momentum")!;
    multiplier += (momentum.value / 100) * momentum.stacks;
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

### 4.3 ダメージ配分ロジック

```typescript
/**
 * ダメージを Guard → AP → HP の順に配分
 */
function applyDamageAllocation(
  defender: Character,
  damage: number
): { penetrationDamage: number; actualDamage: number } {
  let remainingDmg = damage;
  let penetrationDmg = 0;

  // Step 1: バリア処理
  if (defender.buffDebuffs.has("barrier")) {
    const barrier = defender.buffDebuffs.get("barrier")!;
    const barrierAmount = barrier.value * barrier.stacks;

    if (barrierAmount >= remainingDmg) {
      // バリアで全吸収
      barrier.value -= remainingDmg;
      return { penetrationDamage: 0, actualDamage: 0 };
    } else {
      // バリア破壊
      remainingDmg -= barrierAmount;
      defender.buffDebuffs.delete("barrier");
    }
  }

  // Step 2: アーマーブレイク時の貫通処理
  if (defender.ap <= 0) {
    penetrationDmg = Math.floor(remainingDmg * 0.5);
    defender.hp -= penetrationDmg;
    remainingDmg -= penetrationDmg;
  }

  // Step 3: Guardでの受け
  if (defender.guard > 0) {
    if (defender.guard >= remainingDmg) {
      defender.guard -= remainingDmg;
      return { penetrationDamage: penetrationDmg, actualDamage: damage };
    } else {
      remainingDmg -= defender.guard;
      defender.guard = 0;
    }
  }

  // Step 4: APでの受け
  if (defender.ap > 0) {
    if (defender.ap >= remainingDmg) {
      defender.ap -= remainingDmg;
      return { penetrationDamage: penetrationDmg, actualDamage: damage };
    } else {
      remainingDmg -= defender.ap;
      defender.ap = 0;
      // アーマーブレイク発生
    }
  }

  // Step 5: HPでの受け
  if (remainingDmg > 0) {
    defender.hp -= remainingDmg;
  }

  return { penetrationDamage: penetrationDmg, actualDamage: damage };
}
```

---

## 5. ターンフェーズ定義

### 5.1 プレイヤーターン開始時

```typescript
function onPlayerTurnStart(player: Character, enemy: Character): void {
  // 1. Guardの消滅
  player.guard = 0;

  // 2. バフ/デバフの持続時間減少
  player.buffDebuffs = decreaseBuffDebuffDuration(player.buffDebuffs);
  enemy.buffDebuffs = decreaseBuffDebuffDuration(enemy.buffDebuffs);

  // 3. 再生・シールド再生処理
  const healing = calculateStartTurnHealing(player.buffDebuffs);
  player.hp = Math.min(player.maxHp, player.hp + healing.hp);
  player.guard += healing.shield;

  // 4. エナジー再生処理
  let energyGain = BASE_ENERGY_PER_TURN;
  if (player.buffDebuffs.has("energyRegen")) {
    const energyRegen = player.buffDebuffs.get("energyRegen")!;
    energyGain += energyRegen.value * energyRegen.stacks;
  }

  // スロウデバフの影響
  if (player.buffDebuffs.has("slow")) {
    energyGain -= 1;
  }

  player.energy = Math.max(0, energyGain);

  // 5. ドロー処理
  let drawCount = BASE_DRAW_COUNT;
  if (player.buffDebuffs.has("drawPower")) {
    const drawPower = player.buffDebuffs.get("drawPower")!;
    drawCount += drawPower.value * drawPower.stacks;
  }

  // カードをdrawCount枚ドロー

  // 6. 自動浄化処理
  if (player.buffDebuffs.has("cleanse")) {
    const cleanse = player.buffDebuffs.get("cleanse")!;
    const cleansCount = cleanse.value * cleanse.stacks;
    removeDebuffs(player.buffDebuffs, cleansCount);
  }

  // 7. 行動不可チェック
  if (player.buffDebuffs.has("freeze") || player.buffDebuffs.has("stun")) {
    // このターンは行動不可
    // ターンをスキップ
  }
}
```

### 5.2 プレイヤーターン終了時

```typescript
function onPlayerTurnEnd(player: Character, enemy: Character): void {
  // 1. 持続ダメージ処理（火傷、出血、毒、呪い）
  const dotDamage = calculateEndTurnDamage(player.buffDebuffs);

  // 持続ダメージは防御を無視
  player.hp -= dotDamage;

  // 2. Momentum（勢い）のスタック増加
  if (player.buffDebuffs.has("momentum")) {
    const momentum = player.buffDebuffs.get("momentum")!;
    momentum.stacks += 1;
  }
}
```

### 5.3 敵ターン開始時

```typescript
function onEnemyTurnStart(player: Character, enemy: Character): void {
  // 1. Guardの消滅
  enemy.guard = 0;

  // 2. 再生・シールド再生処理
  const healing = calculateStartTurnHealing(enemy.buffDebuffs);
  enemy.hp = Math.min(enemy.maxHp, enemy.hp + healing.hp);
  enemy.guard += healing.shield;

  // 3. 行動不可チェック
  if (enemy.buffDebuffs.has("freeze") || enemy.buffDebuffs.has("stun")) {
    // このターンは行動不可
    // ターンをスキップ
  }
}
```

### 5.4 敵ターン終了時

```typescript
function onEnemyTurnEnd(player: Character, enemy: Character): void {
  // 1. 持続ダメージ処理
  const dotDamage = calculateEndTurnDamage(enemy.buffDebuffs);
  enemy.hp -= dotDamage;
}
```

---

## 6. バフ/デバフ計算関数

### 6.1 ターン終了時の持続ダメージ

```typescript
/**
 * ターン終了時の持続ダメージ計算
 */
function calculateEndTurnDamage(buffDebuffs: BuffDebuffMap): number {
  let totalDamage = 0;

  if (buffDebuffs.has("burn")) {
    const burn = buffDebuffs.get("burn")!;
    totalDamage += burn.stacks * 3;
  }

  if (buffDebuffs.has("bleed")) {
    const bleed = buffDebuffs.get("bleed")!;
    totalDamage += bleed.stacks * 2;
  }

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
```

### 6.2 ターン開始時の回復・再生

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

### 6.3 デバフ解除

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

## 7. ダンジョン深度スケーリング (Depth Scaling)

### 7.1 深度情報

| 深度 | 名称 | 魔力倍率 | 物理/HP 倍率 | 敵 AI・環境特性              |
| ---- | ---- | -------- | ------------ | ---------------------------- |
| 1    | 腐食 | x1.0     | x1.0         | 基本行動のみ                 |
| 2    | 狂乱 | x2.0     | x1.2         | 重度状態異常の使用開始       |
| 3    | 混沌 | x4.0     | x1.5         | 連携行動、自己バフ使用       |
| 4    | 虚無 | x8.0     | x2.0         | アーマー貫通攻撃、高火力魔法 |
| 5    | 深淵 | x16.0    | x3.0         | 学習 AI、多回行動            |

### 7.2 深度スケーリング実装

```typescript
interface DepthInfo {
  depth: number;
  name: string;
  magicMultiplier: number;
  physicalMultiplier: number;
  hpMultiplier: number;
  aiLevel: string;
}

class DepthScaling {
  private static readonly DEPTH_TABLE: Map<number, DepthInfo> = new Map([
    [
      1,
      {
        depth: 1,
        name: "腐食",
        magicMultiplier: 1.0,
        physicalMultiplier: 1.0,
        hpMultiplier: 1.0,
        aiLevel: "basic",
      },
    ],
    [
      2,
      {
        depth: 2,
        name: "狂乱",
        magicMultiplier: 2.0,
        physicalMultiplier: 1.2,
        hpMultiplier: 1.2,
        aiLevel: "heavy_status",
      },
    ],
    [
      3,
      {
        depth: 3,
        name: "混沌",
        magicMultiplier: 4.0,
        physicalMultiplier: 1.5,
        hpMultiplier: 1.5,
        aiLevel: "cooperative",
      },
    ],
    [
      4,
      {
        depth: 4,
        name: "虚無",
        magicMultiplier: 8.0,
        physicalMultiplier: 2.0,
        hpMultiplier: 2.0,
        aiLevel: "penetration",
      },
    ],
    [
      5,
      {
        depth: 5,
        name: "深淵",
        magicMultiplier: 16.0,
        physicalMultiplier: 3.0,
        hpMultiplier: 3.0,
        aiLevel: "learning",
      },
    ],
  ]);

  static getDepthInfo(depth: number): DepthInfo {
    const info = this.DEPTH_TABLE.get(depth);
    if (!info) {
      throw new Error(`Invalid depth: ${depth}`);
    }
    return info;
  }

  static scaleEnemyStats(baseEnemy: Character, depth: number): Character {
    const info = this.getDepthInfo(depth);

    return {
      ...baseEnemy,
      hp: Math.floor(baseEnemy.hp * info.hpMultiplier),
      maxHp: Math.floor(baseEnemy.hp * info.hpMultiplier),
      attack: Math.floor(baseEnemy.attack * info.physicalMultiplier),
      magicPower: Math.floor(baseEnemy.magicPower * info.magicMultiplier),
    };
  }
}
```

---

## 8. 実装上の注意点

### 8.1 用語の区別

- **Depth (深度)**: ダンジョンの階層（敵の強さ）
- **Duration (持続時間)**: バフ/デバフの残りターン数
- **Stacks (スタック)**: バフ/デバフの重ね掛け数

### 8.2 バフ/デバフの優先度

```
【ダメージ計算時の適用順序】
1. 基本攻撃力
2. 深度補正
3. 攻撃力上昇バフ (atkUp, physicalUp, magicUp)
4. 攻撃力低下デバフ (weak, paralyze, atkDown)
5. クリティカル判定
6. 防御力低下デバフ (defDown)
7. ダメージ軽減バフ (damageReduction, defUp)
8. 装備DEF軽減
9. バリア・反撃・吸血処理
```

### 8.3 Guard の特殊処理

- プレイヤーターン開始時に必ず 0 になる
- 敵ターン開始時にも 0 になる
- 戦闘終了後は引き継がれない
- アーマーブレイク時は 50%貫通される

### 8.4 持続ダメージの処理

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

### 8.5 戦闘終了時の状態保存

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

## 9. UI 表示のための予測計算

### 9.1 ダメージ予測

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
