// Mirrors oracle/src/Suites/Texts.cs.
import { Try, enumValues, arr, lookup } from '../lib/cs.mjs';

export const imports = [
  'src/core/battle/CardCatalog.js',
  'src/core/battle/Mastery.js',
  'src/core/battle/Enemies.js',
  'src/core/battle/Statuses.js',
  'src/core/battle/Types.js',
  'src/core/battle/TurnLoop.js',
  'src/bridge/depiction/CoreText.js',
  'src/bridge/depiction/CardFaceText.js',
  'src/bridge/depiction/StatusChipText.js',
];

const STATUS_STACKS = [1, 2, 3, 5];

export function run(c, { use }) {
  const kind = c.id.slice(0, c.id.indexOf(':'));
  const { CoreText } = use('src/bridge/depiction/CoreText.js', 'CoreText');
  switch (kind) {
    case 'card': {
      const { CardCatalog } = use('src/core/battle/CardCatalog.js', 'CardCatalog');
      const { Mastery, MasteryProfiles } = use('src/core/battle/Mastery.js', 'Mastery', 'MasteryProfiles');
      const { CardFaceText } = use('src/bridge/depiction/CardFaceText.js', 'CardFaceText');
      let def = CardCatalog.ById(c.input.Card);
      if (c.input.Rank > 0) def = Mastery.Master(def, c.input.Rank, MasteryProfiles.Find(c.input.Card));
      return cardTexts(def, CoreText, CardFaceText);
    }
    case 'status': {
      const { StatusChipText } = use('src/bridge/depiction/StatusChipText.js', 'StatusChipText');
      const { EnumTokens } = use('src/core/battle/Types.js', 'EnumTokens');
      const k = c.input.Kind;
      return {
        Stacks: STATUS_STACKS,
        Chip: STATUS_STACKS.map((n) => Try(() => StatusChipText.Chip(k, n))),
        Effect: STATUS_STACKS.map((n) => Try(() => StatusChipText.Effect(k, n))),
        KindOf: Try(() => StatusChipText.KindOf(k)),
        GroupWord: Try(() => StatusChipText.GroupWord(StatusChipText.KindOf(k))),
        Decay: Try(() => StatusChipText.Decay(k)),
        GlyphOf: CoreText.GlyphOf(k),
        MoveBlockedLine: CoreText.MoveBlockedLine(k),
        ToToken: EnumTokens.ToToken(k),
        ToLabel: EnumTokens.ToLabel(k),
      };
    }
    case 'chips': {
      const { StatusChipText } = use('src/bridge/depiction/StatusChipText.js', 'StatusChipText');
      const { StatusSet } = use('src/core/battle/Statuses.js', 'StatusSet');
      // The set is rebuilt with Add from Empty (same clamp and order as StatusSet.Of for fresh kinds).
      let set = StatusSet.Empty;
      for (const [k, n] of Object.entries(c.input.Statuses.Stacks)) set = set.Add(k, n);
      const stances = c.input.Stances;
      return {
        Chips: Try(() => StatusChipText.Chips(set, stances)),
        StanceChips: Try(() => StatusChipText.StanceChips(stances)),
        CoreChips: Try(() => CoreText.Chips(set, stances)),
      };
    }
    case 'action': {
      const { Enemies } = use('src/core/battle/Enemies.js', 'Enemies');
      const { OmenKind } = use('src/core/battle/Types.js', 'OmenKind');
      const enemy = Enemies.ById(c.input.Enemy);
      let action;
      if (c.input.Source === 'Actions') action = lookup(enemy.Actions, c.input.Action);
      else {
        const sw = arr(enemy.Switches).find((s) => 'Switch:' + s.Id === c.input.Source);
        action = arr(sw.Overrides).find((a) => a.Id === c.input.Action);
      }
      const byKind = {};
      for (const k of enumValues(OmenKind)) {
        byKind[k] = {
          IconOf: CoreText.IconOf(k, action),
          OmenStatusesOf: Try(() => CoreText.OmenStatusesOf(k, action)),
          OmenValueOf: Try(() => CoreText.OmenValueOf(k, action, null)),
        };
      }
      return {
        Action: action,
        SystemOf: Try(() => CoreText.SystemOf(action)),
        Describe: Try(() => CoreText.Describe(action.Face, action.Attributes)),
        ValueOf: CoreText.ValueOf(action.Face, action.Attributes),
        FacePower: CoreText.FacePower(action.Face),
        TraitLine: Try(() => CoreText.TraitLine(action.Trait)),
        StanceText: Try(() => CoreText.StanceText(action.Face.Stance)),
        ByOmenKind: byKind,
      };
    }
    case 'omen': {
      const { Enemies } = use('src/core/battle/Enemies.js', 'Enemies');
      const { Omen, OmenLabel, Reach } = use('src/core/battle/Types.js', 'Omen', 'OmenLabel', 'Reach');
      const { OmenPreview } = use('src/core/battle/TurnLoop.js', 'OmenPreview');
      const enemy = Enemies.ById(c.input.Enemy);
      const o = c.input.Omen;
      const reach = o.Label.Reach ? new Reach(o.Label.Reach.Min, o.Label.Reach.Max) : null;
      const omen = new Omen(o.ActionId, new OmenLabel(o.Label.Kind, reach));
      const p = c.input.Preview;
      const preview = p ? new OmenPreview(p.RawPower, p.Damage, p.Lands, p.Rests) : null;
      return {
        OmenOf: CoreText.OmenOf(omen, enemy, preview),
        OmenOfNoPreview: CoreText.OmenOf(omen, enemy),
        OmenKindWord: Try(() => CoreText.OmenKindWord(omen.Label.Kind)),
        ReachText: omen.Label.Reach ? omen.Label.Reach.ToText() : null,
      };
    }
    default:
      throw new Error(`harness: unknown texts case ${c.id}`);
  }
}

