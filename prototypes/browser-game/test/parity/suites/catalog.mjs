// Mirrors oracle/src/Suites/Catalog.cs.
import { Try, enumValues, arr, entries, readStatic, range } from '../lib/cs.mjs';

export const imports = [
  'src/core/battle/CardCatalog.js',
  'src/core/battle/Mastery.js',
  'src/core/battle/Cards.js',
  'src/core/battle/Attributes.js',
  'src/core/battle/EnemyAi.js',
  'src/core/battle/Enemies.js',
  'src/core/battle/Constants.js',
  'src/core/battle/Columns.js',
  'src/core/battle/Statuses.js',
  'src/core/battle/Types.js',
  'src/sim/Decks.js',
];

export function run(c, { use, expected }) {
  const [kind, name] = splitId(c.id);
  if (kind === 'card') {
    const { CardCatalog } = use('src/core/battle/CardCatalog.js', 'CardCatalog');
    const { Mastery, MasteryProfiles } = use('src/core/battle/Mastery.js', 'Mastery', 'MasteryProfiles');
    const { Cards } = use('src/core/battle/Cards.js', 'Cards');
    const { AttributeRule } = use('src/core/battle/Attributes.js', 'AttributeRule');
    const { EnemyAi } = use('src/core/battle/EnemyAi.js', 'EnemyAi');
    const byId = CardCatalog.ById(c.input.Id);
    const profile = MasteryProfiles.Find(c.input.Id) ?? null;
    const ranks = [];
    if (profile != null) for (let rank = 1; rank <= Mastery.TopRank; rank++) ranks.push(Try(() => Mastery.Master(byId, rank, profile)));
    return {
      Def: byId,
      Profile: profile,
      Ranks: ranks,
      IsStanceCard: Cards.IsStanceCard(byId),
      StanceStandsAlone: Cards.StanceStandsAlone(byId),
      MultiHitCarriesNoFoeStatus: Cards.MultiHitCarriesNoFoeStatus(byId),
      HasMovement: AttributeRule.HasMovement(byId.Face),
      IsMovementCard: AttributeRule.IsMovementCard(byId.Attributes, byId.Face),
      IsOpponentDirected: EnemyAi.IsOpponentDirected(byId.Attributes, byId.Face, byId.Targets),
      GapHolds0to5: range(0, 6).map((gap) => Mastery.GapHolds(byId, gap, profile)),
    };
  }
  if (kind === 'enemy') {
    const { Enemies } = use('src/core/battle/Enemies.js', 'Enemies');
    const { Cards } = use('src/core/battle/Cards.js', 'Cards');
    const byId = Enemies.ById(c.input.Id);
    const validation = {};
    for (const [k, v] of entries(byId.Actions)) validation[k] = Cards.ValidateEnemyAction(v);
    return { Def: byId, ValidateEnemyAction: validation };
  }
  // table:<Name>: the static members the oracle listed, plus the extras below.
  const keys = Object.keys(expected || {});
  switch (name) {
    case 'Constants': {
      const { Constants } = use('src/core/battle/Constants.js', 'Constants');
      return readStatic(Constants, keys);
    }
    case 'Columns': {
      const { Columns } = use('src/core/battle/Columns.js', 'Columns');
      const m = readStatic(Columns, keys.filter((k) => k !== 'CostOf0to5'));
      m.CostOf0to5 = range(0, 6).map((col) => Try(() => Columns.CostOf(col)));
      return m;
    }
    case 'Statuses': {
      const { Statuses } = use('src/core/battle/Statuses.js', 'Statuses');
      const { StatusKind, EnumTokens } = use('src/core/battle/Types.js', 'StatusKind', 'EnumTokens');
      const m = readStatic(Statuses, keys.filter((k) => k !== 'Kinds'));
      const kinds = {};
      for (const kind of enumValues(StatusKind)) {
        kinds[kind] = {
          DecayOf: Statuses.DecayOf(kind),
          IsOwn: Try(() => Statuses.IsOwn(kind)),
          IsBossOnly: Statuses.IsBossOnly(kind),
          ToToken: EnumTokens.ToToken(kind),
          ToLabel: EnumTokens.ToLabel(kind),
        };
      }
      m.Kinds = kinds;
      return m;
    }
    case 'PrototypeDeck': {
      const { PrototypeDeck } = use('src/core/battle/CardCatalog.js', 'PrototypeDeck');
      const { Cards } = use('src/core/battle/Cards.js', 'Cards');
      return {
        Copies: PrototypeDeck.Copies,
        Kinds: arr(PrototypeDeck.Kinds).map((d) => d.Id),
        Build: arr(PrototypeDeck.Build()).map((ci) => ci.InstanceId),
        Validate: Cards.Validate(PrototypeDeck.Build()),
      };
    }
    case 'MasteryProfiles': {
      const { Mastery, MasteryProfiles } = use('src/core/battle/Mastery.js', 'Mastery', 'MasteryProfiles');
      return {
        All: MasteryProfiles.All,
        TopRank: Mastery.TopRank,
        MovementGuardByRank: Mastery.MovementGuardByRank,
        MovementExtraByRank: Mastery.MovementExtraByRank,
      };
    }
    case 'Decks': {
      const { Decks } = use('src/sim/Decks.js', 'Decks');
      const { Cards } = use('src/core/battle/Cards.js', 'Cards');
      const all = [...arr(Decks.Archetypes), Decks.CostOneOnly(), Decks.Initial(), Decks.InitialForty(), Decks.Plain(), ...arr(Decks.NoStanceTwins())];
      return all.map((d) => ({ Deck: d, Build: arr(d.Build()).map((ci) => ci.InstanceId), Validate: Cards.Validate(d.Build()) }));
    }
    default:
      throw new Error(`harness: unknown catalog case ${c.id}`);
  }
}

function splitId(id) {
  const at = id.indexOf(':');
  return [id.slice(0, at), id.slice(at + 1)];
}
