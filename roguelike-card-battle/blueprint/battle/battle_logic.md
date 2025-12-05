# BATTLE SYSTEM LOGIC SPECIFICATION (Ver 2.0)

## 1. 概要

本ドキュメントは「ローグライトカード RPG」のコアバトルシステムの論理仕様である。
アーマー（耐久値）とガード（一時防御）の分離、状態異常の重症度進化（軽度/重度）、およびダンジョン深度による環境変化のロジックを定義する。

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

1.  **Guard**（盾で防ぐ）
2.  **AP**（鎧で受ける）
3.  **HP**（肉体で受ける）

### 2.3 アーマーブレイク (Armor Break)

- **条件:** `AP` が `0` になった状態。
- **効果 (貫通ペナルティ):**
  - AP が 0 の状態では、**敵攻撃ダメージの 50%が Guard を無視して直接 HP にヒットする**。
  - 残りの 25%は通常通り Guard で受ける。
  - _意図:_ 鎧が壊れた生身の状態では、盾の上から衝撃が通るリアリティの表現。

---

## 3. ダメージ計算ロジック (Damage Formula)

### 3.1 計算フロー

攻撃発生時、以下のアルゴリズムで最終ダメージを決定する。

```typescript
// 型定義
interface Character {
  hp: number;
  ap: number;
  guard: number;
  atk_buff_percent: number;
  crit_dmg_bonus: number;
  equipment_def_percent: number;
  hasStatus(status: string): boolean;
}

interface Card {
  power: number;
  // その他のカード情報
}

interface DamageResult {
  finalDamage: number;
  isCritical: boolean;
  penetrationDamage: number;
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
  // --- Phase 1: 最終攻撃力 (Final ATK) 算出 ---
  // 深度適正、ステータス、バフ/デバフの適用
  const baseDmg = card.power;
  const depthMod = getDepthModifier(currentDepth); // ダンジョン深度によるカード適正

  // 攻撃バフ・デバフ計算
  // 軽度:脱力(x0.75) / 重度:無力(x0.5)
  let atkMultiplier = 1.0 + attacker.atk_buff_percent;
  if (attacker.hasStatus("脱力")) atkMultiplier *= 0.75;
  if (attacker.hasStatus("無力")) atkMultiplier *= 0.5;

  // 麻痺(威力半減)の適用
  if (attacker.hasStatus("麻痺")) atkMultiplier *= 0.5;

  // クリティカル判定 ("無力"状態なら発生しない)
  let critMod = 1.0;
  const isCritical = checkCritical(attacker) && !attacker.hasStatus("無力");
  if (isCritical) {
    critMod = 1.5 + attacker.crit_dmg_bonus;
  }

  const finalAtk = Math.floor(baseDmg * depthMod * atkMultiplier * critMod);

  // --- Phase 2: 被ダメージ (Incoming Dmg) 算出 ---
  // 防御側のデバフ補正
  // 軽度:弱体(x1.25) / 重度:脆弱(x1.5)
  let vulnMod = 1.0;
  if (defender.hasStatus("弱体")) vulnMod = 1.25;
  if (defender.hasStatus("脆弱")) vulnMod = 1.5;
  if (defender.hasStatus("気絶")) vulnMod *= 1.5; // 確定クリティカル扱いとして計算

  // 装備DEF軽減 (例: 0.2 = 20%軽減)
  const defMitigation = defender.equipment_def_percent;

  const incomingDmg = Math.floor(finalAtk * vulnMod * (1.0 - defMitigation));

  // --- Phase 3: ダメージ配分 (Allocation) ---
  const penetrationDamage = applyDamageAllocation(defender, incomingDmg);

  return {
    finalDamage: incomingDmg,
    isCritical: isCritical,
    penetrationDamage: penetrationDamage,
  };
}

/**
 * ダンジョン深度による修正値取得
 */
function getDepthModifier(depth: number): number {
  // カードごとの深度適正カーブを参照
  // 例: 浅層型カードなら深度1で1.3、深度5で0.5など
  // 実装はカードデータに依存
  return 1.0; // プレースホルダー
}

/**
 * クリティカル判定
 */
function checkCritical(attacker: Character): boolean {
  // クリティカル率の判定ロジック
  // 実装は確率計算に依存
  return Math.random() < 0.1; // プレースホルダー（10%）
}
```