export function cardTexts(def, CoreText, CardFaceText) {
  const traits = arr(def.AllTraits).map((t) => ({
    Trait: t,
    TraitLine: Try(() => CoreText.TraitLine(t)),
    ConditionWord: Try(() => CoreText.ConditionWord(t)),
    EffectWord: Try(() => CoreText.EffectWord(t)),
    Condition: Try(() => CardFaceText.Condition(t, def)),
    Effect: Try(() => CardFaceText.Effect(t, def)),
    ConditionClause: Try(() => CardFaceText.ConditionClause(t, def)),
    EffectClause: Try(() => CardFaceText.EffectClause(t, def)),
  }));
  const rows = CardFaceText.TraitRows(def);
  return {
    Def: def,
    FaceOf: Try(() => CoreText.FaceOf(def)),
    KindOf: CoreText.KindOf(def.Attribute),
    KindOfDeclared: CoreText.KindOf(def.Attributes),
    KindWord: CoreText.KindWord(CoreText.KindOf(def.Attribute)),
    SystemOf: Try(() => CoreText.SystemOf(def)),
    ReachHint: CoreText.ReachHint(def.Attributes, def.Face, def.Targets),
    ReachText: CoreText.ReachText(def.Attributes, def.Face, def.Targets),
    Describe: Try(() => CoreText.Describe(def.Face, def.Attributes)),
    ValueOf: CoreText.ValueOf(def.Face, def.Attributes),
    FacePower: CoreText.FacePower(def.Face),
    TraitLines: Try(() => CoreText.TraitLines(def)),
    StanceText: Try(() => CoreText.StanceText(def.Face.Stance)),
    Traits: traits,
    Values: CardFaceText.Values(def.Face, def.Attributes),
    TextLines: CardFaceText.TextLines(def.Face, def.Attributes),
    LastingLines: CardFaceText.LastingLines(def.Face),
    TraitRows: rows,
    HoverLine: Try(() => CardFaceText.HoverLine(def)),
    Detail: Try(() => CardFaceText.Detail(def)),
    KindLine: Try(() => CardFaceText.KindLine(def)),
    TermsOf: Try(() => CardFaceText.TermsOf(def)),
    TermMarks: Try(() => CardFaceText.TermMarks(def, rows[0], rows[1])),
    AttributeWord: Try(() => CardFaceText.AttributeWord(def.Attribute)),
    BoundRefusal: CoreText.BoundRefusal(def.Name),
  };
}
