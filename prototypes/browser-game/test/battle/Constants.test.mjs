import { fileURLToPath } from 'node:url';
// Tests for src/core/battle/Constants.js (port of unity-port/BattleCore/Constants.cs).
// Ports CombatTests.Constants_MatchTheCanonNumbers / FaceOrder_PutsAttackBeforeMoveBeforeGuard and
// ConstantsTableTests.EveryRowOfSection10_MatchesTheCode (reads the canon §10 table, skipped when absent).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { Constants } from '../../src/core/battle/Constants.js';
import { Columns } from '../../src/core/battle/Columns.js';
import { loadDeps } from './_foundation_deps.mjs';

// Constants.js and Columns.js are leaves. FaceKind and Reach come from Types.js, which imports
// Statuses.js: the rows that read them skip with a reason while that module is missing.
const { mods: T, skip: SKIP_TYPES } = await loadDeps(import.meta.url, ['../../src/core/battle/Types.js']);
const { FaceKind, Reach } = T ?? {};

test('Constants_MatchTheCanonNumbers', () => {
  assert.equal(Constants.HandDraw, 6);
  assert.equal(Constants.HandLimit, 8);
  assert.equal(Constants.BaseMaxStamina, 10);
  assert.equal(Constants.StaminaRecovery, 3);
  assert.equal(Constants.PlayerMaxHp, 50);
  assert.equal(Constants.ReserveThreshold, 3);
  assert.equal(Constants.ReserveGuard, 3);
  assert.equal(Constants.DeckMin, 20);
  assert.equal(Constants.DeckMax, 40);
  assert.equal(Constants.CopiesMax, 3);
  assert.equal(Constants.StatusApplyDefault, 2);
});

test('FaceOrder_PutsAttackBeforeMoveBeforeGuard (member names)', () => {
  assert.deepEqual([...Constants.FaceOrder], ['Attack', 'Move', 'Guard', 'Skill', 'Stance']);
});

test('FaceOrder_PutsAttackBeforeMoveBeforeGuard', { skip: SKIP_TYPES }, () => {
  assert.deepEqual([...Constants.FaceOrder], [
    FaceKind.Attack,
    FaceKind.Move,
    FaceKind.Guard,
    FaceKind.Skill,
    FaceKind.Stance,
  ]);
});

test('every remaining constant keeps its C# value', () => {
  const expected = {
    HandDrawMin: 3, HandDrawMax: 8, OwnedKindsMax: 80, ColumnCount: 4, CostMin: 1, CostMax: 3,
    MaxStaminaFloor: 3, MaxStaminaCeil: 14, DesperateThreshold: 2, HeavyBlowPower: 6,
    HeavyBlowRecoveryPenalty: 1, TraitConditions: 13, TraitEffects: 10, PositionTraitCards: 16,
    GapScalingCardsMax: 4, PlainCardBonus: 2, SameTraitMax: 3, ThinHandMax: 2, FinisherPlayNumber: 3,
    BrokenBelow: 3, FollowUpPower: 5, StatusKindsPlayer: 6, TurnDecayStackMax: 4, BleedPerStack: 2,
    RegenPerStack: 2, IntimidatePenalty: 3, FatiguePenalty: 1, BossStatusKinds: 2, EliteActions: 2,
    OmenDepthElite: 2, MasteryTopRankCards: 1, TalentTickMultiplier: 2, TrainingTicks: 1,
    ChainBattlesDefault: 3, ChainRestHpPercent: 30, FieldCellsMin: 5, FieldCellsMax: 8, CellCapacity: 1,
    EnemiesMax: 3, EnemySizeMax: 3, PlayerStartCell: 2, StartGap: 3, MoveStepMax: 2, WallDamage: 3,
    EmpowerMult: 1.5, FragileMult: 1.5,
  };
  for (const [k, v] of Object.entries(expected)) assert.equal(Constants[k], v, k);
  assert.deepEqual([...Constants.MasteryThresholds], [3, 8, 15]);
  assert.deepEqual([...Constants.MasteryThresholdsReform], [2, 6, 12]);
  assert.ok(Object.isFrozen(Constants));
});

// ---- ConstantsTableTests: battle_core_v4.md §10 against the code ----

const CANON = fileURLToPath(new URL('../../../../.claude/docs/battle_document/battle_core_v4.md', import.meta.url));

const join = (...values) => values.join(' / ');
/** C# value.ToString("0.##", InvariantCulture). */
const number = (value) => String(Number(value.toFixed(2)));