### 3.2 ダメージ配分ロジック (Allocation)

```typescript
/**
 * ダメージを Guard → AP → HP の順に配分
 * @returns 貫通ダメージ量
 */
function applyDamageAllocation(defender: Character, damage: number): number {
  let remainingDmg = damage;
  let penetrationDmg = 0;

  // Step 0: 状態異常「溶解」チェック
  // 溶解状態ならガード値の効率が半減した状態で計算開始
  let effectiveGuard = defender.guard;
  if (defender.hasStatus("溶解")) {
    effectiveGuard = Math.floor(effectiveGuard * 0.5);
  }

  // Step 1: アーマーブレイク時の貫通処理 (Penetration)
  if (defender.ap <= 0) {
    // 50%は強制的にHPへ (端数切り捨て)
    penetrationDmg = Math.floor(remainingDmg * 0.5);
    defender.hp -= penetrationDmg;
    remainingDmg -= penetrationDmg;
    // 残りのダメージは通常通りGuardで受ける
  }

  // Step 2: Guardでの受け
  if (effectiveGuard > 0) {
    if (effectiveGuard >= remainingDmg) {
      defender.guard -= remainingDmg; // 実ガード値を減算
      return penetrationDmg; // ダメージ吸収完了
    } else {
      remainingDmg -= effectiveGuard;
      defender.guard = 0;
    }
  }

  // Step 3: APでの受け
  if (defender.ap > 0) {
    if (defender.ap >= remainingDmg) {
      defender.ap -= remainingDmg;
      return penetrationDmg; // ダメージ吸収完了
    } else {
      remainingDmg -= defender.ap;
      defender.ap = 0;
      // ※ここでアーマーブレイク発生 (次回被弾から貫通適用)
    }
  }

  // Step 4: HPでの受け
  if (remainingDmg > 0) {
    defender.hp -= remainingDmg;
  }

  return penetrationDmg;
}
```

---

## 4. 状態異常システム (Status Effects)

### 4.1 重症度進化 (Severity Progression)

- **軽度 (Light):** 初回付与時の状態。
- **重度 (Heavy):** 軽度の状態で同じ状態異常が付与されると進化する。
- **仕様:** 重度の状態でさらに重ねがけしても、持続ターンのみ延長される。

### 4.2 状態異常マトリクス

| ID  | 系統 | 名称 (Light)          | 名称 (Heavy)           | 対象 | 軽度 (Light) 効果         | 重度 (Heavy) 効果                    | 減衰ルール              |
| --- | ---- | --------------------- | ---------------------- | ---- | ------------------------- | ------------------------------------ | ----------------------- |
| Psn | 継続 | 火傷<br>(burn)        | 大火傷<br>(inflamed)   | HP   | 開始時 Stack dmg          | 開始時 + 終了時 Stack dmg            | Stack -1<br>(End Turn)  |
| Bld | 反動 | 出血<br>(Bleed)       | 大出血<br>(Hemorrhage) | HP   | 攻撃時 Stack dmg          | 全行動時 Stack\*1.5 dmg              | Duration -1<br>(3 Turn) |
| Oxi | 環境 | 酸化<br>(Oxidize)     | 溶解<br>(Melt)         | AP   | 終了時 AP10%削り          | 終了時 AP15%削り +<br>Guard 効果半減 | Duration -1<br>(3 Turn) |
| Stg | 行動 | よろめき<br>(Stagger) | 気絶<br>(Stun)         | Act  | Energy -1<br>(行動は可能) | 行動不能 (Skip Turn)                 | Duration -1<br>(1 Turn) |
| Frz | 行動 | 凍結<br>(Freeze)      | 氷獄<br>(Glacial)      | Hand | 手札 1 枚禁止             | 手札全て禁止                         | Duration -1<br>(1 Turn) |
| Wek | 弱体 | 弱体<br>(Weaken)      | 脆弱<br>(Vulnerable)   | Def  | 被ダメージ x1.25          | 被ダメージ x1.50 +<br>Guard 不可     | Duration -1<br>(2 Turn) |
| Fbl | 脱力 | 脱力<br>(Exhaust)     | 無力<br>(Powerless)    | Atk  | 与ダメージ x0.75          | 与ダメージ x0.50 +<br>Critical 不可  | Duration -1<br>(2 Turn) |

