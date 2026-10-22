// Port of unity-port/unity-project-kit/Assets/View/Depiction/Bridge/CardSystems.cs
// battle_ui_ux_v2 §5.3 (#57): which of the eight systems each card and each enemy action plays as.
// Pure: BattleCore + Depiction.Script. The rule is the grammar's (EffectGrammar.Of); this file only
// reads the core's faces into it and keeps the table by card id, so the writer puts on the script
// what the table says and the View never works a system out.
import { hasFlag } from '../../shared/cs.js';
import { BattleAttribute, EnemyActionDef } from '../../core/battle/Types.js';
import { CardCatalog } from '../../core/battle/CardCatalog.js';
import { EffectGrammar, FxFaces } from '../depictionScript/EffectGrammar.js';

/**
 * C# `public static readonly ... Table = Build();` — a static initializer, run on first use of the
 * type. Built lazily here so module load order (circular imports) cannot read CardCatalog early.
 */
let _table = null;

export const CardSystems = {
  /**
   * Every card the core knows (CardCatalog.All), by its id: the systems it plays, in the order
   * its faces resolve (§5.4). A movement-only card maps to an empty list (§5.3: a move has none).
   * C# IReadOnlyDictionary<string, IReadOnlyList<FxSystem>> → Map of frozen arrays.
   * @returns {Map<string, ReadonlyArray<string>>}
   */
  get Table() {
    if (_table == null) _table = CardSystems.Build();
    return _table;
  },

  /**
   * C# has two overloads, Of(CardDef) and Of(EnemyActionDef); this dispatches on the type.
   *
   * Of(CardDef): the systems of a card. A card the table holds plays its row, so a mastered card that
   * keeps its id under a new name keeps its look; any other card is read off its own faces.
   *
   * Of(EnemyActionDef): §5.3: an enemy's action is read by the same rule as a card.
   * @returns {string[]}
   */
  Of(defOrAction) {
    if (defOrAction == null) return [];
    if (defOrAction instanceof EnemyActionDef) {
      const action = defOrAction;
      return EffectGrammar.Of(CardSystems.FacesOf(action.Name, action.Attributes, action.Face));
    }
    const def = defOrAction;
    const table = CardSystems.Table;
    return table.has(def.Id) ? table.get(def.Id) : EffectGrammar.Of(CardSystems.FacesOf(def.Name, def.Attributes, def.Face));
  },

  /** The faces as the grammar reads them: what the definition declares, and what its face carries. */
  FacesOf(name, attributes, face) {
    const faces = new FxFaces();
    faces.Name = name ?? '';
    faces.Attacks = hasFlag(attributes, BattleAttribute.Attack) || face.Power > 0;
    faces.Pushes = face.Push !== 0;
    faces.Guards = hasFlag(attributes, BattleAttribute.Guard) || face.Guard > 0;
    faces.PutsStatus = face.StatusList.length > 0;
    faces.Breathes = face.Heal > 0 || face.Draw > 0 || face.StaminaGain > 0;
    faces.Stance = hasFlag(attributes, BattleAttribute.Stance) || face.Stance != null;
    return faces;
  },

  /** private static Dictionary<string, IReadOnlyList<FxSystem>> Build() */
  Build() {
    const table = new Map();
    for (const def of CardCatalog.All) {
      // .AsReadOnly(): a read-only view; frozen here so a write throws as it would in C#.
      table.set(def.Id, Object.freeze(EffectGrammar.Of(CardSystems.FacesOf(def.Name, def.Attributes, def.Face))));
    }
    return table;
  },
};
