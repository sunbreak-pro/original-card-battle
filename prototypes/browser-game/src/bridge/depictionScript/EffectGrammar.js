// Port of unity-port/unity-project-kit/Assets/View/Depiction/Script/EffectGrammar.cs
// The effect grammar of battle_ui_ux_v2 §5.3 (#57): eight systems, one look each. Pure C#, like the
// rest of Script/: no UnityEngine and no BattleCore. The Bridge reads a card's or an action's faces,
// asks this file for its systems and writes them on the script; the View draws the look of the
// system it is handed and never works one out.
//
// The strength stays one constant for now (#30, 2026-10-03): every writer puts EffectStrength.Uniform
// on the cues, so a look differs by system and never by the number a face settled at.
import { ArgumentNullException, ArgumentOutOfRangeException, defineEnum } from '../../shared/cs.js';
// Read inside functions only (circular with DepictionTypes, whose Cue carries an FxSystem).
import { CueKind, StrikeSystem } from './DepictionTypes.js';

/** new ArgumentNullException(nameof(x)) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

/** new ArgumentOutOfRangeException(paramName, actualValue, message) with .NET's message. */
function argRange(name, value, message) {
  const e = new ArgumentOutOfRangeException(`${message} (Parameter '${name}')\nActual value was ${value}.`);
  e.ParamName = name;
  e.ActualValue = value;
  return e;
}

/**
 * battle_ui_ux_v2 §5.3 系統: the eight families every card face and enemy action plays as. A
 * movement has no system of its own (§5.3: the figure's step and the gap's number show it).
 *
 * - Slash   斬: an arc of light across the chest. An attack whose name holds no other word.
 * - Thrust  突: one straight flash and a point of impact.
 * - Sweep   払: a wide band that sweeps across.
 * - Strike  打: a ripple where it lands. Pushes and pulls play as this.
 * - Shield  盾: a ring stands up in front of the figure. A guard face, a Guard +n.
 * - Form    型: a crest at the feet and a faint light on the body, fading 600 ms after it opens. A stance face.
 * - Spirit  気: a mark falls from above onto the chest. A status put on.
 * - Breath  息: the pips lighting. A heal, a draw, a stamina point. Its form is the beat itself (the pips
 *   of a stamina gain, the cards of a draw); only a heal adds the chest rising once (§5.3, 400 ms).
 */
export const FxSystem = defineEnum(['Slash', 'Thrust', 'Sweep', 'Strike', 'Shield', 'Form', 'Spirit', 'Breath']);

/** The one shape each system draws (§5.3 形). One system, one form: the same system looks the same. */
export const FxForm = defineEnum(['Arc', 'Line', 'Band', 'Ripple', 'Ring', 'Crest', 'Sigil', 'Pips']);

/**
 * What the View starts beside a beat for the grammar (§5.3), read off the script alone.
 * - None: nothing beside the beat: no system, a blow (drawn inside the attack), or a 息 that is its own beat.
 * - Shape: the system's shape (EffectGrammar.FormOf): 盾 ring, 型 crest, 気 mark.
 * - ChestHeave: 息 on a heal: the chest rises and settles once beside the HP beat.
 */
export const FxMark = defineEnum(['None', 'Shape', 'ChestHeave']);

/**
 * The faces of one card or one enemy action, as far as the grammar reads them. The Bridge fills it
 * from the core's definition; nothing here knows the core's types.
 *
 * JS extra: `init` plays the part of a C# object initializer (`new FxFaces { Name = ..., Attacks = true }`):
 * its own properties are assigned after the field defaults. `new FxFaces()` is the C# default.
 */
export class FxFaces {
  constructor(init) {
    /** The technique's name: the attack's system is read off its words. */
    this.Name = '';
    /** It has an attack face. */
    this.Attacks = false;
    /** The attack pushes or pulls the opponent (§5.3: 打 before the name's words). */
    this.Pushes = false;
    /** It has a guard face (or a Guard on its face). */
    this.Guards = false;
    /** Its skill face puts a status on someone (気). */
    this.PutsStatus = false;
    /** Its skill face heals, draws or gives stamina (息), when it puts no status. */
    this.Breathes = false;
    /** It has a stance face (型). */
    this.Stance = false;
    if (init) Object.assign(this, init);
  }
}

// §5.3: the attack's words, read in the order 突 → 打 → 払 → 斬. 斬 needs no word: it is the default.
const ThrustWords = Object.freeze(['突', '貫', '矢', '弩', '撃', '投']);
const StrikeWords = Object.freeze(['打', '当', '礫', '圧殺', '押し込み']);
const SweepWords = Object.freeze(['薙', '払', '牽制']);

function HasAny(name, words) {
  for (const word of words) {
    // string.Contains(string) is ordinal.
    if (name.includes(word)) return true;
  }
  return false;
}