※ ID はプログラム内部での識別子として使用する。

### 4.3 状態異常データ型定義

```typescript
/**
 * 状態異常の重症度
 */
enum StatusSeverity {
  LIGHT = "Light",
  HEAVY = "Heavy",
}

/**
 * 状態異常インターフェース
 */
interface StatusEffect {
  id: string; // Psn, Bld, Oxi, Stg, Frz, Wek, Fbl
  severity: StatusSeverity;
  duration: number; // 残りターン数
  stack: number; // スタック数（毒・出血など）
}

/**
 * 状態異常適用クラス例
 */
class StatusManager {
  private effects: Map<string, StatusEffect> = new Map();

  /**
   * 状態異常を付与
   */
  applyStatus(id: string, duration: number, stack: number = 1): void {
    const existing = this.effects.get(id);

    if (existing) {
      // 既存の状態異常がある場合
      if (existing.severity === StatusSeverity.LIGHT) {
        // 軽度 → 重度に進化
        existing.severity = StatusSeverity.HEAVY;
        existing.duration = Math.max(existing.duration, duration);
        existing.stack += stack;
      } else {
        // 重度の場合は持続ターンとスタックのみ延長
        existing.duration = Math.max(existing.duration, duration);
        existing.stack += stack;
      }
    } else {
      // 新規付与（軽度から開始）
      this.effects.set(id, {
        id,
        severity: StatusSeverity.LIGHT,
        duration,
        stack,
      });
    }
  }

  /**
   * 状態異常を持っているか確認
   */
  hasStatus(id: string, severity?: StatusSeverity): boolean {
    const effect = this.effects.get(id);
    if (!effect) return false;
    if (severity) return effect.severity === severity;
    return true;
  }

  /**
   * ターン終了時の減衰処理
   */
  decrementDurations(): void {
    for (const [id, effect] of this.effects.entries()) {
      effect.duration--;
      if (effect.duration <= 0) {
        this.effects.delete(id);
      }
    }
  }

  /**
   * スタック減少処理（毒など）
   */
  decrementStack(id: string, amount: number = 1): void {
    const effect = this.effects.get(id);
    if (effect && effect.stack > 0) {
      effect.stack -= amount;
      if (effect.stack <= 0) {
        this.effects.delete(id);
      }
    }
  }
}
```

---

## 5. ターン進行とタイミング (Timing)

厳密な処理順序を以下に定める。

### **Turn Start Phase**

1 **[System]** 前ターンの Guard 消滅（残っていた場合、0 になる）

- エナジー回復 / カードドロー
- **[Effect]** 火傷/大火傷 ダメージ発生
- **[Effect]** リジェネ (HP/AP 回復)

### **Player Action Phase**

- カード使用
- **[Trigger]** 出血/大出血 ダメージ発生 (HP 直接)

### **Turn End Phase**

- 手札を捨てる
- **[System]** Guard は消滅しない（次ターン開始時まで持続）
- **[System]**敵のバフ/デバフ カウント減少
- **[Effect]** 大火傷 ダメージ発生
- **[Effect]** 酸化/溶解 ダメージ発生 (AP 減少)

