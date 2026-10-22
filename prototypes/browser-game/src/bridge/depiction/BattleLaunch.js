// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/BattleLaunch.cs
// What the battle scene is started with, and the source it turns into. Pure: the MonoBehaviour
// that owns the Inspector fields (BattleBootstrap) only copies them here, so everything that can go
// wrong with them — an enemy id nobody defined, a negative turn limit — is caught under test.
import { ArgumentException, ArgumentOutOfRangeException, KeyNotFoundException } from '../../shared/cs.js';
import { BattleSetup } from '../../core/battle/TurnLoop.js';
import { Constants } from '../../core/battle/Constants.js';
import { Enemies } from '../../core/battle/Enemies.js';
import { PrototypeDeck } from '../../core/battle/CardCatalog.js';
import { Cards } from '../../core/battle/Cards.js';
import { OmenDisclosure } from '../depictionScript/OmenDisclosure.js';
import { CoreBattleSource } from './CoreBattleSource.js';

/** new ArgumentOutOfRangeException(paramName, actualValue, message) with .NET's message. */
function argRange(name, value, message) {
  const e = new ArgumentOutOfRangeException(`${message} (Parameter '${name}')\nActual value was ${value}.`);
  e.ParamName = name;
  e.ActualValue = value;
  return e;
}

export class BattleLaunch {
  /** The slice's seed: the battle pinned in BattleCore.Tests (TurnLoopTests) uses the same one. */
  static DefaultSeed = 20260921;

  /** The slice's line until the dungeon hands one in (#168). The bootstrap's Inspector reads it here. (C# const = BattleSetup.SliceFieldCells) */
  static get SliceFieldCells() { return BattleSetup.SliceFieldCells; }

  /** battle_core_v4 §7.3 `START_GAP` (3 since 2026-09-23, #169). (C# const = Constants.StartGap) */
  static get DefaultStartGap() { return Constants.StartGap; }

  /** battle_ui_ux_v2 §7: 開示度 2, everything the omen has. (C# const = OmenDisclosure.Full) */
  static get DefaultDisclosure() { return OmenDisclosure.Full; }

  /** battle_ui_ux_v2 §7: −1, the enemies of the 100% run, hidden whole. */
  static LowestDisclosure = -1;

  /**
   * JS extra: `init` plays the part of a C# object initializer (`new BattleLaunch { EnemyId = ... }`):
   * its own properties are assigned after the field defaults. `new BattleLaunch()` is the C# default.
   */
  constructor(init) {
    /** The roster id of the enemy to fight. The slice knows one: polearm_warped. */
    this.EnemyId = Enemies.PolearmWarpedId;

    /** Fixes every shuffle. The same seed deals the same hands here and under test. */
    this.Seed = BattleLaunch.DefaultSeed;

    /**
     * The cells on the line (battle_core_v4 §7.1, 5〜8). Handed in per battle since 2026-09-23
     * (#169); the dungeon side picks it per layer (#168), the slice fights on 6 until then.
     */
    this.FieldCells = BattleLaunch.SliceFieldCells;

    /**
     * The gap on turn 1 (battle_core_v4 §7.3 `START_GAP`, 3): the player stands on cell 2 and the
     * enemy this many empty cells further right — or at the right end of a line too short for it.
     */
    this.StartGap = BattleLaunch.DefaultStartGap;

    /**
     * The journal's 開示度 for the enemy (battle_ui_ux_v2 §7, #500), handed to the omen once as the
     * battle starts: it does not move during a battle. Below 2 the omen shows no numbers; −1 (the
     * 100% run's enemies) has no parts yet and reads as 0. 2 until the journal's data (#107) reaches it.
     */
    this.Disclosure = BattleLaunch.DefaultDisclosure;

    /** Unattended runs only: the source plays the leftmost payable card by itself. */
    this.AutoPlay = false;

    /** Unattended runs only: stop once this many turns have closed. 0 = fight to the end. */
    this.StopAfterTurns = 0;

    /**
     * The deck to fight with: the one the deck screen built (#190), or null for the slice's
     * ten-kind prototype deck × 2 (#71). A deck that breaks §8 is refused when the battle is built.
     * @type {Array|null} CardInstance list
     */
    this.Deck = null;

    /**
     * JS extension (docs/EXTENSIONS.md 「出立の枠（DeckLimits）」): the life's deck limits the deck is held
     * to (a survivor's deck may hold 45, concept-v3.md §14 生存者ボーナス), set by EncounterBattle.NextLaunch.
     * Null: the C#'s §8 bounds (20〜40).
     * @type {import('../../core/battle/DeckRules.js').DeckLimits|null}
     */
    this.Limits = null;

    // ---- What the exploration hands in (#99). The defaults are a fresh battle, as before. ----

    /**
     * The enemies standing behind EnemyId on the same line, in phase order (roster §9's pair:
     * 錆槍 in front, 灰弩 behind). Null or empty for one enemy. Up to two (battle_core_v4 §7.4
     * ENEMIES_MAX 3); TurnLoop.Start refuses more, or a line too short to hold them all.
     * JS extension: the source's script shows every one of them (docs/port-notes/depiction-multi-enemy.md);
     * the C# writer throws KeyNotFoundException once the second enemy's omen is shown.
     * @type {string[]|null}
     */
    this.MoreEnemyIds = null;

    /** The player's HP maximum (PLAYER_MAX_HP until the exploration carries another). */
    this.PlayerMaxHp = Constants.PlayerMaxHp;

    /**
     * The max stamina the battle starts from: the exploration's temporary changes and 瘴気 already
     * counted (battle_core_v4 §1, concept-v3 §6). The battle's own 瘴気纏い goes on top inside it.
     */
    this.MaxStamina = Constants.BaseMaxStamina;

    /** The HP the player walks in with; null is full. (int?) */
    this.StartHp = null;

    /** The current stamina the player walks in with; null is full. (int?) */
    this.StartStamina = null;

    /** The player's cell on turn 1 (§7.3: 2; 間合いの履 moves it to 1, widening StartGap by the same). */
    this.PlayerStartCell = Constants.PlayerStartCell;

    /** §9.1 (#452): the in-battle tools, as amounts — 息継ぎの笛, 竜鱗の欠片 and 竜語の符. 0 is no tool. */
    this.StartStaminaBonus = 0;
    this.FirstHitGuard = 0;
    this.BossWordWard = 0;

    /** Which battle of a back-to-back encounter this is, for the header (1 of 1 for a lone battle). */
    this.ChainIndex = 1;
    this.ChainTotal = 1;

    if (init) Object.assign(this, init);
  }

