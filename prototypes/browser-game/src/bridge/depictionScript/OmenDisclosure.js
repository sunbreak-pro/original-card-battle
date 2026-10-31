// Port of unity-project-kit/Assets/View/Depiction/Script/OmenDisclosure.cs
// What the omen badge may show for how far the journal has read the enemy (battle-visual-v1 §4.4,
// battle_ui_ux_v2 §7, #455). Pure C#: the writer of the script applies it, so the View shows what it
// is handed and judges nothing. The level reaches the writer once, as the battle starts (#500:
// BattleLaunch, DemoSession); a battle nobody hands a level to is written at Full (2).
//
// Port notes: this module imports nothing from DepictionTypes (OmenFrame's constructor reads
// OmenDisclosure.Full, so the two would form a cycle); Apply works on any OmenFrame it is handed.
// `List.Clear()` empties the frame's lists in place (`length = 0`), as the C# does.
import { ArgumentNullException } from '../../shared/cs.js';

/** new ArgumentNullException(paramName) with .NET's message. */
function argNull(name) {
  const e = new ArgumentNullException(`Value cannot be null. (Parameter '${name}')`);
  e.ParamName = name;
  return e;
}

export const OmenDisclosure = {
  /** 開示度 0: the first omen's kind (icon and word) only; no plan. */
  Unread: 0,

  /** 開示度 1. */
  Partial: 1,

  /** 開示度 2: everything the badge has. The default until the journal hands a level in. */
  Full: 2,

  /**
   * battle-visual-v1 §4.4: the numbers (attack and guard, on the omen and on the plan) show for an
   * enemy read to 2 only; 0 and 1 show the icon and the kind word. battle_ui_ux_v2 §7 says the same
   * since #496 (decision A, 2026-10-12).
   */
  ShowsNumbers(level) {
    return OmenDisclosure.Clamp(level) >= OmenDisclosure.Full;
  },

  /**
   * battle_ui_ux_v2 §7 / §3.10 / §5.16 順 2: an elite's or a boss's second action (the plan) shows
   * from 開示度 1; an enemy read to 0 shows its first omen's kind alone. battle-visual-v1 §4.4 says
   * nothing of the plan at 0, so the two do not disagree here.
   */
  ShowsPlan(level) {
    return OmenDisclosure.Clamp(level) >= OmenDisclosure.Partial;
  },

  /**
   * `omen` as an enemy read to `level` shows it: the level is kept on the frame, and what it may not
   * show is taken off — below 2 the numbers and the statuses a 状態付与 omen puts on (§4.4: 数字と状態の
   * アイコンは開示度 2 の敵にだけ), at 0 the plan as well. The kind, its icon and the hit mark are left
   * as they are. Returns the same frame.
   */
  Apply(omen, level) {
    if (omen == null) throw argNull('omen');
    omen.Disclosure = OmenDisclosure.Clamp(level);
    if (!OmenDisclosure.ShowsNumbers(level)) {
      omen.ValueText = '';
      omen.PlanValueText = '';
      omen.Statuses.length = 0;
      omen.PlanStatuses.length = 0;
    }
    if (!OmenDisclosure.ShowsPlan(level)) omen.PlanVisible = false;
    return omen;
  },

  /**
   * 0〜2. The −1 of battle_ui_ux_v2 §7 (the enemies of the 100% run, hidden whole) has no parts
   * yet, so it reads as 0.
   */
  Clamp(level) {
    return Math.max(OmenDisclosure.Unread, Math.min(OmenDisclosure.Full, level));
  },
};