### **Enemy Action Phase**

- 敵の行動

### 5.1 ターン管理クラス例

```typescript
/**
 * ターン進行管理クラス
 */
class TurnManager {
  /**
   * ターン開始フェーズ
   */
  executeTurnStartPhase(player: Character): void {
    // 前ターンのGuard消滅
    player.guard = 0;

    // エナジー回復
    player.energy = player.maxEnergy;

    // カードドロー
    this.drawCards(player, 5);

    // 毒ダメージ処理
    this.applyPoisonDamage(player);

    // リジェネ処理
    this.applyRegeneration(player);
  }

  /**
   * プレイヤーアクションフェーズ
   */
  executePlayerActionPhase(player: Character, action: Action): void {
    // カード使用
    this.playCard(player, action.card);

    // 出血ダメージトリガー
    this.applyBleedDamage(player);
  }

  /**
   * ターン終了フェーズ
   */
  executeTurnEndPhase(player: Character): void {
    // 手札を捨てる
    this.discardHand(player);

    // Guardは消滅しない（次ターン開始時まで持続）
    // player.guard = 0; ← これはターン開始時に実行

    // 猛毒の終了時ダメージ
    this.applyDeadlyPoisonEndDamage(player);

    // 酸化/溶解ダメージ
    this.applyOxidizeDamage(player);
  }

  /**
   * 敵アクションフェーズ
   */
  executeEnemyActionPhase(enemies: Character[]): void {
    for (const enemy of enemies) {
      this.executeEnemyAction(enemy);
    }

    // バフ/デバフの減衰
    this.decrementStatusEffects(enemies);
  }

  // 各種ヘルパーメソッド実装...
  private applyPoisonDamage(character: Character): void {
    /* ... */
  }
  private applyBleedDamage(character: Character): void {
    /* ... */
  }
  private applyOxidizeDamage(character: Character): void {
    /* ... */
  }
  private drawCards(player: Character, count: number): void {
    /* ... */
  }
  private playCard(player: Character, card: Card): void {
    /* ... */
  }
  private discardHand(player: Character): void {
    /* ... */
  }
  private executeEnemyAction(enemy: Character): void {
    /* ... */
  }
  private decrementStatusEffects(characters: Character[]): void {
    /* ... */
  }
  private applyRegeneration(character: Character): void {
    /* ... */
  }
  private applyDeadlyPoisonEndDamage(character: Character): void {
    /* ... */
  }
}

interface Action {
  card: Card;
  target?: Character;
}
```

---

## 6. ダンジョン深度スケーリング (Depth Scaling)

ダンジョンの階層（深度）による敵パラメータの変化定義。

| 深度 | 名称 | 魔力倍率 | 物理/HP 倍率 | 敵 AI・環境特性                      |
| ---- | ---- | -------- | ------------ | ------------------------------------ |
| 1    | 上層 | x1.0     | x1.0         | 基本行動のみ                         |
| 2    | 中層 | x2.0     | x1.2         | 重度(Heavy) 状態異常の使用開始       |
| 3    | 下層 | x4.0     | x1.5         | 連携行動 (Role 分担)<br>自己バフ使用 |
| 4    | 深層 | x8.0     | x2.0         | アーマー貫通攻撃を使用<br>高火力魔法 |
| 5    | 深淵 | x16.0    | x3.0         | 学習 AI、多回行動                    |

### 6.1 深度スケーリングクラス例