  /** @returns {Array} CardInstance list */
  BuildDeck() {
    if (this.Deck == null) return PrototypeDeck.Build();
    const validation = Cards.Validate(this.Deck, this.Limits ?? null);
    if (!validation.Ok) {
      throw new ArgumentException('BattleLaunch: the deck breaks §8 — ' + validation.Errors.join(' / '));
    }
    return [...this.Deck];
  }

  /** @returns {BattleSetup} */
  BuildSetup() {
    const enemy = BattleLaunch.EnemyById(this.EnemyId, 'BattleLaunch');
    let more = null;
    if (this.MoreEnemyIds != null && this.MoreEnemyIds.length > 0) {
      more = [];
      for (const id of this.MoreEnemyIds) more.push(BattleLaunch.EnemyById(id, 'BattleLaunch'));
    }

    if (this.StartGap < 0) throw argRange('StartGap', this.StartGap, '0 or more.');
    if (this.FieldCells < Constants.FieldCellsMin || this.FieldCells > Constants.FieldCellsMax) {
      throw argRange('FieldCells', this.FieldCells, Constants.FieldCellsMin + '..' + Constants.FieldCellsMax + ' cells.');
    }
    if (this.PlayerMaxHp < 1) throw argRange('PlayerMaxHp', this.PlayerMaxHp, '1 or more.');
    if (this.MaxStamina < Constants.MaxStaminaFloor || this.MaxStamina > Constants.MaxStaminaCeil) {
      throw argRange('MaxStamina', this.MaxStamina, Constants.MaxStaminaFloor + '..' + Constants.MaxStaminaCeil + ' (battle_core_v4 §1).');
    }
    if (this.PlayerStartCell < 1) throw argRange('PlayerStartCell', this.PlayerStartCell, 'cell 1 or further right.');
    return new BattleSetup(enemy, this.BuildDeck(), this.FieldCells, this.StartGap, this.PlayerStartCell, this.PlayerMaxHp, this.MaxStamina, more,
      this.StartHp ?? null, this.StartStamina ?? null, this.StartStaminaBonus, this.FirstHitGuard, this.BossWordWard);
  }

  /**
   * Enemies.ById, with an unknown id as an ArgumentException that lists the known ids, the type the
   * callers (BattleBootstrap, DemoFlow) catch. `caller` heads the message. (C# internal static)
   */
  static EnemyById(id, caller) {
    try {
      return Enemies.ById(id ?? '');
    } catch (err) {
      if (!(err instanceof KeyNotFoundException || (err && err.name === 'KeyNotFoundException'))) throw err;
      const known = [];
      for (const def of Enemies.All) known.push(def.Id);
      // C# string concatenation writes a null id as "".
      throw new ArgumentException(
        caller + ': no enemy has the id "' + (id ?? '') + '". Known ids: ' + known.join(', ') + '.');
    }
  }

  /** @returns {CoreBattleSource} */
  CreateSource() {
    if (this.Disclosure < BattleLaunch.LowestDisclosure || this.Disclosure > OmenDisclosure.Full) {
      throw argRange('Disclosure', this.Disclosure, BattleLaunch.LowestDisclosure + '..' + OmenDisclosure.Full + ' (battle_ui_ux_v2 §7).');
    }
    return new CoreBattleSource(this.BuildSetup(), this.Seed, this.AutoPlay, this.StopAfterTurns, this.ChainIndex, this.ChainTotal,
      OmenDisclosure.Clamp(this.Disclosure));
  }
}