const expectedRows = () => ({
  'HAND_DRAW': join(Constants.HandDraw),
  'HAND_DRAW_MIN / MAX': join(Constants.HandDrawMin, Constants.HandDrawMax),
  'HAND_LIMIT': join(Constants.HandLimit),
  'DECK_MIN / MAX': join(Constants.DeckMin, Constants.DeckMax),
  'COPIES_MAX': join(Constants.CopiesMax),
  'OWNED_KINDS_MAX': join(Constants.OwnedKindsMax),
  'COLUMN_COUNT': join(Constants.ColumnCount),
  'COST_MIN / MAX': join(Constants.CostMin, Constants.CostMax),
  'STAMINA_RECOVERY': join(Constants.StaminaRecovery),
  'BASE_MAX_STAMINA': join(Constants.BaseMaxStamina),
  'PLAYER_MAX_HP': join(Constants.PlayerMaxHp),
  'RESERVE_THRESHOLD': join(Constants.ReserveThreshold),
  'RESERVE_GUARD': join(Constants.ReserveGuard),
  'DESPERATE_THRESHOLD': join(Constants.DesperateThreshold),
  'TRAIT_CONDITIONS / EFFECTS': join(Constants.TraitConditions, Constants.TraitEffects),
  'POSITION_TRAIT_CARDS': join(Constants.PositionTraitCards),
  'FIELD_CELLS': '戦闘ごと',
  'CELL_CAPACITY': join(Constants.CellCapacity),
  'ENEMY_SIZE_MAX': join(Constants.EnemySizeMax),
  'START_GAP': join(Constants.StartGap),
  'REACH_DEFAULT': Reach.Default.ToText(),
  'MOVE_STEP_MAX': join(Constants.MoveStepMax),
  'WALL_DAMAGE': join(Constants.WallDamage),
  'GAP_SCALING_CARDS_MAX': join(Constants.GapScalingCardsMax),
  'PLAIN_CARD_BONUS': '+' + Constants.PlainCardBonus,
  'SAME_TRAIT_MAX': join(Constants.SameTraitMax),
  'DUAL_FACE_RATIO': number(Columns.DualFaceRatio),
  'STATUS_KINDS_PLAYER': join(Constants.StatusKindsPlayer),
  'STATUS_KINDS_ENEMY': '上限なし',
  'TURN_DECAY_STACK_MAX': join(Constants.TurnDecayStackMax),
  'BOSS_STATUS_KINDS': join(Constants.BossStatusKinds),
  'ENEMIES_MAX': join(Constants.EnemiesMax),
  'ELITE_ACTIONS': join(Constants.EliteActions),
  'OMEN_DEPTH_ELITE': join(Constants.OmenDepthElite),
  'EMPOWER_MULT': number(Constants.EmpowerMult),
  'FRAGILE_MULT': number(Constants.FragileMult),
  'MASTERY_THRESHOLDS': join(...Constants.MasteryThresholds),
  'MASTERY_THRESHOLDS_REFORM': join(...Constants.MasteryThresholdsReform),
  'MASTERY_TOP_RANK_CARDS': join(Constants.MasteryTopRankCards),
  'TALENT_TICK_MULT': join(Constants.TalentTickMultiplier),
  'TRAINING_TICKS': join(Constants.TrainingTicks),
  'CHAIN_BATTLES_DEFAULT': join(Constants.ChainBattlesDefault),
});

function readTable(path) {
  const rows = new Map();
  let inside = false;
  for (const line of readFileSync(path, 'utf8').split(/\r?\n/)) {
    if (line.startsWith('## 10.')) { inside = true; continue; }
    if (!inside) continue;
    if (line.startsWith('## ')) break;
    if (!line.startsWith('|')) continue;
    const cells = line.split('|').map((c) => c.trim());
    if (cells.length < 4) continue;
    const name = cells[1];
    if (name === '定数' || name.startsWith('-')) continue;
    rows.set(name, cells[2]);
  }
  return rows;
}

test('EveryRowOfSection10_MatchesTheCode', { skip: existsSync(CANON) ? SKIP_TYPES : 'battle_core_v4.md is not beside this copy' }, () => {
  const EXPECTED = expectedRows();
  const table = readTable(CANON);
  assert.ok(table.size > 0, "§10's table was found");
  for (const [name, value] of table) {
    assert.ok(name in EXPECTED, `§10 has ${name}, the code has no constant for it`);
    assert.equal(value, EXPECTED[name], name);
  }
  for (const name of Object.keys(EXPECTED)) {
    assert.ok(table.has(name), `the code checks ${name}, §10 no longer has it`);
  }
});