```typescript
/**
 * ダンジョン深度情報
 */
interface DepthInfo {
  depth: number;
  name: string;
  magicMultiplier: number;
  physicalMultiplier: number;
  hpMultiplier: number;
  aiLevel: string;
}

/**
 * 深度スケーリング管理クラス
 */
class DepthScaling {
  private static readonly DEPTH_TABLE: Map<number, DepthInfo> = new Map([
    [
      1,
      {
        depth: 1,
        name: "上層",
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
        name: "中層",
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
        name: "下層",
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
        name: "深層",
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

  /**
   * 深度情報を取得
   */
  static getDepthInfo(depth: number): DepthInfo {
    const info = this.DEPTH_TABLE.get(depth);
    if (!info) {
      throw new Error(`Invalid depth: ${depth}`);
    }
    return info;
  }

  /**
   * 深度に応じて敵のステータスをスケーリング
   */
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

## 7. 実装上の注意点

### 7.1 用語の区別

- **Depth (深度)** はダンジョンの階層（敵の強さ）を指す。
- **Severity (重症度)** は状態異常のレベル（Light/Heavy）を指す。
- これらを混同しないように変数名を設計すること。

### 7.2 出血の HP ダメージ

- 出血ダメージは AP/Guard に吸われず、直接 HP を減らすこと。

```typescript
/**
 * 出血ダメージ処理（AP/Guard無視）
 */
function applyBleedDamage(character: Character, bleedStack: number): void {
  // 出血は直接HP減算
  character.hp -= bleedStack;

  // 大出血の場合は1.5倍
  if (character.hasStatus("大出血")) {
    character.hp -= Math.floor(bleedStack * 0.5);
  }
}
```

### 7.3 アーマー持ち越し

- 戦闘終了時の `current_ap` は必ず保存し、次回の戦闘開始時に適用すること。

```typescript
/**
 * 戦闘終了時の状態保存
 */
interface BattleEndState {
  currentAp: number;
  maxAp: number;
  currentHp: number;
  // その他の永続データ
}

function saveBattleState(player: Character): BattleEndState {
  return {
    currentAp: player.ap, // 重要: 現在のAP値を保存
    maxAp: player.maxAp,
    currentHp: player.hp,
  };
}

function loadBattleState(player: Character, savedState: BattleEndState): void {
  player.ap = savedState.currentAp; // 前回のAP値を復元
  player.maxAp = savedState.maxAp;
  player.hp = savedState.currentHp;
  player.guard = 0; // Guardは必ず0から開始
}
```

### 7.4 貫通の UI 表示

- アーマーブレイク時（AP=0）や、敵の貫通攻撃時は、ダメージ予測 UI において「HP への直接ダメージ」を明確に色分け（例: 赤色点滅）して表示すること。

```typescript
/**
 * ダメージ予測UI用データ
 */
interface DamagePreview {
  totalDamage: number;
  guardDamage: number;
  apDamage: number;
  hpDamage: number;
  penetrationDamage: number; // 貫通ダメージ（赤色表示用）
  isArmorBreak: boolean; // アーマーブレイク状態フラグ
}

/**
 * ダメージ予測計算
 */
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
    penetrationDmg = Math.floor(remainingDmg * 0.25);
    hpDmg += penetrationDmg;
    remainingDmg -= penetrationDmg;
  }

  // Guard
  if (defender.guard > 0) {
    guardDmg = Math.min(defender.guard, remainingDmg);
    remainingDmg -= guardDmg;
  }

  // AP
  if (remainingDmg > 0 && defender.ap > 0) {
    apDmg = Math.min(defender.ap, remainingDmg);
    remainingDmg -= apDmg;
  }

  // HP
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
  };
}
```

---

## 8. まとめ

本仕様書は、以下の点を重視して設計されています：

1. **明確な防御レイヤー構造**: Guard（使い捨て）と AP（持ち越し）の分離により、戦略的な深みを提供
2. **状態異常の段階的進化**: Light/Heavy の 2 段階により、直感的で分かりやすい状態管理
3. **深度による難易度スケーリング**: ダンジョンが深くなるほど敵が強化される明確な仕組み
4. **厳密なタイミング定義**: ターンフェーズを明確化し、バグを防止

実装時は、TypeScript の型システムを活用し、バグの少ない堅牢なシステムを構築してください。

---

**Document Version:** 2.0  
**Last Updated:** 2025-12-05  
**TypeScript Compatible:** Yes