export const EffectGrammar = {
  /** Every system, in the order of §5.3's table. (C# static readonly FxSystem[]) */
  Systems: Object.freeze([
    FxSystem.Slash, FxSystem.Thrust, FxSystem.Sweep, FxSystem.Strike,
    FxSystem.Shield, FxSystem.Form, FxSystem.Spirit, FxSystem.Breath,
  ]),

  /**
   * §5.3 / §5.4: the systems one card or action plays, in the order it resolves its faces —
   * attack, (move, which has none), guard, skill, stance. A movement-only card has none.
   * @returns {string[]} List<FxSystem>
   */
  Of(faces) {
    if (faces == null) throw argNull('faces');
    const systems = [];
    if (faces.Attacks) systems.push(EffectGrammar.BlowOf(faces.Name, faces.Pushes));
    if (faces.Guards) systems.push(FxSystem.Shield);
    // §5.3: a skill face is 気 when the play puts a status on, 息 when it only heals or draws.
    if (faces.PutsStatus) systems.push(FxSystem.Spirit);
    else if (faces.Breathes) systems.push(FxSystem.Breath);
    if (faces.Stance) systems.push(FxSystem.Form);
    return systems;
  },

  /**
   * §5.3: the system of an attack face. A push or pull is what the blow is for, so it is 打
   * before any word is read; then the name's words in the order 突 → 打 → 払, and 斬 when none.
   */
  BlowOf(name, pushes) {
    if (pushes) return FxSystem.Strike;
    name = name ?? '';
    if (HasAny(name, ThrustWords)) return FxSystem.Thrust;
    if (HasAny(name, StrikeWords)) return FxSystem.Strike;
    if (HasAny(name, SweepWords)) return FxSystem.Sweep;
    return FxSystem.Slash;
  },

  /** The four systems that are a blow: they play inside the attack beat (StrikeShape). */
  IsBlow(system) {
    return system === FxSystem.Slash || system === FxSystem.Thrust || system === FxSystem.Sweep || system === FxSystem.Strike;
  },

  /** The shape a system draws. One form per system and no form shared. */
  FormOf(system) {
    switch (system) {
      case FxSystem.Slash: return FxForm.Arc;
      case FxSystem.Thrust: return FxForm.Line;
      case FxSystem.Sweep: return FxForm.Band;
      case FxSystem.Strike: return FxForm.Ripple;
      case FxSystem.Shield: return FxForm.Ring;
      case FxSystem.Form: return FxForm.Crest;
      case FxSystem.Spirit: return FxForm.Sigil;
      case FxSystem.Breath: return FxForm.Pips;
      default: throw argRange('system', system, 'No form is written for this system.');
    }
  },

  /**
   * The look a system draws, as one stable id ("fx.ring"). It is read off the system alone, so two
   * cards of the same system share it whatever their names, numbers or traits.
   */
  LookId(system) {
    // FxForm values are their member names, so ToString() is the value itself.
    return 'fx.' + EffectGrammar.FormOf(system).toLowerCase();
  },

  /**
   * §5.3: what the View starts beside this beat. A blow draws its shape inside the attack beat
   * (StrikeShape), so nothing here. 盾 / 型 / 気 draw their shape. 息's form is the pips lighting,
   * which the stamina beat already is, and a draw is its own beat, so only a heal adds the chest's
   * 400 ms.
   */
  MarkOf(cue) {
    if (cue == null || cue.Fx == null) return FxMark.None;
    const system = cue.Fx;
    if (EffectGrammar.IsBlow(system)) return FxMark.None;
    if (system !== FxSystem.Breath) return FxMark.Shape;
    return cue.Kind === CueKind.HpChange && cue.Amount > 0 ? FxMark.ChestHeave : FxMark.None;
  },

  /** The system's one character, as §5.3 names it. */
  Glyph(system) {
    switch (system) {
      case FxSystem.Slash: return '斬';
      case FxSystem.Thrust: return '突';
      case FxSystem.Sweep: return '払';
      case FxSystem.Strike: return '打';
      case FxSystem.Shield: return '盾';
      case FxSystem.Form: return '型';
      case FxSystem.Spirit: return '気';
      case FxSystem.Breath: return '息';
      default: throw argRange('system', system, 'No glyph is written for this system.');
    }
  },

  /** The blow shape and enemy motion (StrikeSystem) a system plays as. */
  ToStrike(system) {
    switch (system) {
      case FxSystem.Thrust: return StrikeSystem.Thrust;
      case FxSystem.Sweep: return StrikeSystem.Sweep;
      case FxSystem.Strike: return StrikeSystem.Strike;
      case FxSystem.Shield: return StrikeSystem.Shield;
      case FxSystem.Slash: return StrikeSystem.Slash;
      default: return StrikeSystem.Step; // 型 / 気 / 息 carry no blow and no lunge of their own
    }
  },

  /** The system a blow of this shape belongs to; null for a step, which has none. (FxSystem?) */
  FromStrike(strike) {
    switch (strike) {
      case StrikeSystem.Slash: return FxSystem.Slash;
      case StrikeSystem.Thrust: return FxSystem.Thrust;
      case StrikeSystem.Sweep: return FxSystem.Sweep;
      case StrikeSystem.Strike: return FxSystem.Strike;
      case StrikeSystem.Shield: return FxSystem.Shield;
      default: return null;
    }
  },
};
